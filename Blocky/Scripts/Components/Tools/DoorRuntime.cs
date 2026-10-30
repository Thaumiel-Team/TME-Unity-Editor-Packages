#if UNITY_EDITOR
using Assets.Scripts.Components.Objects;
using Assets.Scripts.Enums;
using Assets.Scripts.Yaml;
using UnityEditor;
using UnityEngine;
using Assets.Scripts.Networking.Blocky;

namespace Assets.Scripts.Components.Tools
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DoorObject))]
    public class DoorRuntime : ToolBase
    {
#if TME_BLOCKY
        [Header("Door Events")]
        [Tooltip("Blocky workspace that runs when this door is destroyed.")]
        public CodeExportPayload OnDoorDestroyed;

        [Tooltip("Blocky workspace that runs when this door is opened.")]
        public CodeExportPayload OnDoorOpened;

        [Tooltip("Blocky workspace that runs when this door is closed.")]
        public CodeExportPayload OnDoorClosed;

        [Tooltip("Blocky workspace that runs when this door is locked.")]
        public CodeExportPayload OnDoorLocked;

        [Tooltip("Blocky workspace that runs when this door is damaged.")]
        public CodeExportPayload OnDoorDamaged;

        [Tooltip("Blocky workspace that runs when this door is unlocked.")]
        public CodeExportPayload OnDoorUnlocked;

        public override ToolType ToolType => ToolType.DoorRuntime;

        public override void Compile()
        {
            Properties = new()
            {
                ["OnDoorDestroyed"] = OnDoorDestroyed,
                ["OnDoorOpened"] = OnDoorOpened,
                ["OnDoorClosed"] = OnDoorClosed,
                ["OnDoorLocked"] = OnDoorLocked,
                ["OnDoorDamaged"] = OnDoorDamaged,
                ["OnDoorUnlocked"] = OnDoorUnlocked,
            };
        }

        public override void Decompile()
        {
            if (Properties.TryGetValue("OnDoorDestroyed", out var destroyed))
                OnDoorDestroyed = YamlHelpers.ParseObject<CodeExportPayload>(destroyed);

            if (Properties.TryGetValue("OnDoorOpened", out var opened))
                OnDoorOpened = YamlHelpers.ParseObject<CodeExportPayload>(opened);

            if (Properties.TryGetValue("OnDoorClosed", out var closed))
                OnDoorClosed = YamlHelpers.ParseObject<CodeExportPayload>(closed);

            if (Properties.TryGetValue("OnDoorLocked", out var locked))
                OnDoorLocked = YamlHelpers.ParseObject<CodeExportPayload>(locked);

            if (Properties.TryGetValue("OnDoorDamaged", out var damaged))
                OnDoorDamaged = YamlHelpers.ParseObject<CodeExportPayload>(damaged);

            if (Properties.TryGetValue("OnDoorUnlocked", out var unlocked))
                OnDoorUnlocked = YamlHelpers.ParseObject<CodeExportPayload>(unlocked);
        }

        public override void OnBlocklyExportReceived(CodeExportPayload payload, string targetEvent)
        {
            if (targetEvent == nameof(OnDoorDestroyed))
            {
                OnDoorDestroyed = payload;
                Debug.Log($"[DoorRuntime] Successfully added export to {gameObject.name}'s OnDoorDestroyed property.");
            }
            else if (targetEvent == nameof(OnDoorOpened))
            {
                OnDoorOpened = payload;
                Debug.Log($"[DoorRuntime] Successfully added export to {gameObject.name}'s OnDoorOpened property.");
            }
            else if (targetEvent == nameof(OnDoorClosed))
            {
                OnDoorClosed = payload;
                Debug.Log($"[DoorRuntime] Successfully added export to {gameObject.name}'s OnDoorClosed property.");
            }
            else if (targetEvent == nameof(OnDoorLocked))
            {
                OnDoorLocked = payload;
                Debug.Log($"[DoorRuntime] Successfully added export to {gameObject.name}'s OnDoorLocked property.");
            }
            else if (targetEvent == nameof(OnDoorDamaged))
            {
                OnDoorDamaged = payload;
                Debug.Log($"[DoorRuntime] Successfully added export to {gameObject.name}'s OnDoorDamaged property.");
            }
            else if (targetEvent == nameof(OnDoorUnlocked))
            {
                OnDoorUnlocked = payload;
                Debug.Log($"[DoorRuntime] Successfully added export to {gameObject.name}'s OnDoorUnlocked property.");
            }

            EditorUtility.SetDirty(this);
        }
#endif
    }
}

#endif