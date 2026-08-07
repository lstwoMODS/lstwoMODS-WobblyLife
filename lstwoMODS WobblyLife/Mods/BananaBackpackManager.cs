using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;

namespace lstwoMODS_WobblyLife.Mods;

public class BananaBackpackManager : BaseMod
{
    public override string Name => "Banana Peel Backpack Modifier";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;

    [ModSetting] public static Ref<int> MaxBananaPeels = new(5);
    [ModSetting] public static Ref<bool> UnlimitedPeels = new();

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.BananaBackpack").PatchAll(typeof(Patches));
    }

    public class Patches
    {
        [HarmonyPatch(typeof(ClothingBananaBackpack), "OnServerBananaPeelSpawned")]
        [HarmonyPrefix]
        public static bool OnServerBananaPeelSpawnedPatch(ref ClothingBananaBackpack __instance, ref HawkNetworkBehaviour networkBehaviour)
        {
            networkBehaviour.onDestroy.AddCallback(__instance.OnBananaPeelDestroyed);
            __instance.spawnedBananaPeels.Add(networkBehaviour);
            
            if (__instance.spawnedBananaPeels.Count > MaxBananaPeels.Value && !UnlimitedPeels.Value)
            {
                var hawkNetworkBehaviour = __instance.spawnedBananaPeels[0];
                
                if (hawkNetworkBehaviour)
                {
                    VanishComponent.VanishAndDestroy(hawkNetworkBehaviour.gameObject);
                }
                
                __instance.spawnedBananaPeels.RemoveAt(0);
            }

            return false;
        }
    }
}