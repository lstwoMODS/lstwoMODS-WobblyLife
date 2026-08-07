
using System;
using System.Linq;
using System.Reflection;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using Object = UnityEngine.Object;

namespace lstwoMODS_WobblyLife.Mods;

public class TeleportToWardrobe : PlayerBasedMod
{
    public override string Name => "Teleport to Wardrobe";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    [ModAction(Label = "Teleport Player to Nearest Wardrobe")]
    public void Teleport()
    {
        var pos = Player.Body.transform.position;

        Func<Wardrobe, float> sort = (x) => Vector3.Distance(x.gameObject.transform.position, pos);
        var wardrobe = Object.FindObjectsOfType<Wardrobe>().OrderBy(sort).FirstOrDefault();

        if (wardrobe == null || Player.ControllerInteractor.HasEnteredAction())
        {
            return;
        }
        
        var action = wardrobe.actionEnterExitInteract;
        action.RequestEnter(Player.Controller);
    }
}