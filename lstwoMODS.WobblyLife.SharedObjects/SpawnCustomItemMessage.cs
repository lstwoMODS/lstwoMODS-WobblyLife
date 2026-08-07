using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    public class SpawnCustomItemMessage
    {
        public const string MessageType = "WobblyLife.SpawnCustomItem";

        public int PackIndex { get; set; }
        public int ItemIndex { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static SpawnCustomItemMessage Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<SpawnCustomItemMessage>(msg.Payload);
    }
}
