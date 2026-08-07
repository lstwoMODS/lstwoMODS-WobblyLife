using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    /// <summary>
    /// The outcome of a spawn, restore or destroy. Without this a failed spawn only reaches the
    /// BepInEx log and the panel shows nothing at all, which is how the prop spawner behaved
    /// before groups existed.
    /// </summary>
    public class PropSpawnStatusMessage
    {
        public const string MessageType = "WobblyLife.PropSpawnStatus";

        public string? GroupId { get; set; }

        public int Spawned { get; set; }
        public int Failed  { get; set; }

        /// <summary>One line per prop that did not make it, naming the prop and the reason.</summary>
        public string[]? Errors { get; set; }

        /// <summary>Single-line headline for the panel.</summary>
        public string? Summary { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static PropSpawnStatusMessage? Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<PropSpawnStatusMessage>(msg.Payload);
    }
}
