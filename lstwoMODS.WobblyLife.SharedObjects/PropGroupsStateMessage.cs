using System.Collections.Generic;
using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    /// <summary>
    /// The complete prop-group and live-prop picture, pushed from the mod on every change.
    ///
    /// This is a snapshot rather than a stream of deltas on purpose. Props die for reasons the mod
    /// never initiates (network despawn, scene change, the user deleting one in UnityExplorer), a
    /// single group action changes many props at once, and the overlay can restart mid-session and
    /// needs to resync. A snapshot is idempotent, so all three cases are the same case.
    /// </summary>
    public class PropGroupsStateMessage
    {
        public const string MessageType = "WobblyLife.PropGroupsState";

        /// <summary>Monotonic. The overlay ignores anything not newer than what it has.</summary>
        public int Revision { get; set; }

        /// <summary>Group new spawns land in. null means ungrouped.</summary>
        public string? ActiveGroupId { get; set; }

        public List<PropGroupData> Groups { get; set; } = new List<PropGroupData>();

        /// <summary>Every live prop, grouped and ungrouped alike, ascending by SpawnId.</summary>
        public List<SpawnedPropData> Spawned { get; set; } = new List<SpawnedPropData>();

        /// <summary>Set when <see cref="Spawned"/> was truncated, so the UI can say so.</summary>
        public int Truncated { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static PropGroupsStateMessage? Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<PropGroupsStateMessage>(msg.Payload);
    }

    public class PropGroupData
    {
        public string Id   { get; set; } = "";
        public string Name { get; set; } = "";

        public int LiveCount  { get; set; }
        public int SavedCount { get; set; }

        /// <summary>
        /// True when the live props differ from the saved file — including props merely having been
        /// moved. The mod recomputes this once a second rather than per frame, which is what makes
        /// a transform-aware comparison affordable. Do not "optimise" it into a membership-only
        /// flag: noticing that you dragged something in UnityExplorer is the point.
        /// </summary>
        public bool HasUnsavedChanges { get; set; }

        /// <summary>False until the group has been saved at least once.</summary>
        public bool HasFile { get; set; }

        /// <summary>Unix seconds; 0 when never saved.</summary>
        public long SavedAtUnix { get; set; }

        /// <summary>For display only. Revealing it is a mod-side command, because resolving the
        /// path also flushes pending writes.</summary>
        public string? FilePath { get; set; }
    }

    public class SpawnedPropData
    {
        public int     SpawnId   { get; set; }
        public string? Address   { get; set; }
        public string? Name      { get; set; }
        public bool    Networked { get; set; }

        /// <summary>null means ungrouped.</summary>
        public string? GroupId { get; set; }
    }
}
