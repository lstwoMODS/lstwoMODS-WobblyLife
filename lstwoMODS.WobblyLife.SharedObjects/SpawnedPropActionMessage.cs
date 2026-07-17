using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    public class SpawnedPropActionMessage
    {
        public const string MessageType = "WobblyLife.SpawnedPropAction";

        public int    SpawnId { get; set; }
        /// <summary>"Delete" or "Inspect"</summary>
        public string Action  { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static SpawnedPropActionMessage Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<SpawnedPropActionMessage>(msg.Payload);
    }
}
