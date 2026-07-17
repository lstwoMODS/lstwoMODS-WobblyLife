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
