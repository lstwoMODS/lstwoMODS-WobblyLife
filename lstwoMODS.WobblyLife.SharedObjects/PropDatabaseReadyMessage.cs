using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    public class PropDatabaseReadyMessage
    {
        public const string MessageType = "WobblyLife.PropDatabaseReady";

        public string CachePath { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static PropDatabaseReadyMessage Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<PropDatabaseReadyMessage>(msg.Payload);
    }
}
