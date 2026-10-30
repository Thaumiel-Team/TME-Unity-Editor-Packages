#if UNITY_EDITOR
using Assets.Scripts.Enums;
using Assets.Scripts.Networking.Blocky;
using System.Collections.Generic;

namespace Assets.Scripts.Networking.Blocky.Definitions
{
    public class DoorDefs : DefBase
    {
        private enum Properties
        {
            DoorType,
            Permissions,
            IsOpen,
            IsLocked,
            RequireAllPermissions,
            Bypass2176,
            Name,
            Position,
            Rotation,
            Scale,
            ExactState,
        }

        public override void Register()
        {
            BlocklyServer.RegisterCategory("Door", "#f14f2a", "");

            RegisterFindDoorByName();
            RegisterCreateDoor();
            RegisterSetDoorPerms();
            RegisterOpenDoor();
            RegisterCloseDoor();
            RegisterLockDoor();
            RegisterUnlockDoor();
            RegisterDoorIsOpen();
            RegisterDoorIsClosed();
            RegisterDoorIsOpening();
            RegisterGetProperty();
            RegisterDoorEvents();
        }

        private void RegisterDoorEvents()
        {
            RegisterDoorEvent(
                "door_destroyed_event",
                "Door Destroyed",
                "OnDoorDestroyed",
                "Runs once when this door is destroyed.",
                "Fires when the door breaks. The door variable is the door that was destroyed.");

            RegisterDoorEvent(
                "door_opened_event",
                "Door Opened",
                "OnDoorOpened",
                "Runs when this door opens.",
                "Fires when the door opens. The door variable is the door that opened.");

            RegisterDoorEvent(
                "door_closed_event",
                "Door Closed",
                "OnDoorClosed",
                "Runs when this door closes.",
                "Fires when the door closes. The door variable is the door that closed.");

            RegisterDoorEvent(
                "door_locked_event",
                "Door Locked",
                "OnDoorLocked",
                "Runs when this door is locked.",
                "Fires when the door becomes locked. The door variable is the door that was locked.");

            RegisterDoorEvent(
                "door_damaged_event",
                "Door Damaged",
                "OnDoorDamaged",
                "Runs when this door takes damage.",
                "Fires whenever the door is damaged. The door variable is the door that was damaged.");

            RegisterDoorEvent(
                "door_unlocked_event",
                "Door Unlocked",
                "OnDoorUnlocked",
                "Runs when this door is unlocked.",
                "Fires when the door becomes unlocked. The door variable is the door that was unlocked.");
        }

        private static void RegisterDoorEvent(string id, string label, string method, string tooltip, string explanation)
        {
            BlocklyServer.RegisterEvent(new BlockEvent
            {
                Id = id,
                Label = label,
                Method = method,
                Params = new List<BlockEventParam>
                {
                    BlockEventParam.Var("door", "Door")
                },
                Tooltip = tooltip,
                Explanation = explanation
            });
        }

