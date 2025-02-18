using HarmonyLib;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class FirstPerson : BaseHack
    {
        public static bool firstPersonEnabledPlayer1 = false;
        public static bool firstPersonEnabled = false;
        public static bool enableCutoff = true;

        public static List<GameplayCamera> gameplayCameras = new List<GameplayCamera>();

        public override string Name => "First Person";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        public override void ConstructUI(GameObject root)
        {
            Harmony harmony = new("lstwo.NotAzza.FirstPerson");
            harmony.PatchAll();

            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.FirstPerson.Enable", "Enable First Person", (b) => firstPersonEnabled = b);
            ui.CreateToggle("lstwo.FirstPerson.PlayerOneOnly", "Enable First Person for Player 1 only", (b) => firstPersonEnabledPlayer1 = b);
            ui.CreateToggle("lstwo.FirstPerson.EnableCutoff", "Enable Character Cutoff when in First Person", (b) => enableCutoff = b, true);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }


        [HarmonyPatch(typeof(CameraFocusPlayerCharacter))]
        public static class FirstPersonCameraPatch
        {
            [HarmonyPatch("UpdateCamera")]
            [HarmonyPrefix]
            static bool PrefixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
            {
                // Disable the original camera update method
                if (firstPersonEnabled || firstPersonEnabledPlayer1 && gameplayCameras.Count > 1 && gameplayCameras[0] == camera)
                {
                    __instance.SetUsingCharacterCutoff(enableCutoff);
                    return false;
                }

                if (firstPersonEnabled)
                {
                    __instance.SetUsingCharacterCutoff(true);
                    return true;
                }

                return true;
            }

            [HarmonyPatch("UpdateCamera")]
            [HarmonyPostfix]
            [HarmonyPriority(0)]
            static void PostfixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
            {
                // Call custom update method for first-person camera behavior
                if (firstPersonEnabled)
                {
                    __instance.UpdateFirstPersonCamera(camera);
                    __instance.SetUsingCharacterCutoff(enableCutoff);
                }

                if (firstPersonEnabledPlayer1 && gameplayCameras.Count > 1 && gameplayCameras[0] == camera)
                {
                    __instance.UpdateFirstPersonCamera(camera);
                    __instance.SetUsingCharacterCutoff(enableCutoff);
                }
            }

            [HarmonyPatch("OnFocus")]
            [HarmonyPostfix]
            static void PostfixOnFocus(CameraFocusVehicle __instance, GameplayCamera gameplayCamera)
            {
                if (!gameplayCameras.Contains(gameplayCamera)) gameplayCameras.Add(gameplayCamera);
            }
        }

        [HarmonyPatch(typeof(CameraFocusVehicle))]
        public static class FirstPersonVehicleCameraPatch
        {
            [HarmonyPatch("UpdateCamera")]
            [HarmonyPrefix]
            static bool PrefixUpdateCamera(CameraFocusVehicle __instance, GameplayCamera camera)
            {
                // Disable the original camera update method
                if (firstPersonEnabled || firstPersonEnabledPlayer1 && gameplayCameras.Count > 1 && gameplayCameras[0] == camera)
                    return false;
                else
                    return true;
            }

            [HarmonyPatch("UpdateCamera")]
            [HarmonyPostfix]
            static void PostfixUpdateCamera(CameraFocusVehicle __instance, GameplayCamera camera)
            {
                // Call custom update method for first-person camera behavior
                if (firstPersonEnabled)
                    __instance.UpdateFirstPersonCamera(camera);

                if (firstPersonEnabledPlayer1 && gameplayCameras.Count > 1 && gameplayCameras[0] == camera)
                    __instance.UpdateFirstPersonCamera(camera);
            }

            [HarmonyPatch("OnFocus")]
            [HarmonyPostfix]
            static void PostfixOnFocus(CameraFocusVehicle __instance, GameplayCamera camera)
            {
                if (firstPersonEnabled)
                    __instance.ResetRotation();
            }
        }
    }

    public static class FirstPersonCameraExtension
    {
        public static Dictionary<GameplayCamera, Vector3> mouseRotations = new Dictionary<GameplayCamera, Vector3>();

        public static void UpdateFirstPersonCamera(this CameraFocusPlayerCharacter instance, GameplayCamera camera)
        {
            camera.GetCamera().nearClipPlane = 0.001f;

            if (instance == null || instance.transform == null || instance.transform.Find("Player").Find("Wobbly").Find("Hip") == null) return;

            if (camera.GetPlayerController().GetPlayerCharacter().GetRagdollController().IsActiveRagdoll())
            {
                if (!mouseRotations.ContainsKey(camera)) mouseRotations.Add(camera, Vector3.zero);

                Vector3 mouseRotation = mouseRotations[camera];

                FieldInfo focusTransformField = typeof(CameraFocusPlayerCharacter).GetField("focusTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo rotationAxisField = typeof(CameraFocusPlayerCharacter).GetField("rotationAxis", BindingFlags.NonPublic | BindingFlags.Instance);

                Transform playerCameraTransform = instance.transform.Find("Player").Find("Wobbly").Find("Hip").Find("Chest").Find("Head");
                focusTransformField.SetValue(instance, playerCameraTransform);

                float mouseX = camera.GetAxisDeltaX() * 10;
                float mouseY = camera.GetAxisDeltaY() * 10;

                Vector3 rotationAxis = (Vector3)rotationAxisField.GetValue(instance);
                Vector3 mouseDeltaRotation = Vector3.zero;

                mouseDeltaRotation.y = mouseX;
                mouseDeltaRotation.x = -mouseY;

                mouseRotation.y += mouseDeltaRotation.y;
                mouseRotation.x += mouseDeltaRotation.x;

                mouseRotation.x = Mathf.Clamp(mouseRotation.x, -89, 89);

                mouseRotations[camera] = mouseRotation;

                Vector3 combinedRotation = new(mouseRotation.x, mouseRotation.y, mouseRotation.z);
                rotationAxisField.SetValue(instance, mouseRotation);
                camera.transform.SetPositionAndRotation(playerCameraTransform.position + Vector3.up * .5f, Quaternion.Euler(combinedRotation));
            }
            else
            {
                Transform playerCameraTransform = instance.transform.Find("Player").Find("Wobbly").Find("Hip").Find("Chest").Find("Head");

                camera.transform.SetPositionAndRotation(playerCameraTransform.position + Vector3.up * .3f, playerCameraTransform.rotation);
            }
        }
    }

    public static class FirstPersonCameraVehicleExtension
    {
        public static Dictionary<GameplayCamera, Vector3> mouseRotations = new Dictionary<GameplayCamera, Vector3>();

        public static void UpdateFirstPersonCamera(this CameraFocusVehicle instance, GameplayCamera camera)
        {
            if (instance == null || typeof(CameraFocus).GetField("playerController", BindingFlags.NonPublic | BindingFlags.Instance) == null ||
                ((PlayerController)typeof(CameraFocus).GetField("playerController", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance)).GetPlayerCharacter().transform
                .Find("Player").Find("Wobbly").Find("Hip") == null) return;

            if (!mouseRotations.ContainsKey(camera)) mouseRotations.Add(camera, Vector3.zero);

            Vector3 rotation = mouseRotations[camera];

            if (camera.transform.parent == null)
            {
                GameObject go = new GameObject("cameraPivot");

                camera.transform.SetParent(go.transform);
            }

            PlayerCharacter character = ((PlayerController)typeof(CameraFocus).GetField("playerController", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance))
                .GetPlayerCharacter();

            Transform playerCameraTransform = character.transform.Find("Player").Find("Wobbly").Find("Hip").Find("Chest").Find("Head");
            GameObject vehicle = camera.GetPlayerController().GetPlayerControllerInteractor().GetEnteredAction().GetGameObject();

            FieldInfo rotationEulersField = typeof(CameraFocusVehicle).GetField("rotationEulers", BindingFlags.NonPublic | BindingFlags.Instance);

            Vector3 rotationDelta = Vector3.zero;
            Vector3 playerCamRotation;

            if (vehicle.GetComponent<PlayerVehicle>() != null)
                playerCamRotation = camera.GetPlayerController().GetPlayerControllerInteractor().GetEnteredAction().GetGameObject().transform.rotation.eulerAngles;
            else
                playerCamRotation = playerCameraTransform.rotation.eulerAngles;

            rotationDelta += new Vector3(0, camera.GetAxisDeltaX() * 10, 0);
            rotationDelta += new Vector3(-camera.GetAxisDeltaY() * 10, 0, 0);

            rotation += rotationDelta;

            rotation.x = Mathf.Clamp(rotation.x, -89, 89);

            Vector3 combined = new(playerCamRotation.x, playerCamRotation.y + rotation.y, playerCamRotation.z);
            Vector3 combinedFull = playerCamRotation + rotationDelta;

            rotationEulersField.SetValue(instance, (Vector3)rotationEulersField.GetValue(instance) + rotationDelta);

            mouseRotations[camera] = rotation;

            if (vehicle.GetComponent<PlayerHoverboardMovement>() != null || vehicle.GetComponent<PlayerBallMovement>() != null || vehicle.GetComponent<PlayerSpaceHopperMovement>() != null)
            {
                camera.transform.localRotation = Quaternion.Euler(rotation.x, 0, 0);
                camera.transform.parent.rotation = Quaternion.Euler(new(playerCamRotation.x, playerCamRotation.y, playerCamRotation.z));
                camera.transform.parent.position = playerCameraTransform.position + Vector3.up * .7f;
                camera.transform.localPosition = Vector3.zero;
            }
            else
            {
                camera.transform.localRotation = Quaternion.Euler(rotation.x, 0, 0);
                camera.transform.parent.rotation = Quaternion.Euler(combined);
                camera.transform.parent.position = playerCameraTransform.position + Vector3.up * .7f;
                camera.transform.localPosition = Vector3.zero;
            }
        }

        public static void ResetRotation(this CameraFocusVehicle instance)
        {
            typeof(CameraFocusVehicle).GetField("rotationEulers", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, ((PlayerController)typeof(CameraFocus)
                .GetField("playerController", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance)).GetPlayerCharacter().transform.Find("Player").Find("Wobbly").Find("Hip")
                .Find("Chest").Find("Head").rotation.eulerAngles);
        }
    }
}
