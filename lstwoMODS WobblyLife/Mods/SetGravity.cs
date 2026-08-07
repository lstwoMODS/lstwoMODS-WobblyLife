using UnityEngine;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class SetGravity : BaseMod
{
    public override string Name => "Set Gravity";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    [ModSetting(Order = 10)]
    public static Vector3 Gravity
    {
        get => Physics.gravity;
        set => Physics.gravity = value;
    }

    [ModAction(Order = 20)]
    public static void ResetGravity()
    {
        Physics.gravity = new(0, -19.62f, 0);
    }
}