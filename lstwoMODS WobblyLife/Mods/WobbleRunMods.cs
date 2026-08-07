using System.Collections;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using ModWobblyLife;

namespace lstwoMODS_WobblyLife.Mods;

public class WobbleRunMods : BaseMod
{
    public override string Name => "Wobble Run Mods";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    [ModSetting] public static Ref<bool> SkipIntro = new();

    protected override void OnStaticInit()
    {
        base.OnStaticInit();

        new Harmony(Id).PatchAll(typeof(Patches));
    }

    public static class Patches
    {
        [HarmonyPatch(typeof(WobbleRunGamemode), "Intro")]
        [HarmonyPrefix]
        public static bool SkipIntroCoroutine(WobbleRunGamemode __instance, ref IEnumerator __result)
        {
            if (!SkipIntro.Value)
            {
                return true;
            }

            __result = SkipIntroRoutine(__instance);
            return false;
        }

        private static IEnumerator SkipIntroRoutine(WobbleRunGamemode gamemode)
        {
            ModInstance.Instance.IterateModPlayerControllers(x =>
            {
                gamemode.SpawnPlayerCharacter(x, null, false);
            });
            
            yield return null;
            gamemode.SetGamemodeState(WobbleRunGamemode.WobbleRunGamemodeState.Countdown);
        }
    }
}