        private void RegisterGetProperty()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "get_door_property",
                PropertyTypes = new Dictionary<string, string>
                {
                    ["IsOpen"] = "Boolean",
                    ["IsLocked"] = "Boolean",
                    ["RequireAllPermissions"] = "Boolean",
                    ["Bypass2176"] = "Boolean",
                    ["Name"] = "String",
                    ["Position"] = "Vector3",
                    ["Rotation"] = "Quaternion",
                    ["Scale"] = "Vector3",
                },
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Gets a property of the door.",
                Explanation = "Reads one Property (name, state, permissions, and more) from the Door object.",
                Message = "Get %2 of Door: %1",
                Connections = BlockConnectionType.Output,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door"),
                    BlockArg.Dropdown("Property", EnumOptions<Properties>())
                }
            });
        }

        private void RegisterFindDoorByName()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_find_by_name",
                OutputType = "Door",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Finds a door in the schematic by its name. The search is case-insensitive and the result is cached.",
                Explanation = "Name matching ignores upper and lower case, and repeats are fast thanks to caching.\nOutputs nothing when no door has that name.",
                Message = "Find Door  Name: %1",
                Connections = BlockConnectionType.Output,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.TextField("name", "MyDoor")
                }
            });
        }

        private void RegisterCreateDoor()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_create",
                OutputType = "Door",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Creates a new door and spawns it into the schematic.",
                Explanation = "Builds a door of the given type with the given permissions and open/locked state, then spawns it.\nOutputs the new door for wiring into other blocks.",
                Message = "Create Door  Type: %1  Permissions: %2  Open: %3  Locked: %4  Require All Perms: %5  Bypass 2176: %6",
                Connections = BlockConnectionType.Output,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Dropdown("doorType", EnumOptions<DoorType>()),
                    BlockArg.Dropdown("doorPermissionFlags", EnumOptions<DoorPermissionFlags>()),
                    BlockArg.Checkbox("isOpen", false),
                    BlockArg.Checkbox("isLocked", false),
                    BlockArg.Checkbox("requireAllPermissions", false),
                    BlockArg.Checkbox("bypass2176", false)
                }
            });
        }

        private void RegisterSetDoorPerms()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_set_permissions",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Set the keycard permission flags required to interact with this door.",
                Explanation = "Replaces the door access list with perms. Combine flag blocks to require several keycards.",
                Message = "Set Permissions of Door: %1 → %2",
                Connections = BlockConnectionType.Previous | BlockConnectionType.Next,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door"),
                    BlockArg.Dropdown("perms", EnumOptions<DoorPermissionFlags>())
                }
            });
        }

        private void RegisterOpenDoor()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_open",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Opens this door.",
                Explanation = "Starts the opening animation and lets players pass.\nDoes nothing when the door is locked.",
                Message = "Open Door: %1",
                Connections = BlockConnectionType.Previous | BlockConnectionType.Next,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }

        private void RegisterCloseDoor()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_close",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Closes this door.",
                Explanation = "Starts the closing animation and blocks passage again.",
                Message = "Close Door: %1",
                Connections = BlockConnectionType.Previous | BlockConnectionType.Next,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }

        private void RegisterLockDoor()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_lock",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Locks this door, preventing it from being opened.",
                Explanation = "Open attempts fail until Unlock runs.\nPlayers already inside are unaffected.",
                Message = "Lock Door: %1",
                Connections = BlockConnectionType.Previous | BlockConnectionType.Next,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }

        private void RegisterUnlockDoor()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_unlock",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Unlocks this door, allowing it to be opened.",
                Explanation = "Removes the lock so the door can be opened again.",
                Message = "Unlock Door: %1",
                Connections = BlockConnectionType.Previous | BlockConnectionType.Next,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }

        private void RegisterDoorIsOpen()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_is_open",
                OutputType = "Boolean",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Returns true if the door is currently open.",
                Explanation = "Checks the live state. Use it to avoid re-opening an open door.",
                Message = "Is Door Open: %1",
                Connections = BlockConnectionType.Output,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }

        private void RegisterDoorIsClosed()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_is_closed",
                OutputType = "Boolean",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Returns true if the door is currently closed (not open).",
                Explanation = "True whenever the door is not open.\nPair with Is Opening to detect the moving state.",
                Message = "Is Door Closed: %1",
                Connections = BlockConnectionType.Output,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }

        private void RegisterDoorIsOpening()
        {
            BlocklyServer.RegisterBlock(new BlockDefinition
            {
                Id = "door_is_opening",
                OutputType = "Boolean",
                Category = "Door",
                Color = "#f14f2a",
                Tooltip = "Returns true while the door is animating between open and closed (opening or closing).",
                Explanation = "True during the whole animation in either direction.\nUse it to wait until movement finishes.",
                Message = "Is Door Opening: %1",
                Connections = BlockConnectionType.Output,
                Args = new List<Dictionary<string, object>>
                {
                    BlockArg.Value("Door")
                }
            });
        }
    }
}
#endif