using System;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife.Mods;

public class BuyUnlimitedHouses : BaseMod
{
    public override string Name => "Buy Unlimited Houses";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    [ModSetting]
    public static readonly Ref<bool> Enabled = new();

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            SaveGuard.Notice("houses-guard-notice"),
            SaveGuard.Guard(base.BuildPanel(id))
        );
    }

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.BuyUnlimitedHouses").PatchAll(typeof(Patches));
    }

    public static class Patches
    {
        [HarmonyPatch(typeof(UIPlayerBasedHouseBuyHouse), "TryPurchaseHouse_Internal")]
        [HarmonyPrefix]
        public static bool TryPurchaseHouse_Internal_Prefix(UIPlayerBasedHouseBuyHouse __instance)
        {
            if (!Enabled.Value || SaveGuard.On) return true;

            if (!__instance.playerController || !__instance.houseSign) return false;

            var buyableHouse = __instance.houseSign.GetBuyableHouse();
            if (!buyableHouse) return false;

            var unlocker = __instance.playerController.GetPlayerControllerUnlocker();
            if (!unlocker) return false;

            if (unlocker.IsHouseUnlocked(buyableHouse)) return false;

            var money = __instance.playerController.GetPlayerControllerEmployment()?.GetLocalMoney() ?? 0;

            if (!buyableHouse.IsEnoughMoney(money))
            {
                if (__instance.playerBasedUI)
                    __instance.playerBasedUI.GetUIPromptCanvas().ShowPrompt_NotEnoughMoney();
                return false;
            }

            __instance.houseSign.Buy(__instance.playerController);

            if (__instance.buyButton)
                __instance.buyButton.interactable = false;

            return false;
        }

        [HarmonyPatch(typeof(PlayerControllerUnlocker), nameof(PlayerControllerUnlocker.UnlockHouse), typeof(Scene), typeof(Guid))] [HarmonyPrefix]
        public static bool UnlockHouse_Prefix(PlayerControllerUnlocker __instance, Scene scene, Guid houseGUID,
            ref bool __result)
        { 
            if (!Enabled.Value || SaveGuard.On) return true;

            if (!__instance.playerController)
            {
                __result = false;
                return false;
            }

            var flag = false;
            var data = __instance.playerController.GetPlayerPersistentData();
            if (data != null)
                flag = data.HousesData.AddHouse(scene, houseGUID);

            if (flag)
            {
                __instance.OnHouseUnlocked(scene, houseGUID);
                if (__instance.networkObject.IsOwner())
                    __instance.networkObject.SendRPC(__instance.RPC_SERVER_HOUSE_UNLOCK, RPCRecievers.Server, houseGUID);
            }

            if (flag && __instance.networkObject.IsServer() && !__instance.networkObject.IsOwner())
                __instance.networkObject.SendRPC(__instance.RPC_CLIENT_HOUSE_UNLOCK, RPCRecievers.Owner, houseGUID);

            __result = flag;
            return false;
        }
    }
}
