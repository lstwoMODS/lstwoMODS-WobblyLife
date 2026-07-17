using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class FirstPerson : BaseMod
{
    [ModSetting(Label = "Enable First Person")] public static Ref<bool> firstPersonEnabled = new();
    [ModSetting(Label = "Enable First Person for Player 1 only")] public static Ref<bool> firstPersonEnabledPlayer1 = new();
    [ModSetting(Label = "Enable Character Cutoff when in First Person")] public static Ref<bool> enableCutoff = new(true);

    public static List<GameplayCamera> gameplayCameras => GameInstance.Instance?.GetPlayerControllers()?.Select(x => x.GetGameplayCamera()).ToList();

    public static GameplayCamera firstGameplayCamera => GameInstance.Instance?.GetFirstLocalPlayerController()?.GetGameplayCamera();

    public override string Name => "First Person";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    protected override void OnStaticInit()
    {
        Harmony harmony = new("lstwo.NotAzza.FirstPerson");
        harmony.PatchAll(typeof(FirstPerson));
        harmony.PatchAll(typeof(FirstPersonCameraPatch));
        harmony.PatchAll(typeof(FirstPersonVehicleCameraPatch));
    }

    [HarmonyPatch(typeof(CameraFocusPlayerCharacter))]
    public static class FirstPersonCameraPatch
    {
        [HarmonyPatch("UpdateCamera")]
        [HarmonyPrefix]
        static bool PrefixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
        {
            if (firstPersonEnabled.Value || firstPersonEnabledPlayer1.Value && gameplayCameras.Count > 1 && gameplayCameras[0] == camera)
            {
                __instance.SetUsingCharacterCutoff(enableCutoff.Value);
                return false;
            }

            if (firstPersonEnabled.Value)
            {
                __instance.SetUsingCharacterCutoff(true);
                return true;
            }

            return true;
        }

        [HarmonyPatch("UpdateCamera")]
        [HarmonyPostfix]
        [HarmonyPriority(90)]
        static void PostfixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
        {
            if (firstPersonEnabled.Value)
            {
                __instance.UpdateFirstPersonCamera(camera);
                __instance.SetUsingCharacterCutoff(enableCutoff.Value);
            }

            if (firstPersonEnabledPlayer1.Value && gameplayCameras.Count >= 1 && firstGameplayCamera == camera)
            {
                __instance.UpdateFirstPersonCamera(camera);
                __instance.SetUsingCharacterCutoff(enableCutoff.Value);
            }
        }
    }

    [HarmonyPatch(typeof(CameraFocusVehicle))]
    public static class FirstPersonVehicleCameraPatch
    {
        [HarmonyPatch("UpdateCamera")]
        [HarmonyPrefix]
        static bool PrefixUpdateCamera(CameraFocusVehicle __instance, GameplayCamera camera)
        {
            if (firstPersonEnabled.Value || (firstPersonEnabledPlayer1.Value && gameplayCameras.Count >= 1 && firstGameplayCamera == camera))
            {
                __instance.SetUsingCharacterCutoff(enableCutoff.Value);
                return false;
            }

            if (firstPersonEnabled.Value)
            {
                __instance.SetUsingCharacterCutoff(true);
                return true;
            }

            return true;
        }

        [HarmonyPatch("UpdateCamera")]
        [HarmonyPostfix]
        static void PostfixUpdateCamera(CameraFocusVehicle __instance, GameplayCamera camera)
        {
            if (firstPersonEnabled.Value)
            {
                __instance.UpdateFirstPersonCamera(camera);
                __instance.SetUsingCharacterCutoff(enableCutoff.Value);
            }

            if (firstPersonEnabledPlayer1.Value && gameplayCameras.Count >= 1 && gameplayCameras[0] == camera)
            {
                __instance.UpdateFirstPersonCamera(camera);
                __instance.SetUsingCharacterCutoff(enableCutoff.Value);
            }
        }

        [HarmonyPatch("OnFocus")]
        [HarmonyPostfix]
        static void PostfixOnFocus(CameraFocusVehicle __instance, GameplayCamera camera)
        {
            if (firstPersonEnabled.Value)
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

            var mouseRotation = mouseRotations[camera];

            var focusTransformField = typeof(CameraFocusPlayerCharacter).GetField("focusTransform", BindingFlags.NonPublic | BindingFlags.Instance);
            var rotationAxisField = typeof(CameraFocusPlayerCharacter).GetField("rotationAxis", BindingFlags.NonPublic | BindingFlags.Instance);

            var playerCameraTransform = instance.transform.Find("Player").Find("Wobbly").Find("Hip").Find("Chest").Find("Head");
            focusTransformField.SetValue(instance, playerCameraTransform);

            var mouseX = camera.GetAxisDeltaX() * 10;
            var mouseY = camera.GetAxisDeltaY() * 10;

            var rotationAxis = (Vector3)rotationAxisField.GetValue(instance);
            var mouseDeltaRotation = Vector3.zero;

            mouseDeltaRotation.y = mouseX;
            mouseDeltaRotation.x = -mouseY;

            mouseRotation.y += mouseDeltaRotation.y;
            mouseRotation.x += mouseDeltaRotation.x;

            mouseRotation.x = Mathf.Clamp(mouseRotation.x, -89, 89);

            mouseRotations[camera] = mouseRotation;

            Vector3 combinedRotation = new(mouseRotation.x, mouseRotation.y, mouseRotation.z);
            rotationAxisField.SetValue(instance, mouseRotation);
            camera.transform.SetPositionAndRotation(playerCameraTransform.position + playerCameraTransform.up * .15f + Vector3.up * .15f, Quaternion.Euler(combinedRotation));
            
            // set to avoid seeing your own head; clamp to avoid nearClipPlane=0 when looking straight ahead
            camera.GetCamera().nearClipPlane = Mathf.Max(0.001f, 0.00175f * Mathf.Abs(combinedRotation.x));
        }
        else
        {
            var playerCameraTransform = instance.transform.Find("Player").Find("Wobbly").Find("Hip").Find("Chest").Find("Head");

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