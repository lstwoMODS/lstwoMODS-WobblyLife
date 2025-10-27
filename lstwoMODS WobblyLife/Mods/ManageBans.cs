using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using Steamworks;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class ManageBans : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        unbanLDB = ui.CreateLDBTrio("Unban Player", "net.lstwo.managebans.unbanldb");
        unbanLDB.Button.OnClick = () =>
        {
            
        };

        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Unban Players";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ServerHacksTab;

    private HacksUIHelper.LDBTrio unbanLDB;
}
