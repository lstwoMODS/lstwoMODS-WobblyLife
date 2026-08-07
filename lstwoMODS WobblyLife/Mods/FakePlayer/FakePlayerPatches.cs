using HarmonyLib;

namespace lstwoMODS_WobblyLife.Mods.FakePlayer;

public static class FakePlayerPatches
{
    [HarmonyPatch(typeof(PlayerController), "LoadSaveData")]
    [HarmonyPrefix]
    public static bool PlayerController_LoadSaveData_Prefix(PlayerController __instance)
    {
        return !__instance.GetComponent<FakePlayerMarker>();
    }

    [HarmonyPatch(typeof(PlayerControllerInputManager), "OnLocalPlayeridAssigned")]
    [HarmonyPrefix]
    public static bool PlayerControllerInputManager_OnLocalPlayeridAssigned_Prefix(PlayerControllerInputManager __instance)
    {
        return !__instance.GetComponent<FakePlayerMarker>();
    }

    [HarmonyPatch(typeof(GameInstance), nameof(GameInstance.GetMaxAllowedLocalPlayers))]
    [HarmonyPrefix]
    public static bool GameInstance_GetMaxAllowedLocalPlayers_Prefix(ref int __result)
    {
        __result = 32;
        return false;
    }

    [HarmonyPatch(typeof(PlayerCharacterInput), "HandleInput")]
    [HarmonyPrefix]
    public static bool PlayerCharacterInput_HandleInput_Prefix(PlayerCharacterInput __instance)
    {
        var marker = __instance.GetComponent<FakePlayerMarker>();
        var instance = marker != null ? marker.Instance : null;
        if (instance?.InputProvider == null) return true;

        if (__instance.networkObject == null || !__instance.networkObject.IsOwner()) return false;
        if (!__instance.IsControlsEnabled()) return false;

        var input = instance.InputProvider(instance);
        if (!input.HasValue) return false;

        __instance.EnqueueInput(input.Value, false);
        return false;
    }
}
