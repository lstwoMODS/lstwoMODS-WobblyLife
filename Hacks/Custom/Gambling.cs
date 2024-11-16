using HarmonyLib;
using HawkNetworking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;
using NotAzzamods.UI.TabMenus;

namespace NotAzzamods.Hacks.Custom
{
    public class Gambling : BaseHack
    {
        public static float timeSinceLastGamblePrompt = 0;
        public static bool enableGambling = false;

        public override string Name => "Gambling";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        public override void ConstructUI(GameObject root)
        {
            new Harmony("lstwo.NotAzza.Gambling").PatchAll(typeof(Gambling));

            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("gamblingToggle", "Enable Gambling", (b) => enableGambling = b);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public static int DoGamble(int currentMoney)
        {
            if (!enableGambling) return currentMoney;

            int rand = UnityEngine.Random.Range(0, 200);
            int newMoney;

            if (rand < 1)
            {
                if (UnityEngine.Random.Range(0, 10) < 1)
                {
                    newMoney = currentMoney * 20000;
                }
                else
                {
                    newMoney = currentMoney * 100;
                }
            }
            else if (rand < 5)
            {
                newMoney = currentMoney * 10;
            }
            else if (rand < 50)
            {
                newMoney = currentMoney * 2;
            }
            else if (rand < 75)
            {
                newMoney = currentMoney;
            }
            else if (rand < 100)
            {
                newMoney = currentMoney / 2;
            }
            else if (rand < 201)
            {
                newMoney = 0;
            }
            else if (rand < 185)
            {
                newMoney = -currentMoney;
            }
            else
            {
                newMoney = -currentMoney * 2;
            }
            return newMoney;
        }

        [HarmonyPatch(typeof(Bank), "OnMoneyBagDeposit")]
        [HarmonyPrefix]
        private static bool OnMoneyBagDesposit(ref Bank __instance, ref MoneyBag moneyBag)
        {
            var acceptedMoneyTypes = (MoneyType)typeof(Bank).GetField("acceptedMoneyTypes", Plugin.Flags).GetValue(__instance);
            var RPC_CLIENT_MONEY_DEPOSITED = (byte)typeof(Bank).GetField("RPC_CLIENT_MONEY_DEPOSITED", Plugin.Flags).GetValue(__instance);

            if (!__instance.networkObject.IsServer() || !moneyBag)
            {
                return false;
            }
            MoneyType moneyType = moneyBag.GetMoneyType();
            if ((acceptedMoneyTypes & moneyType) == MoneyType.None)
            {
                return false;
            }
            if (__instance.GetAssetIdRaw() == "77b8f6a55800b6d40826fde2d7350b5e")
            {
                moneyBag.SetMoney(DoGamble(moneyBag.GetMoney()));
            }
            PlayerController moneyOwnerPlayerController = moneyBag.GetMoneyOwnerPlayerController();
            if (moneyOwnerPlayerController)
            {
                HawkNetworkObject networkObject = moneyBag.GetNetworkObject();
                if (networkObject != null)
                {
                    PlayerControllerEmployment playerControllerEmployment = moneyOwnerPlayerController.GetPlayerControllerEmployment();
                    if (playerControllerEmployment)
                    {
                        playerControllerEmployment.UpdateMoney(moneyBag.GetMoney());
                    }
                    moneyBag.OnCashedIn(moneyOwnerPlayerController);
                    Action<MoneyBag> action = __instance.onMoneyBagDeposited;
                    if (action != null)
                    {
                        action(moneyBag);
                    }
                    networkObject.Destroy();
                    moneyBag.gameObject.SetActive(false);
                    __instance.networkObject.SendRPC(RPC_CLIENT_MONEY_DEPOSITED, moneyOwnerPlayerController.networkObject.GetOwner(), Array.Empty<object>());
                    typeof(Bank).GetMethod("OnMoneyBagSuccessfullyDeposited", Plugin.Flags).Invoke(__instance, new object[] { moneyBag });
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(Bank), "ClientEnterBank")]
        [HarmonyPrefix]
        private static bool ClientEnterBank(ref Bank __instance, ref HawkNetReader reader, ref HawkRPCInfo info)
        {
            PlayerController playerControllerByNetworkID = UnitySingleton<GameInstance>.Instance.GetPlayerControllerByNetworkID(reader.ReadUInt32());
            if (playerControllerByNetworkID && playerControllerByNetworkID.IsLocal())
            {
                PlayerBasedUI playerBasedUI = playerControllerByNetworkID.GetPlayerBasedUI();
                if (playerBasedUI)
                {
                    UIPlayerBasedGameplayCanvas uigameplayCanvas = playerBasedUI.GetUIGameplayCanvas();
                    UIPlayerBasedPromptCanvas uipromptCanvas = playerBasedUI.GetUIPromptCanvas();
                    if (__instance.GetAssetIdRaw() == "77b8f6a55800b6d40826fde2d7350b5e" && uipromptCanvas && Time.time - timeSinceLastGamblePrompt >= 1f && enableGambling)
                    {
                        UIPlayerBasedPromptCanvas uiplayerBasedPromptCanvas = uipromptCanvas;
                        string text5 = "Gambling Station";
                        string text2 = "How much money would you like to gamble?";
                        string text3 = text5;
                        string text4 = text2;
                        PromptButtonAction[] array = new PromptButtonAction[]
                        {
                            new PromptButtonAction("Cancel", delegate
                            {
                                timeSinceLastGamblePrompt = Time.time;
                            }),
                            new PromptButtonAction("$1", delegate
                            {
                                playerControllerByNetworkID.GetPlayerControllerEmployment().UpdateMoney(DoGamble(1) - 1);
                                timeSinceLastGamblePrompt = Time.time;
                            }),
                            new PromptButtonAction("$5", delegate
                            {
                                playerControllerByNetworkID.GetPlayerControllerEmployment().UpdateMoney(DoGamble(5) - 5);
                                timeSinceLastGamblePrompt = Time.time;
                            }),
                            new PromptButtonAction("$25", delegate
                            {
                                playerControllerByNetworkID.GetPlayerControllerEmployment().UpdateMoney(DoGamble(25) - 25);
                                timeSinceLastGamblePrompt = Time.time;
                            }),
                            new PromptButtonAction("$100", delegate
                            {
                                playerControllerByNetworkID.GetPlayerControllerEmployment().UpdateMoney(DoGamble(100) - 100);
                                timeSinceLastGamblePrompt = Time.time;
                            }),
                            new PromptButtonAction("$1000", delegate
                            {
                                playerControllerByNetworkID.GetPlayerControllerEmployment().UpdateMoney(DoGamble(1000) - 1000);
                                timeSinceLastGamblePrompt = Time.time;
                            }),
                            new PromptButtonAction("$5000", delegate
                            {
                                playerControllerByNetworkID.GetPlayerControllerEmployment().UpdateMoney(DoGamble(5000) - 5000);
                                timeSinceLastGamblePrompt = Time.time;
                            })
                        };
                        uipromptCanvas.HidePrompt();
                        uiplayerBasedPromptCanvas.ShowPrompt(text3, text4, array);
                        return false;
                    }
                    if (uigameplayCanvas)
                    {
                        UIPlayerBasedGameplayGenericCanvas genericCanvas = uigameplayCanvas.GetGenericCanvas();
                        if (genericCanvas)
                        {
                            genericCanvas.ShowMoney(null);
                        }
                    }
                }
            }
            return false;
        }
    }
}
