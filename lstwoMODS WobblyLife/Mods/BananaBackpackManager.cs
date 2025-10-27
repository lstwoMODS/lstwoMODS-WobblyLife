using HarmonyLib;
using HawkNetworking;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks;

public class BananaBackpackManager : BaseHack
{
    public override string Name => "Banana Peel Backpack Modifier";

    public override string Description => "";

    public override HacksTab HacksTab => Plugin.ExtraHacksTab;

    private static int maxBananaPeels = 5;
    private static bool unlimitedPeels = false;

    private HacksUIHelper.LIBTrio maxBananasLib;

    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.BananaBackpack").PatchAll(typeof(Patches));

        var ui = new HacksUIHelper(root);

        ui.AddSpacer(6);

        maxBananasLib = ui.CreateLIBTrio("Max Banana Peels", "lstwo.BananaBackpackManager.MaxBananaPeelsLIB");
        maxBananasLib.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        maxBananasLib.Button.OnClick = () =>
        {
            maxBananaPeels = int.Parse(maxBananasLib.Input.Text);
        };

        ui.AddSpacer(6);

        ui.CreateToggle("lstwo.BananaBackpackManager.UnlimitedBananaPeels", "Unlimited Banana Peels", (b) => unlimitedPeels = b);

        ui.AddSpacer(6);
    }

    public override void RefreshUI()
    {
        maxBananasLib.Input.Text = maxBananaPeels.ToString();
    }

    public override void Update()
    {
    }

    public class Patches
    {
        [HarmonyPatch(typeof(ClothingBananaBackpack), "OnServerBananaPeelSpawned")]
        [HarmonyPrefix]
        public static bool OnServerBananaPeelSpawnedPatch(ref ClothingBananaBackpack __instance, ref HawkNetworkBehaviour networkBehaviour)
        {
            var reflect = new QuickReflection<ClothingBananaBackpack>(__instance, Plugin.Flags);
            var spawnedBananaPeels = (List<HawkNetworkBehaviour>)reflect.GetField("spawnedBananaPeels");

            var onBananaPeelDestroyedMethod = typeof(ClothingBananaBackpack).GetMethod("OnBananaPeelDestroyed", Plugin.Flags);
            var onBananaPeelDestroyedDelegate = (Action<HawkNetworkBehaviour>)Delegate.CreateDelegate(typeof(Action<HawkNetworkBehaviour>), __instance, onBananaPeelDestroyedMethod);
            networkBehaviour.onDestroy.AddCallback(onBananaPeelDestroyedDelegate);

            spawnedBananaPeels.Add(networkBehaviour);

            if (spawnedBananaPeels.Count > maxBananaPeels && !unlimitedPeels)
            {
                HawkNetworkBehaviour hawkNetworkBehaviour = spawnedBananaPeels[0];

                if (hawkNetworkBehaviour)
                {
                    VanishComponent.VanishAndDestroy(hawkNetworkBehaviour.gameObject);
                }

                spawnedBananaPeels.RemoveAt(0);
            }

            reflect.SetField("spawnedBananaPeels", spawnedBananaPeels);

            return false;
        }
    }
}