using HarmonyLib;
using NotAzzamods.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NotAzzamods.Hacks
{
    public class BuyUnlimitedHouses : BaseHack
    {
        public static bool enabled = false;

        public override string Name => "Buy Unlimited Houses";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        public override void ConstructUI(GameObject root)
        {
            new Harmony("lstwo.NotAzza.BuyUnlimitedHouses").PatchAll(typeof(Patches));
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("enabled", "Enable Unlimited Houses (Only for you)", (b) => enabled = b);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public static class Patches
        {
            [HarmonyPatch(typeof(UIPlayerBasedHouseBuyHouse), "Show", new Type[] { typeof(BuyHouseSign) })]
            [HarmonyPrefix]
            public static bool Show(ref UIPlayerBasedHouseBuyHouse __instance, ref BuyHouseSign houseSign)
            {
                var r = new QuickReflection<UIPlayerBasedHouseBuyHouse>(__instance, Plugin.Flags);

                r.SetField("houseSign", houseSign);
                __instance.Show();
                BuyableHouse buyableHouse = houseSign.GetBuyableHouse();
                if (!buyableHouse)
                {
                    return false;
                }
                int num = 0;
                if (__instance.GetPlayerController())
                {
                    PlayerControllerEmployment playerControllerEmployment = __instance.GetPlayerController().GetPlayerControllerEmployment();
                    if (playerControllerEmployment)
                    {
                        num = playerControllerEmployment.GetLocalMoney();
                    }
                }
                PlayerControllerUnlocker playerControllerUnlocker = __instance.GetPlayerController().GetPlayerControllerUnlocker();
                bool flag = playerControllerUnlocker && playerControllerUnlocker.IsHouseUnlocked(buyableHouse);
                bool flag2 = playerControllerUnlocker && playerControllerUnlocker.GetHousesUnlockedCount() >= 1 && !enabled;
                if ((Button)r.GetField("buyButton"))
                {
                    ((Button)r.GetField("buyButton")).interactable = buyableHouse && buyableHouse.IsEnoughMoney(num) && !flag && !flag2;
                }
                if ((Button)r.GetField("cancelButton"))
                {
                    ((Button)r.GetField("cancelButton")).interactable = true;
                }
                if ((TextMeshProUGUI)r.GetField("moneyText"))
                {
                    ((TextMeshProUGUI)r.GetField("moneyText")).text = "$" + buyableHouse.GetHousePrice().ToString();
                }
                if ((Button)r.GetField("sellButton"))
                {
                    ((Button)r.GetField("sellButton")).interactable = flag;
                }
                return false;
            }
        }
    }
}
