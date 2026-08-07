using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Mods.ESP;

namespace lstwoMODS_WobblyLife.Mods;

public class PlayerESPMod : BaseMod
{
    public override string Name => "Player ESP";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    [ModSetting(Order = 10)]
    public bool EnablePlayerESP
    {
        get => ESPManager.playerTracker.draw;
        set => ESPManager.playerTracker.draw = value;
    }

    [ModSetting(Order = 20)]
    public bool DrawLines
    {
        get => ESPManager.playerTracker.drawLines;
        set => ESPManager.playerTracker.drawLines = value;
    }

    [ModSetting(Order = 30)]
    public bool DrawBoxes
    {
        get => ESPManager.playerTracker.drawBoxes;
        set => ESPManager.playerTracker.drawBoxes = value;
    }

    [ModSetting(Order = 40)]
    public bool DrawText
    {
        get => ESPManager.playerTracker.drawText;
        set => ESPManager.playerTracker.drawText = value;
    }

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.PlayerESPMod").PatchAll(typeof(Patches));
    }

    public class Patches
    {
        [HarmonyPatch(typeof(PlayerBody), "Awake")]
        [HarmonyPostfix]
        private static void PlayerBodyAwakePatch(ref PlayerBody __instance)
        {
            ESPManager.playerTracker.AddTrackedObject(__instance);
        }
    }
}