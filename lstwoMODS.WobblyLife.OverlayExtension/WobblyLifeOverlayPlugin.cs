using lstwoMODS_Overlay;
using lstwoMODS.WobblyLife.SharedObjects;
using Newtonsoft.Json;

namespace lstwoMODS.WobblyLife.OverlayExtension;

public class WobblyLifeOverlayPlugin : OverlayPluginBase
{
    public override string Id => "net.lstwo.lstwoMODS.WobblyLife";

    internal static IOverlayContext Context { get; private set; }
    internal static readonly PropSpawnerState PropSpawner = new();

    public override void Initialize(IOverlayContext ctx)
    {
        Context = ctx;

        ctx.RegisterRenderer<PropSpawnerData, PropSpawnerRenderer>();

        ctx.RegisterMessageHandler(PropDatabaseReadyMessage.MessageType, msg =>
        {
            var ready = PropDatabaseReadyMessage.Deserialize(msg);
            if (ready?.CachePath == null) return;

            PropSpawner.LoadDatabase(ready.CachePath);

            // The database swap invalidates the spawned props' library back-references, and this
            // is also the point at which an overlay that started late needs the live picture.
            SendCommand(new PropGroupCommandMessage { Command = PropGroupCommand.Refresh });
        });

        // Both handlers run on the IPC reader thread: queue only, never touch the collections the
        // renderer walks.
        ctx.RegisterMessageHandler(PropGroupsStateMessage.MessageType, msg =>
        {
            var snapshot = PropGroupsStateMessage.Deserialize(msg);
            if (snapshot != null) PropSpawner.QueueSnapshot(snapshot);
        });

        ctx.RegisterMessageHandler(PropSpawnStatusMessage.MessageType, msg =>
        {
            var status = PropSpawnStatusMessage.Deserialize(msg);
            if (status != null) PropSpawner.QueueStatus(status);
        });

        ctx.RegisterMessageHandler(CustomItemsReadyMessage.MessageType, msg =>
        {
            var ready = CustomItemsReadyMessage.Deserialize(msg);
            if (ready?.Packs != null)
                PropSpawner.LoadCustomItems(ready.Packs);
        });
    }

    internal static void SendCommand(PropGroupCommandMessage command)
        => Context.SendToMod(JsonConvert.SerializeObject(command.Serialize()));
}
