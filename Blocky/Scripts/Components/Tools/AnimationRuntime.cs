#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Enums;
using Assets.Scripts.Yaml;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Serialization;
using Assets.Scripts.Networking.Blocky;

namespace Assets.Scripts.Components.Tools
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]

    public class AnimationRuntime : ToolBase
    {
        public const string EventId = "animator_time_event";

        public const float KeyTimeTolerance = 0.0001f;

        [Serializable]
        public class KeyedBlocks
        {
            [Tooltip("Animator state name.")]
            public string stateName = "";

            [Tooltip("Zero based index of the animation key in the state's clip. -1 resolves it from the key time.")]
            public int keyIndex = -1;

            [Tooltip("Time of the animation key in seconds. Refreshed from the clip when edited or exported.")]
            [FormerlySerializedAs("time")]
            public float keyTime;

            [Tooltip("Blocky workspace that runs when the animation reaches this key.")]
            public CodeExportPayload payload = new();

            [SerializeField, HideInInspector]
            private bool keyResolved;

            internal bool KeyResolved
            {
                get => keyResolved;
                set => keyResolved = value;
            }
        }

        [Tooltip("Animator layer to watch.")]
        public int layer;

        public List<KeyedBlocks> entries = new();

        [NonSerialized]
        public int EditingIndex = -1;

        public event Action<KeyedBlocks> KeyReached;

        public static event Action<AnimationRuntime, KeyedBlocks> AnyKeyReached;

        private Animator _animator;
        private int _lastStateHash;
        private float _lastNormalized;

        public override ToolType ToolType => ToolType.AnimationRuntime;

        public override void Compile()
        {
            RefreshKeyTimes();

            Properties = new()
            {
                ["Layer"] = layer,
                ["Entries"] = entries,
            };
        }

        public override void Decompile()
        {
            if (Properties.TryGetValue("Layer", out var layerValue) && int.TryParse(layerValue?.ToString(), out int parsedLayer))
                layer = parsedLayer;

            if (Properties.TryGetValue("Entries", out var entriesValue))
                entries = ParseEntries(entriesValue);

            RefreshKeyTimes();
        }

        public override void OnBlocklyExportReceived(CodeExportPayload payload, string targetEvent)
        {
            if (targetEvent != EventId)
            {
                base.OnBlocklyExportReceived(payload, targetEvent);
                return;
            }

            if (payload == null || EditingIndex < 0 || EditingIndex >= entries.Count)
                return;

            entries[EditingIndex].payload = payload;
            Debug.Log($"[AnimationRuntime] Successfully added export to {gameObject.name}'s entry {EditingIndex}.");

            EditorUtility.SetDirty(this);
        }

        private void Awake() =>
            _animator = GetComponent<Animator>();


        private void OnEnable() => 
            ResetTracking();

        private void ResetTracking()
        {
            _lastStateHash = 0;
            _lastNormalized = -KeyTimeTolerance;
        }

        private void Update()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || entries.Count == 0)
                return;

            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(layer);
            bool stateChanged = info.fullPathHash != _lastStateHash;
            float current = info.normalizedTime;

            if (current < 0f)
            {
                current = 0f;
            }
            else if (info.loop)
                current = Mathf.Repeat(current, 1f);

            if (stateChanged)
            {
                _lastStateHash = info.fullPathHash;
                _lastNormalized = current > 0f ? current : -KeyTimeTolerance;
            }

            float length = Mathf.Max(info.length, KeyTimeTolerance);

            for (int i = 0; i < entries.Count; i++)
            {
                KeyedBlocks entry = entries[i];
                if (entry == null || !StateMatches(entry, info))
                    continue;

                float target = Mathf.Clamp01(entry.keyTime / length);
                bool justEntered = stateChanged && target <= KeyTimeTolerance;

                if (justEntered || Crossed(_lastNormalized, current, target, info.loop))
                {
                    KeyReached?.Invoke(entry);
                    AnyKeyReached?.Invoke(this, entry);
                }
            }

            _lastNormalized = current;
        }

        public static List<float> GetKeyTimes(AnimationClip clip)
        {
            List<float> result = new();
            if (clip == null)
                return result;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;

                foreach (Keyframe key in curve.keys)
                {
                    if (key.time < -KeyTimeTolerance || key.time > clip.length + KeyTimeTolerance)
                        continue;

                    AddKeyTime(result, Mathf.Clamp(key.time, 0f, clip.length));
                }
            }

            result.Sort();
            return result;
        }

        public static int FindClosestKeyIndex(IReadOnlyList<float> keyTimes, float time)
        {
            if (keyTimes == null || keyTimes.Count == 0)
                return 0;

            int best = 0;
            float bestDistance = Mathf.Abs(keyTimes[0] - time);

            for (int i = 1; i < keyTimes.Count; i++)
            {
                float distance = Mathf.Abs(keyTimes[i] - time);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static AnimationClip GetStateClip(Animator animator, int layer, string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
                return null;

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            AnimatorOverrideController overrides = controller as AnimatorOverrideController;
            if (overrides != null)
                controller = overrides.runtimeAnimatorController;

            if (controller is not AnimatorController animatorController || layer < 0 || layer >= animatorController.layers.Length)
                return null;

            AnimationClip clip = FindStateClip(animatorController.layers[layer].stateMachine, stateName);
            return ResolveClip(clip, overrides);
        }

        public static AnimationClip ResolveClip(AnimationClip clip, AnimatorOverrideController overrides)
        {
            if (clip == null || overrides == null || overrides.runtimeAnimatorController == null)
                return clip;

            AnimationClip overrideClip = overrides[clip];
            return overrideClip != null ? overrideClip : clip;
        }

        public void RefreshKeyTimes()
        {
            if (entries == null)
                return;

            Animator animator = _animator != null ? _animator : GetComponent<Animator>();
            if (animator == null)
                return;

            foreach (KeyedBlocks entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.stateName))
                    continue;

                AnimationClip clip = GetStateClip(animator, layer, entry.stateName);
                if (clip == null)
                    continue;

                List<float> keyTimes = GetKeyTimes(clip);
                if (keyTimes.Count == 0)
                {
                    entry.keyIndex = 0;
                    entry.keyTime = 0f;
                    entry.KeyResolved = false;
                    continue;
                }

                if (!entry.KeyResolved || entry.keyIndex < 0 || entry.keyIndex >= keyTimes.Count)
                    entry.keyIndex = FindClosestKeyIndex(keyTimes, entry.keyTime);

                entry.keyTime = keyTimes[entry.keyIndex];
                entry.KeyResolved = true;
            }
        }

        private static void AddKeyTime(List<float> times, float time)
        {
            foreach (float existing in times)
            {
                if (Mathf.Abs(existing - time) <= KeyTimeTolerance)
                    return;
            }

            times.Add(time);
        }

        private static AnimationClip FindStateClip(AnimatorStateMachine machine, string stateName)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == stateName)
                    return child.state.motion as AnimationClip;
            }

            foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            {
                AnimationClip found = FindStateClip(sub.stateMachine, stateName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static List<KeyedBlocks> ParseEntries(object rawEntries)
        {
            List<KeyedBlocks> result = new();
            if (rawEntries is not IEnumerable rawList)
                return result;

            foreach (object rawEntry in rawList)
            {
                if (rawEntry is KeyedBlocks typed)
                {
                    result.Add(typed);
                    continue;
                }

                if (rawEntry is not IDictionary dict)
                    continue;

                try
                {
                    object keyIndexValue = GetValue(dict, "keyIndex", "KeyIndex");
                    object keyTimeValue = GetValue(dict, "keyTime", "KeyTime", "time", "Time");

                    KeyedBlocks entry = new()
                    {
                        stateName = GetValue(dict, "stateName", "StateName")?.ToString() ?? "",
                        keyIndex = keyIndexValue != null ? Convert.ToInt32(keyIndexValue) : -1,
                        keyTime = keyTimeValue != null ? Convert.ToSingle(keyTimeValue) : 0f,
                        KeyResolved = keyIndexValue != null && keyTimeValue != null,
                    };

                    object payloadValue = GetValue(dict, "payload", "Payload");
                    if (payloadValue is IDictionary)
                        entry.payload = YamlHelpers.ParseObject<CodeExportPayload>(payloadValue);

                    result.Add(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AnimationRuntime] Failed to parse animation key entry: {ex.Message}");
                }
            }

            return result;
        }

        private static object GetValue(IDictionary dict, params string[] names)
        {
            foreach (string name in names)
            {
                if (dict.Contains(name))
                    return dict[name];
            }

            foreach (object key in dict.Keys)
            {
                string keyName = key?.ToString();

                foreach (string name in names)
                {
                    if (string.Equals(keyName, name, StringComparison.OrdinalIgnoreCase))
                        return dict[key];
                }
            }

            return null;
        }

        private static bool StateMatches(KeyedBlocks entry, AnimatorStateInfo info) =>
            string.IsNullOrEmpty(entry.stateName) || info.IsName(entry.stateName);

        private static bool Crossed(float previous, float current, float target, bool loop)
        {
            if (!loop)
                return previous < target && current >= target;

            if (current < previous && previous > 0.5f && current < 0.5f)
                return target > previous || target <= current;

            return previous < target && current >= target;
        }
    }
}
#endif