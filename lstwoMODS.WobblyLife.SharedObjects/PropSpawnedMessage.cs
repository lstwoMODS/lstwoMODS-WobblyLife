using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    public class PropSpawnedMessage
    {
        public const string MessageType = "WobblyLife.PropSpawned";

        public int    SpawnId   { get; set; }
        public string Address   { get; set; }
        public string Name      { get; set; }
        public bool   Networked { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static PropSpawnedMessage Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<PropSpawnedMessage>(msg.Payload);
    }
}
