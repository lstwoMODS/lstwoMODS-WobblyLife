using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    public class SpawnPropMessage
    {
        public const string MessageType = "WobblyLife.SpawnProp";

        public string Address  { get; set; }
        public bool   Networked { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static SpawnPropMessage Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<SpawnPropMessage>(msg.Payload);
    }
}
