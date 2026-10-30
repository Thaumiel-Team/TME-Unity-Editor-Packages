using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Components.Tools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Assets.Scripts.Tools.Editor
{
    [InitializeOnLoad]
    public static class DopeSheetMenuPatch
    {
        private const string HarmonyId = "tme.animationruntime.dopesheetmenu";
        private const string MenuLabel = "Add Blocky Entry using Key";

        private static object _harmony;
        private static Type _harmonyType;

        private static string HarmonyPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "Harmony", "0Harmony.dll");

        static DopeSheetMenuPatch() =>
            Apply();

        private static void Apply()
        {
            try
            {
                string path = HarmonyPath;
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[DopeSheetMenuPatch] Harmony not found at '{path}'. The Dopesheet menu patch was not applied.");
                    return;
                }

                Assembly harmonyAssembly = Assembly.LoadFrom(path);
                _harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
                Type harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);

                Type dopeSheet = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditorInternal.DopeSheetEditor", false)).FirstOrDefault(t => t != null);

                MethodInfo target = dopeSheet?.GetMethod("GenerateMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (target == null)
                {
                    Debug.LogWarning($"[DopeSheetMenuPatch] Could not find UnityEditorInternal.DopeSheetEditor.GenerateMenu method. The Dopesheet menu patch was not applied.");
                    return;
                }

                MethodInfo postfix = typeof(DopeSheetMenuPatch).GetMethod(nameof(GenerateMenuPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                object postfixMethod = Activator.CreateInstance(harmonyMethodType);
                harmonyMethodType.GetField("method").SetValue(postfixMethod, postfix);

                _harmony = Activator.CreateInstance(_harmonyType, HarmonyId);

                MethodInfo patch = _harmonyType.GetMethods()
                    .First(m => m.Name == "Patch" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(MethodBase));

                ParameterInfo[] parameters = patch.GetParameters();
                object[] args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i == 0)
                    {
                        args[i] = target;
                    }
                    else if (parameters[i].Name == "postfix")
                    {
                        args[i] = postfixMethod;
                    }
                    else
                        args[i] = null;
                }

                patch.Invoke(_harmony, args);

                AssemblyReloadEvents.beforeAssemblyReload += Unpatch;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DopeSheetMenuPatch] Dopesheet menu patch failed: {e.Message}");
            }
        }

        private static void Unpatch()
        {
            try
            {
                if (_harmony == null || _harmonyType == null)
                    return;

                MethodInfo unpatchSelf = _harmonyType.GetMethod("UnpatchSelf", Type.EmptyTypes);
                if (unpatchSelf != null)
                {
                    unpatchSelf.Invoke(_harmony, null);
                }
                else
                    _harmonyType.GetMethod("UnpatchAll", new[] { typeof(string) })?.Invoke(_harmony, new object[] { HarmonyId });
            }
            catch (Exception)
            {
            }

            _harmony = null;
        }

        private static void GenerateMenuPostfix(object __instance, GenericMenu __result)
        {
            try
            {
                if (__instance == null || __result == null)
                    return;

                object state = Read(__instance, "state");

                GameObject stateRoot = Read(state, "activeRootGameObject") as GameObject;
                GameObject root = stateRoot != null ? stateRoot : Selection.activeGameObject;

                AnimationWindow mouseOverWindow = EditorWindow.mouseOverWindow as AnimationWindow;
                AnimationWindow window = mouseOverWindow != null ? mouseOverWindow : Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault();

                AnimationClip clip = window != null ? window.animationClip : null;
                AnimationRuntime runtime = root != null ? root.GetComponent<AnimationRuntime>() : null;

                __result.AddSeparator("");

                if (runtime == null)
                {
                    __result.AddDisabledItem(new GUIContent(MenuLabel + " (needs AnimationRuntime)"));
                    return;
                }

                if (clip == null)
                {
                    __result.AddDisabledItem(new GUIContent(MenuLabel + " (no clip selected)"));
                    return;
                }

                List<float> keyTimes = AnimationRuntime.GetKeyTimes(clip);
                if (keyTimes.Count == 0)
                {
                    __result.AddDisabledItem(new GUIContent(MenuLabel + " (clip has no keys)"));
                    return;
                }

                string stateName = FindStateName(runtime, clip);
                if (string.IsNullOrEmpty(stateName))
                {
                    __result.AddDisabledItem(new GUIContent(MenuLabel + " (clip is not used by the Animator)"));
                    return;
                }

                float time = TimeUnderCursor(state, clip);
                int keyIndex = AnimationRuntime.FindClosestKeyIndex(keyTimes, time);
                float keyTime = keyTimes[keyIndex];
                float frameRate = clip.frameRate > 0f ? clip.frameRate : 60f;
                int frame = Mathf.RoundToInt(keyTime * frameRate);

                __result.AddItem(new GUIContent($"{MenuLabel} (frame {frame})"), false, () => AddEntry(runtime, stateName, keyIndex, keyTime));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static float TimeUnderCursor(object state, AnimationClip clip)
        {
            float time = 0f;

            MethodInfo pixelToTime = state?.GetType().GetMethod("PixelToTime", new[] { typeof(float) });
            if (pixelToTime != null && Event.current != null)
            {
                float raw = (float)pixelToTime.Invoke(state, new object[] { Event.current.mousePosition.x });
                float fps = Read(state, "frameRate") is float f && f > 0f ? f : clip.frameRate;
                time = Mathf.Round(raw * fps) / fps;
            }

            return Mathf.Clamp(time, 0f, clip.length);
        }

        private static void AddEntry(AnimationRuntime runtime, string stateName, int keyIndex, float keyTime)
        {
            if (runtime == null)
                return;

            Undo.RecordObject(runtime, "Add Animation Blocky Entry");
            runtime.entries.Add(new AnimationRuntime.KeyedBlocks
            {
                stateName = stateName,
                keyIndex = keyIndex,
                keyTime = keyTime
            });

            runtime.RefreshKeyTimes();

            EditorUtility.SetDirty(runtime);
            PrefabUtility.RecordPrefabInstancePropertyModifications(runtime);

            Debug.Log($"[DopeSheetMenuPatch] Added entry {runtime.entries.Count - 1} for state '{stateName}' at key {keyIndex} ({keyTime:0.###}s). Select the object and use Edit Blocks on that entry.");
        }

        private static object Read(object obj, string name)
        {
            if (obj == null)
                return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = obj.GetType();

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property.GetValue(obj);

            return type.GetField(name, flags)?.GetValue(obj);
        }

        private static string FindStateName(AnimationRuntime runtime, AnimationClip clip)
        {
            Animator animator = runtime.GetComponent<Animator>();
            RuntimeAnimatorController rac = animator != null ? animator.runtimeAnimatorController : null;

            AnimatorOverrideController overrideController = rac as AnimatorOverrideController;
            if (overrideController != null)
                rac = overrideController.runtimeAnimatorController;

            if (rac is not AnimatorController controller || runtime.layer < 0 || runtime.layer >= controller.layers.Length)
                return "";

            return FindState(controller.layers[runtime.layer].stateMachine, overrideController, clip) ?? "";
        }

        private static string FindState(AnimatorStateMachine machine, AnimatorOverrideController overrides, AnimationClip clip)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                AnimationClip baseClip = child.state.motion as AnimationClip;
                AnimationClip stateClip = AnimationRuntime.ResolveClip(baseClip, overrides);

                if (stateClip == clip || baseClip == clip)
                    return child.state.name;
            }

            foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            {
                string found = FindState(sub.stateMachine, overrides, clip);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}