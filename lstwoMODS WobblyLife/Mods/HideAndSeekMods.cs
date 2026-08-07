using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using ModWobblyLife;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class HideAndSeekMods : BaseMod
{
    public override string Name => "Hide and Seek Mods";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    [ModSetting(Label = "Number of Seekers", Min = 1, Max = 8)]
    public static Ref<int> SeekerCount = new(1);

    protected override void OnStaticInit()
    {
        base.OnStaticInit();

        new Harmony(Id).PatchAll(typeof(Patches));
    }

    public static class Patches
    {
        [HarmonyPatch(typeof(HideAndSeekGamemode), "Intro")]
        [HarmonyPrefix]
        public static bool IntroWithMultipleSeekers(HideAndSeekGamemode __instance, ref IEnumerator __result)
        {
            // A single seeker is the vanilla behaviour, so let the original run untouched.
            if (SeekerCount.Value <= 1)
            {
                return true;
            }

            __result = MultiSeekerIntroRoutine(__instance);
            return false;
        }

        // Faithful reproduction of HideAndSeekGamemode.Intro that promotes several random
        // players to seekers instead of just one. Everything a seeker relies on keys off
        // ServerSetIsSeeker/IsSeeker (catching, spawn points, cameras, respawn), so seeding
        // multiple seekers is all that's needed. seekerController still points at one of them
        // for the win/abort checks that assume a single reference.
        private static IEnumerator MultiSeekerIntroRoutine(HideAndSeekGamemode gamemode)
        {
            if (gamemode.introCamera_Seeker != null)
            {
                gamemode.introCamera_Seeker.Play();
            }
            if (gamemode.introCamera_Hider != null)
            {
                gamemode.introCamera_Hider.Play();
            }

            var controllers = ModInstance.Instance.GetModPlayerControllers();

            var seekers = new HashSet<ModPlayerController>();
            if (controllers.Length != 0)
            {
                var desired = SeekerCount.Value;
                if (desired < 1)
                {
                    desired = 1;
                }
                if (desired > controllers.Length)
                {
                    desired = controllers.Length;
                }

                var pool = new List<ModPlayerController>(controllers);
                ModPlayerController firstSeeker = null;
                for (var i = 0; i < desired && pool.Count > 0; i++)
                {
                    var idx = Random.Range(0, pool.Count);
                    var picked = pool[idx];
                    pool.RemoveAt(idx);
                    seekers.Add(picked);
                    if (firstSeeker == null)
                    {
                        firstSeeker = picked;
                    }
                }

                gamemode.seekerController = firstSeeker;
            }

            foreach (var controller in controllers)
            {
                var isSeeker = seekers.Contains(controller);
                if (!isSeeker)
                {
                    gamemode.hiderControllers.Add(controller);
                }
                if (controller is HideAndSeekPlayerController hideAndSeekPlayerController)
                {
                    hideAndSeekPlayerController.ServerSetIsSeeker(isSeeker);
                }
            }

            foreach (var controller in controllers)
            {
                gamemode.SpawnPlayerCharacter(controller, null, false);
                var inputManager = controller.GetModPlayerControllerInputManager();
                if (inputManager)
                {
                    inputManager.DisablePlayerTransformInput(gamemode);
                }
            }

            yield return new WaitForSeconds(gamemode.timeTillInstructionsAppear);

            foreach (var controller in controllers)
            {
                if (controller is HideAndSeekPlayerController hideAndSeekPlayerController)
                {
                    hideAndSeekPlayerController.ServerSetInstructionsVisible(true);
                }
            }

            yield return new WaitUntil(() =>
                gamemode.introCamera_Hider.HasFinished() && gamemode.introCamera_Seeker.HasFinished());

            foreach (var controller in controllers)
            {
                if (controller is HideAndSeekPlayerController hideAndSeekPlayerController)
                {
                    hideAndSeekPlayerController.ServerSetInstructionsVisible(false);
                }
            }

            foreach (var controller in controllers)
            {
                var inputManager = controller.GetModPlayerControllerInputManager();
                if (inputManager)
                {
                    inputManager.EnablePlayerTransformInput(gamemode);
                }
            }

            gamemode.SetGamemodeState(HideAndSeekGamemode.HideAndSeekGamemodeState.Countdown);
        }
    }
}
