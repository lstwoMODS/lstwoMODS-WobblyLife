using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PropGroupCommand
    {
        /// <summary>Resend the snapshot. Used on overlay attach and by the manual refresh button,
        /// which also re-scans the folder so a shared file dropped in gets picked up.</summary>
        Refresh,

        /// <summary>Uses Name. When SpawnIds is set, the new group also takes those props — which
        /// is how "Move to -> New Group..." works without the overlay inventing an id.</summary>
        CreateGroup,

        RenameGroup,

        /// <summary>Uses DeleteFile. Live members of the group become ungrouped either way.</summary>
        DeleteGroup,

        /// <summary>GroupId null means ungrouped.</summary>
        SetActiveGroup,

        /// <summary>Capture the group's live props into its file.</summary>
        SaveGroup,

        /// <summary>Uses LoadMode and ReplaceExisting.</summary>
        LoadGroup,

        /// <summary>Destroy the group's live props. The group and its file are kept.</summary>
        DestroyGroup,

        /// <summary>Destroy every live prop, in every group and ungrouped.</summary>
        DestroyAll,

        /// <summary>Move SpawnIds into GroupId; null GroupId means ungrouped.</summary>
        AssignProps,

        RevealGroupFile,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PropGroupLoadMode
    {
        /// <summary>Exactly where the layout was authored.</summary>
        AtSavedCoords,

        /// <summary>Translated and yaw-rotated so it appears in front of the player.</summary>
        RebaseToPlayer,
    }

    /// <summary>
    /// One envelope for every group-scoped verb. The codebase already works this way — the
    /// existing SpawnedPropActionMessage is a verb string plus a target — and the mod dispatches
    /// on message type with a hand-written if/else chain that would otherwise grow a branch per
    /// command.
    /// </summary>
    public class PropGroupCommandMessage
    {
        public const string MessageType = "WobblyLife.PropGroupCommand";

        public PropGroupCommand Command { get; set; }

        public string? GroupId { get; set; }
        public string? Name    { get; set; }

        public int[]? SpawnIds { get; set; }

        public PropGroupLoadMode LoadMode        { get; set; }
        public bool              ReplaceExisting { get; set; }
        public bool              DeleteFile      { get; set; }
        public bool              MakeActive      { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static PropGroupCommandMessage? Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<PropGroupCommandMessage>(msg.Payload);
    }
}
