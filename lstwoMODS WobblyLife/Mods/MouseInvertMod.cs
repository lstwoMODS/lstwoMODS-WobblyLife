using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class MouseInvertMod : BaseMod
{
    public override string Name => "Mouse Invert X";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;
    
    [ModSetting] public static Ref<bool> InvertMouseX = new();

    protected override void OnStaticInit()
    {
        new Harmony(GetType().FullName).PatchAll(typeof(MouseInvertMod));
    }
    
    [HarmonyPatch(typeof(GameplayCamera), nameof(GameplayCamera.GetAxisDeltaX))]
    [HarmonyPostfix]
    public static void GetAxisDeltaX(ref float __result)
    {
        if(InvertMouseX.Value)
            __result = -__result;
    }
}