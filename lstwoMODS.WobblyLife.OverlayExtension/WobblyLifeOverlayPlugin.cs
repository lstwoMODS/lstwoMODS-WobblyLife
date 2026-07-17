using lstwoMODS_Overlay;
using lstwoMODS.WobblyLife.SharedObjects;

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
            if (ready?.CachePath != null)
                PropSpawner.LoadDatabase(ready.CachePath);
        });

        ctx.RegisterMessageHandler(PropSpawnedMessage.MessageType, msg =>
        {
            var spawned = PropSpawnedMessage.Deserialize(msg);
            if (spawned != null)
                PropSpawner.AddSpawned(spawned);
        });

        ctx.RegisterMessageHandler(CustomItemsReadyMessage.MessageType, msg =>
        {
            var ready = CustomItemsReadyMessage.Deserialize(msg);
            if (ready?.Packs != null)
                PropSpawner.LoadCustomItems(ready.Packs);
        });
    }
}
