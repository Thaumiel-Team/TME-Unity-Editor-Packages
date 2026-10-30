#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Networking.Blocky;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Assets.Scripts.Components.Tools
{
    [InitializeOnLoad]
    [CustomEditor(typeof(AnimationRuntime))]
    public class AnimationRuntimeEditor : Editor
    {
        private const string EventId = AnimationRuntime.EventId;
        private const string KeyTarget = "AnimationRuntime.target";
        private const string KeyIndex = "AnimationRuntime.index";

        private static AnimationRuntime _editing;
        private static int _editingIndex = -1;

        private sealed class StateEntry
        {
            public string name;
            public AnimationClip clip;
            public List<float> keyTimes;
        }

        static AnimationRuntimeEditor()
        {
            BlocklyServer.OnCodeExport += OnCodeExport;
            BlocklyServer.OnClientConnected += RegisterEvent;
        }

        private static void RegisterEvent()
        {
            BlocklyServer.RegisterEvent(new BlockEvent
            {
                Id = EventId,
                Label = "OnAnimationKey",
                Method = "OnAnimationKey",
                Color = "#00838F",
                Tooltip = "Runs when the animation reaches the chosen key.",
                Explanation = "Blocks inside run when the Animator animation reaches the selected animation key."
            });
        }

        private static void OnCodeExport(CodeExportPayload payload, string targetEvent)
        {
            if (payload == null || targetEvent != EventId)
                return;

            AnimationRuntime comp = ResolveTarget(out int index);
            if (comp == null || index < 0 || index >= comp.entries.Count)
                return;

            Undo.RecordObject(comp, "Update Animation Runtime Blocks");

            comp.EditingIndex = index;
            comp.OnBlocklyExportReceived(payload, targetEvent);

            EditorUtility.SetDirty(comp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);

            foreach (AnimationRuntimeEditor ed in Resources.FindObjectsOfTypeAll<AnimationRuntimeEditor>())
                ed.Repaint();
        }

        private static AnimationRuntime ResolveTarget(out int index)
        {
            if (_editing != null)
            {
                index = _editingIndex;
                return _editing;
            }

            index = SessionState.GetInt(KeyIndex, -1);
            string id = SessionState.GetString(KeyTarget, "");
            if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId gid))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as AnimationRuntime;
        }

        private static void ClearEditing()
        {
            if (_editing != null)
                _editing.EditingIndex = -1;

            _editing = null;
            _editingIndex = -1;
            SessionState.EraseString(KeyTarget);
            SessionState.EraseInt(KeyIndex);
        }

        private static AnimationRuntime _pendingComp;
        private static int _pendingIndex = -1;

        private static void BeginEdit(AnimationRuntime comp, int index)
        {
            if (!BlocklyServer.IsRunning)
                BlocklyServer.StartServer();

            if (!BlocklyServer.IsClientConnected)
            {
                _pendingComp = comp;
                _pendingIndex = index;

                BlocklyServer.OnClientConnected -= OnBlocklyClientConnected;
                BlocklyServer.OnClientConnected += OnBlocklyClientConnected;

                if (!TryOpenBlocklyPage())
                {
                    BlocklyServer.OnClientConnected -= OnBlocklyClientConnected;
                    _pendingComp = null;
                    _pendingIndex = -1;
                    return;
                }

                if (BlocklyServer.IsClientConnected)
                    OnBlocklyClientConnected();

                return;
            }

            LoadEntryAfterSwitch(comp, index);
        }

        private static void OnBlocklyClientConnected()
        {
            BlocklyServer.OnClientConnected -= OnBlocklyClientConnected;

            AnimationRuntime comp = _pendingComp;
            int index = _pendingIndex;
            _pendingComp = null;
            _pendingIndex = -1;

            if (comp == null)
                return;

            LoadEntryAfterSwitch(comp, index);
        }

        private static void LoadEntryAfterSwitch(AnimationRuntime comp, int index)
        {
            if (comp == null || index < 0 || index >= comp.entries.Count)
                return;

            AnimationRuntime current = ResolveTarget(out int currentIndex);
            bool switching = current != null && (current != comp || currentIndex != index);

            if (switching)
            {
                BlocklyServer.RequestExport();
                RunAfter(0.5, () => LoadEntry(comp, index));
            }
            else
                LoadEntry(comp, index);
        }

        private static bool TryOpenBlocklyPage()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "Blocky", "index.html").Replace(@"\", @"/");
            string absolutePath = Path.GetFullPath(filePath);

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[Blocky] Could not find HTML file at: {filePath}");
                return false;
            }

            Application.OpenURL(new Uri(absolutePath).AbsoluteUri);
            return true;
        }

        private static void LoadEntry(AnimationRuntime comp, int index)
        {
            if (comp == null || index < 0 || index >= comp.entries.Count)
                return;

            if (_editing != null && _editing != comp)
                _editing.EditingIndex = -1;

            _editing = comp;
            _editingIndex = index;
            comp.EditingIndex = index;
            SessionState.SetString(KeyTarget, GlobalObjectId.GetGlobalObjectIdSlow(comp).ToString());
            SessionState.SetInt(KeyIndex, index);

            RegisterEvent();
            BlocklyServer.ActiveTargetEvent = EventId;

            string xml = comp.entries[index].payload?.Xml;
            if (string.IsNullOrEmpty(xml))
            {
                BlocklyServer.LoadEvent(EventId);
            }
            else
                BlocklyServer.LoadXml(xml, EventId);
        }

        private static void RunAfter(double seconds, Action action)
        {
            double due = EditorApplication.timeSinceStartup + seconds;

            void Tick()
            {
                if (EditorApplication.timeSinceStartup < due)
                    return;

                EditorApplication.update -= Tick;
                action();
            }

            EditorApplication.update += Tick;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AnimationRuntime comp = (AnimationRuntime)target;
            SerializedProperty layerProp = serializedObject.FindProperty("layer");
            EditorGUILayout.PropertyField(layerProp);

            if (comp.GetComponent<Animator>() == null)
                EditorGUILayout.HelpBox("No Animator on this GameObject. Add one so animations and animation keys can be listed.", MessageType.Info);

            List<StateEntry> states = GetStates(comp, layerProp.intValue);
            SerializedProperty list = serializedObject.FindProperty("entries");

            AnimationRuntime editingComp = ResolveTarget(out int editingIdx);
            int remove = -1;
            int editRequest = -1;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty el = list.GetArrayElementAtIndex(i);
                SerializedProperty stateProp = el.FindPropertyRelative("stateName");
                SerializedProperty keyIndexProp = el.FindPropertyRelative("keyIndex");
                SerializedProperty keyTimeProp = el.FindPropertyRelative("keyTime");
                SerializedProperty keyResolvedProp = el.FindPropertyRelative("keyResolved");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string previousState = stateProp.stringValue;
                    DrawStatePopup(stateProp, states);
                    bool stateChanged = stateProp.stringValue != previousState;
                    StateEntry state = FindState(states, stateProp.stringValue);

                    if (stateChanged)
                    {
                        keyIndexProp.intValue = 0;
                        keyTimeProp.floatValue = state != null && state.keyTimes.Count > 0 ? state.keyTimes[0] : 0f;
                        keyResolvedProp.boolValue = true;
                    }
                    else if (state != null && state.keyTimes.Count > 0)
                    {
                        int currentIndex = keyIndexProp.intValue;
                        if (!keyResolvedProp.boolValue || currentIndex < 0 || currentIndex >= state.keyTimes.Count)
                        {
                            keyIndexProp.intValue = AnimationRuntime.FindClosestKeyIndex(state.keyTimes, keyTimeProp.floatValue);
                            keyResolvedProp.boolValue = true;
                        }
                    }

                    if (state == null)
                    {
                        keyResolvedProp.boolValue = false;

                        if (string.IsNullOrEmpty(stateProp.stringValue))
                        {
                            EditorGUILayout.HelpBox("Select a animation to bind this entry to an animation key.", MessageType.Warning);
                        }
                        else
                            EditorGUILayout.HelpBox($"Animation '{stateProp.stringValue}' was not found in the Animator.", MessageType.Warning);
                    }
                    else if (state.keyTimes.Count == 0)
                    {
                        keyIndexProp.intValue = 0;
                        keyTimeProp.floatValue = 0f;
                        keyResolvedProp.boolValue = false;
                        EditorGUILayout.HelpBox("This animation's clip has no animation keys.", MessageType.Warning);
                    }
                    else
                    {
                        DrawKeyPopup(keyIndexProp, state);
                        keyTimeProp.floatValue = state.keyTimes[keyIndexProp.intValue];
                        keyResolvedProp.boolValue = true;
                    }

                    CodeExportPayload payload = comp.entries[i]?.payload;
                    EditorGUILayout.LabelField(Describe(payload), EditorStyles.miniLabel);

                    bool editingThis = editingComp == comp && editingIdx == i;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(editingThis ? "Editing in Blocky..." : "Edit Blocks"))
                            editRequest = i;

                        using (new EditorGUI.DisabledScope(!editingThis))
                        {
                            if (GUILayout.Button(new GUIContent("Pull", "Tell the Blocky page to send the current blocks."), GUILayout.Width(52)))
                                BlocklyServer.RequestExport();
                        }

                        if (GUILayout.Button("⮾", GUILayout.Width(26)))
                            remove = i;
                    }
                }
            }

            if (remove >= 0)
            {
                if (editingComp == comp)
                    ClearEditing();

                list.DeleteArrayElementAtIndex(remove);
            }

            if (GUILayout.Button("+ Add"))
            {
                StateEntry first = states.FirstOrDefault(s => s.keyTimes.Count > 0) ?? states.FirstOrDefault();

                list.arraySize++;
                SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
                added.FindPropertyRelative("stateName").stringValue = first != null ? first.name : "";
                added.FindPropertyRelative("keyIndex").intValue = 0;
                added.FindPropertyRelative("keyTime").floatValue = first != null && first.keyTimes.Count > 0 ? first.keyTimes[0] : 0f;
                added.FindPropertyRelative("keyResolved").boolValue = first != null && first.keyTimes.Count > 0;
                added.FindPropertyRelative("payload.Xml").stringValue = "";
                added.FindPropertyRelative("payload.Code").stringValue = "";
                added.FindPropertyRelative("payload.Timestamp").stringValue = "";
            }

            serializedObject.ApplyModifiedProperties();

            if (editRequest >= 0)
            {
                BeginEdit(comp, editRequest);
                GUIUtility.ExitGUI();
            }
        }

        private static void DrawStatePopup(SerializedProperty prop, List<StateEntry> states)
        {
            if (states.Count == 0)
            {
                EditorGUILayout.PropertyField(prop, new GUIContent("Animation"));
                return;
            }

            List<string> names = new() { "(Select animation)" };
            names.AddRange(states.Select(s => s.name));

            int current = string.IsNullOrEmpty(prop.stringValue) ? 0 : names.IndexOf(prop.stringValue);
            if (current < 0)
            {
                names.Add(prop.stringValue + " (missing)");
                current = names.Count - 1;
            }

            int picked = EditorGUILayout.Popup("Animation", current, names.ToArray());
            if (picked != current)
                prop.stringValue = picked == 0 ? "" : names[picked];
        }

        private static void DrawKeyPopup(SerializedProperty keyIndexProp, StateEntry state)
        {
            List<string> names = new(state.keyTimes.Count);
            for (int i = 0; i < state.keyTimes.Count; i++)
                names.Add(DescribeKey(state, i));

            int current = Mathf.Clamp(keyIndexProp.intValue, 0, names.Count - 1);
            int picked = EditorGUILayout.Popup("Key", current, names.ToArray());
            if (picked != current)
                keyIndexProp.intValue = picked;
        }

        private static string DescribeKey(StateEntry state, int index)
        {
            float time = state.keyTimes[index];
            float frameRate = state.clip != null && state.clip.frameRate > 0f ? state.clip.frameRate : 60f;
            int frame = Mathf.RoundToInt(time * frameRate);
            return $"Key {index + 1} - Frame {frame} ({time:0.###}s)";
        }

        private static StateEntry FindState(List<StateEntry> states, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return states.FirstOrDefault(s => s.name == name);
        }

        private static string Describe(CodeExportPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.Xml))
                return "No blocks yet";

            if (payload.Blocks == null)
                return "Workspace saved";

            int count = payload.Blocks.Count;
            return $"{count} {(count == 1 ? "block" : "blocks")} saved";
        }

        private static List<StateEntry> GetStates(AnimationRuntime comp, int layer)
        {
            List<StateEntry> result = new();

            if (!comp.TryGetComponent<Animator>(out var animator))
                return result;

            RuntimeAnimatorController rac = animator.runtimeAnimatorController;
            AnimatorOverrideController overrides = rac as AnimatorOverrideController;

            if (overrides != null)
                rac = overrides.runtimeAnimatorController;

            if (rac is not AnimatorController controller || layer < 0 || layer >= controller.layers.Length)
                return result;

            Dictionary<AnimationClip, List<float>> keyCache = new();
            CollectStates(controller.layers[layer].stateMachine, overrides, keyCache, result);
            return result;
        }

        private static void CollectStates(AnimatorStateMachine machine, AnimatorOverrideController overrides, Dictionary<AnimationClip, List<float>> keyCache, List<StateEntry> result)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                AnimationClip clip = AnimationRuntime.ResolveClip(child.state.motion as AnimationClip, overrides);
                List<float> keyTimes;

                if (clip == null)
                {
                    keyTimes = new List<float>();
                }
                else if (!keyCache.TryGetValue(clip, out keyTimes))
                {
                    keyTimes = AnimationRuntime.GetKeyTimes(clip);
                    keyCache[clip] = keyTimes;
                }

                result.Add(new StateEntry
                {
                    name = child.state.name,
                    clip = clip,
                    keyTimes = keyTimes
                });
            }

            foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
                CollectStates(sub.stateMachine, overrides, keyCache, result);
        }
    }
}
#endif