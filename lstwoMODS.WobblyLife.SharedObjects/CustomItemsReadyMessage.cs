using System.Collections.Generic;
using lstwoMODS.ImGui.Shared;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.SharedObjects
{
    public class CustomItemsReadyMessage
    {
        public const string MessageType = "WobblyLife.CustomItemsReady";

        public List<CustomItemPackData>? Packs { get; set; }

        public IpcMessage Serialize() => new IpcMessage
        {
            Type    = MessageType,
            Payload = JsonConvert.SerializeObject(this)
        };

        public static CustomItemsReadyMessage? Deserialize(IpcMessage msg)
            => JsonConvert.DeserializeObject<CustomItemsReadyMessage>(msg.Payload);
    }

    public class CustomItemPackData
    {
        public string? PackName   { get; set; }
        public string? PackAuthor { get; set; }
        public List<CustomItemData> Items { get; set; } = new List<CustomItemData>();
    }

    public class CustomItemData
    {
        public string? Name        { get; set; }
        public string? Description { get; set; }
    }
}
