using HarmonyLib;
using NotAzzamods.UI.TabMenus;
using ShadowLib;
using UnityEngine;

namespace NotAzzamods.Hacks
{
    public class Debt : BaseHack
    {
        public override string Name => "Debt Mod";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        public static bool bEnableDebt = false;
        public static bool bDoubleDebt = true;

        public override void ConstructUI(GameObject root)
        {
            var h = new Harmony("lstwo.NotAzza.Debt");
            h.PatchAll(typeof(DebtPatch));

            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("", "Enable Debt", (b) => bEnableDebt = b);
            ui.CreateToggle("", "Double Charges While In Debt", (b) => bDoubleDebt = b, true);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public static class DebtPatch
        {
            [HarmonyPatch(typeof(PlayerControllerEmployment), "OnUpdateMoney")]
            [HarmonyPrefix]
            public static bool OnUpdateMoney(ref PlayerControllerEmployment __instance, ref int amount, ref PlayerControllerEmployment.LocalMoneyChanged callback)
            {
                var r = new QuickReflection<PlayerControllerEmployment>(__instance, Plugin.Flags);

                var persistentData = (SavePlayerPersistentData)r.GetField("persistentData");
                var playerController = (PlayerController)r.GetField("playerController");

                if (persistentData != null && persistentData.MiscData != null)
                {
                    long newMoney = persistentData.MiscData.money;
                    newMoney += amount;

                    if (newMoney > 2147483647L)
                    {
                        newMoney = 2147483647L;
                    }

                    if (newMoney < 0L && amount < 0L && bDoubleDebt && bEnableDebt)
                    {
                        newMoney += amount;
                    }

                    else if (newMoney < 0L && !bEnableDebt)
                    {
                        newMoney = 0;
                    }

                    persistentData.MiscData.money = (int)newMoney;

                    if (callback != null)
                    {
                        callback(amount, (int)newMoney);
                    }

                    if (playerController != null && playerController.IsLocal())
                    {
                        if (newMoney >= 1000L)
                        {
                            global::AchievementManager.Instance.UnlockAchievement(WobblyAchievement.HAVE_1000_IN_THE_BANK, playerController);
                        }

                        if (newMoney >= 5000L)
                        {
                            global::AchievementManager.Instance.UnlockAchievement(WobblyAchievement.HAVE_5000_IN_THE_BANK, playerController);
                        }

                        if (newMoney >= 10000L)
                        {
                            global::AchievementManager.Instance.UnlockAchievement(WobblyAchievement.HAVE_10000_IN_THE_BANK, playerController);
                        }
                    }
                }

                return false;
            }
        }
    }
}
