using System.Threading;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class BugFixes : BaseMod
{
    public override string Name => "Bug Fixes";
    public override string Description => "Fixes for bugs in the base game.";
    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;

    [ModSetting] public static Ref<bool> FixError4 = new(true);

    protected override void OnStaticInit()
    {
        new Harmony("net.lstwo.lstwoMODS.WobblyLife.BugFixes").PatchAll(typeof(Patches));
    }

    public static class Patches
    {
        private static readonly object PoolGate = new object();

        private static void Enter(out bool taken)
        {
            taken = false;
            if (!FixError4.Value) return;
            Monitor.Enter(PoolGate, ref taken);
        }

        private static void Exit(bool taken)
        {
            if (taken) Monitor.Exit(PoolGate);
        }

        [HarmonyPatch(typeof(HawkNetworkBehaviour), "SetupCallbacks"), HarmonyPrefix]
        public static void SetupCallbacks_Prefix(out bool __state) => Enter(out __state);

        [HarmonyPatch(typeof(HawkNetworkBehaviour), "SetupCallbacks"), HarmonyFinalizer]
        public static void SetupCallbacks_Finalizer(bool __state) => Exit(__state);

        [HarmonyPatch(typeof(HawkNetworkBehaviour), "Release"), HarmonyPrefix]
        public static void Release_Prefix(out bool __state) => Enter(out __state);

        [HarmonyPatch(typeof(HawkNetworkBehaviour), "Release"), HarmonyFinalizer]
        public static void Release_Finalizer(bool __state) => Exit(__state);
    }
}
