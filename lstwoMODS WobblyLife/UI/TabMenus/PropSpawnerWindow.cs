using System;
using System.Collections.Generic;
using System.IO;
using HawkNetworking;
using lstwoMODS.WobblyLife.SharedObjects;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.UI.Elements;
using Newtonsoft.Json;

namespace lstwoMODS_WobblyLife.UI.TabMenus;

public class PropSpawnerWindow : BaseWindow
{
    private static readonly HashSet<string> _networkTypes = BuildNetworkTypes();

    /// <summary>The database hand-off is a one-shot event, but a restarted overlay is a blank
    /// process that never saw it. Remembering that it was sent lets it be replayed.</summary>
    private bool _sentDatabaseReady;

    private static string NetworkTypesPath =>
        Path.Combine(Path.GetDirectoryName(AssetDatabase.CachePath)!, "network_types.json");

    public PropSpawnerWindow()
    {
        Name = "Prop Spawner";
        TitleIcon = Lucide.Package;
        AssetDatabase.OnReady += OnDatabaseReady;
    }

    public override Group ConstructUI() => new("PropSpawnerRoot", new PropSpawnerElement("prop-spawner-native"));

    public override void RefreshUI() { }

    private void OnDatabaseReady()
    {
        File.WriteAllText(NetworkTypesPath, JsonConvert.SerializeObject(_networkTypes));
        SendDatabaseReady();
    }

    /// <summary>
    /// Tells the overlay where the asset cache is. Also called after an overlay restart: without
    /// it the fresh process has no library and the panel sits on "Waiting for asset database..."
    /// for the rest of the session. No-op until the database has been ready once.
    /// </summary>
    public void SendDatabaseReady()
    {
        if (UIManager.IpcChannel == null) return;
        if (!_sentDatabaseReady && !AssetDatabase.IsInitialized) return;

        _sentDatabaseReady = true;
        UIManager.IpcChannel.SendMessage(new PropDatabaseReadyMessage
        {
            CachePath = AssetDatabase.CachePath
        }.Serialize());
    }

    private static HashSet<string> BuildNetworkTypes()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "HawkNetworkBehaviour" };
        try
        {
            var hawkType = typeof(HawkNetworkBehaviour);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                try {
                    foreach (var t in asm.GetTypes())
                        if (t.IsSubclassOf(hawkType)) set.Add(t.FullName);
                } catch { }
        }
        catch { }
        return set;
    }
}
