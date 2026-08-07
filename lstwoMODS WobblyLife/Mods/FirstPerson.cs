using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

/// <summary>
/// Drives the gameplay camera from the character's head bone.
///
/// Patch shape (matters for coexisting with other Harmony patches):
/// the whole first person camera is computed in a low priority <b>prefix</b> that returns
/// <c>false</c>, so vanilla <c>UpdateCamera</c> never runs on the same frame, so the camera is
/// solved exactly once. Low priority means every other mod's prefix gets to run (and to veto
/// the update) before ours does, and because we own no postfix, other mods' postfixes still
/// see and can adjust the final transform. If we cannot produce a first person pose (no head
/// bone yet, no camera, not seated in anything) the prefix returns <c>true</c> and vanilla
/// takes over for that frame instead of leaving the camera frozen.
///
/// Everything we mutate on game objects (near clip plane, character cutoff flag,
/// <c>focusTransform</c>, the camera's parent) is captured on entry and handed back on exit,
/// so toggling first person off (or switching to a camera focus we don't patch at all)
/// leaves the game exactly as we found it.
/// </summary>
public class FirstPerson : BaseMod
{
    public const float DefaultNearClip = 0.05f;
    public const float DefaultMinNearClip = 0.02f;

    /// <summary>Near plane gained per degree of pitch when the character cutoff is off.</summary>
    private const float NearClipPitchRamp = 0.00175f;

    private const float LookSpeed = 10f;
    private const float PitchClamp = 89f;

    [ModSetting(Label = "Enable First Person")] public static Ref<bool> firstPersonEnabled = new();
    [ModSetting(Label = "Enable First Person for Player 1 only")] public static Ref<bool> firstPersonEnabledPlayer1 = new();
    [ModSetting(Label = "Enable Character Cutoff when in First Person")] public static Ref<bool> enableCutoff = new(true);

    [ModSetting(
        Label = "Near Clip Plane",
        Min = 0.005f, Max = 0.3f, Format = "%.3f",
        SeparatorText = "Near Clip",
        Description = "Camera near plane while in first person. Raise it if the inside of the head "
                    + "pokes into view, lower it to see more of what is right in front of you "
                    + "(too low costs depth buffer precision and causes z-fighting in the distance).")]
    public static Ref<float> nearClip = new(DefaultNearClip);

    [ModSetting(
        Label = "Min Near Clip Plane",
        Min = 0.001f, Max = 0.3f, Format = "%.3f",
        Description = "Only used while the character cutoff is off: the near plane ramps from this "
                    + "value up to Near Clip Plane as you look further up or down, so the body clips "
                    + "away instead of filling the screen. Clamped to at most Near Clip Plane.")]
    public static Ref<float> minNearClip = new(DefaultMinNearClip);

    /// <summary>Fast (compiled) access to the private CameraFocusPlayerCharacter.focusTransform field.</summary>
    private static readonly AccessTools.FieldRef<CameraFocusPlayerCharacter, Transform> FocusTransformRef =
        AccessTools.FieldRefAccess<CameraFocusPlayerCharacter, Transform>("focusTransform");

    /// <summary>One entry per gameplay camera we have ever driven; kept alive so look angles survive toggling.</summary>
    private static readonly Dictionary<GameplayCamera, State> states = new();
    private static readonly List<GameplayCamera> deadCameras = new();

    public static List<GameplayCamera> gameplayCameras => GameInstance.Instance?.GetPlayerControllers()?.Select(x => x.GetGameplayCamera()).ToList();

    public static GameplayCamera firstGameplayCamera => GameInstance.Instance?.GetFirstLocalPlayerController()?.GetGameplayCamera();

    public override string Name => "First Person";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    /// <summary>True when this camera should be driven in first person.</summary>
    public static bool IsFirstPersonCamera(GameplayCamera camera)
    {
        if (firstPersonEnabled.Value)
            return true;

        return firstPersonEnabledPlayer1.Value && firstGameplayCamera == camera;
    }

    /// <summary>Null-safe lookup of the "Player/Wobbly/Hip/Chest/Head" bone.</summary>
    public static Transform FindHead(Transform root)
        => root == null ? null : root.Find("Player")?.Find("Wobbly")?.Find("Hip")?.Find("Chest")?.Find("Head");

    /// <summary>
    /// Nothing here runs while first person has never been used (<see cref="states"/> stays empty).
    /// Once it has, this is the safety net that restores a camera we stopped receiving
    /// <c>UpdateCamera</c> calls for: the focus was swapped for one we don't patch, the player
    /// left the vehicle, the character despawned. In those cases the patches themselves never
    /// get a chance to clean up.
    /// </summary>
    public override void Update()
    {
        if (states.Count == 0)
            return;

        var frame = Time.frameCount;

        foreach (var pair in states)
        {
            var camera = pair.Key;
            var state = pair.Value;

            if (camera == null)
            {
                state.Restore(null);
                deadCameras.Add(camera);
                continue;
            }

            // UpdateCamera runs in LateUpdate, so the freshest possible stamp is the previous
            // frame when this runs in Update. Anything older means we are no longer driving it.
            if (state.Active && frame - state.LastFrame > 1)
                state.Restore(camera);
        }

        if (deadCameras.Count == 0)
            return;

        foreach (var camera in deadCameras)
            states.Remove(camera);

        deadCameras.Clear();
    }

    protected override void OnStaticInit()
    {
        Harmony harmony = new("lstwo.NotAzza.FirstPerson");
        harmony.PatchAll(typeof(FirstPersonCameraPatch));
        harmony.PatchAll(typeof(FirstPersonVehicleCameraPatch));
    }

    /// <summary>Near plane for the current settings at the given absolute pitch, in degrees.</summary>
    private static float NearClipFor(float pitchAbs)
    {
        var max = Mathf.Max(nearClip.Value, 0.001f);

        if (enableCutoff.Value)
            return max;

        var min = Mathf.Clamp(minNearClip.Value, 0.001f, max);
        return Mathf.Clamp(NearClipPitchRamp * pitchAbs, min, max);
    }

    private static State GetState(GameplayCamera camera)
    {
        if (!states.TryGetValue(camera, out var state))
            states[camera] = state = new State();

        return state;
    }

    /// <summary>Hand a camera back to vanilla if we are currently driving it. No-op otherwise.</summary>
    private static void LeaveFirstPerson(GameplayCamera camera)
    {
        if (states.Count == 0 || camera == null)
            return;

        if (states.TryGetValue(camera, out var state) && state.Active)
            state.Restore(camera);
    }

    private static bool DriveCharacterCamera(CameraFocusPlayerCharacter focus, GameplayCamera camera)
    {
        var cam = camera.GetCamera();
        if (cam == null)
            return false;

        var state = GetState(camera);

        var head = state.ResolveHead(focus.transform);
        if (head == null)
            return false;

        state.Activate(camera, focus);

        var character = camera.GetPlayerController()?.GetPlayerCharacter();
        var ragdollController = character == null ? null : character.GetRagdollController();

        if (ragdollController == null || !ragdollController.IsActiveRagdoll())
        {
            // Animated: the head bone already carries the look direction.
            cam.transform.SetPositionAndRotation(head.position + Vector3.up * .3f, head.rotation);
            ApplyNearClip(cam, 90f);
            return true;
        }

        // Ragdolling: the head tumbles, so look angles are accumulated by hand and the vanilla
        // focus is repointed at the head so third person picks up where we left off.
        if (!state.FocusTransformOverridden)
        {
            state.SavedFocusTransform = FocusTransformRef(focus);
            state.FocusTransformOverridden = true;
        }

        if (FocusTransformRef(focus) != head)
            FocusTransformRef(focus) = head;

        var look = state.CharacterLook;
        look.y += camera.GetAxisDeltaX() * LookSpeed;
        look.x -= camera.GetAxisDeltaY() * LookSpeed;
        look.x = Mathf.Clamp(look.x, -PitchClamp, PitchClamp);
        state.CharacterLook = look;

        focus.SetRotationAxis(look);

        cam.transform.SetPositionAndRotation(
            head.position + head.up * .15f + Vector3.up * .15f,
            Quaternion.Euler(look));

        ApplyNearClip(cam, Mathf.Abs(look.x));
        return true;
    }

    private static bool DriveVehicleCamera(CameraFocusVehicle focus, GameplayCamera camera)
    {
        var cam = camera.GetCamera();
        if (cam == null)
            return false;

        var controller = camera.GetPlayerController();
        var character = controller?.GetPlayerCharacter();
        if (character == null)
            return false;

        var interactor = controller.GetPlayerControllerInteractor();
        var entered = interactor == null ? null : interactor.GetEnteredAction()?.GetGameObject();
        if (entered == null)
            return false;

        var state = GetState(camera);

        var head = state.ResolveHead(character.transform);
        if (head == null)
            return false;

        state.Activate(camera, focus);

        var pivot = state.AttachPivot(camera);

        // Hoverboard / ball / space hopper steer with the body, so their yaw comes from the ride
        // itself; everything else adds the accumulated look yaw on top. Resolved once per ride
        // rather than four GetComponent calls every frame.
        state.ResolveEntered(entered);

        var delta = new Vector3(-camera.GetAxisDeltaY() * LookSpeed, camera.GetAxisDeltaX() * LookSpeed, 0f);

        var look = state.VehicleLook + delta;
        look.x = Mathf.Clamp(look.x, -PitchClamp, PitchClamp);
        state.VehicleLook = look;

        focus.SetRotationEulers(focus.GetRotationEulers() + delta);

        var baseRotation = state.EnteredIsVehicle
            ? entered.transform.rotation.eulerAngles
            : head.rotation.eulerAngles;

        pivot.SetPositionAndRotation(
            head.position + Vector3.up * .7f,
            state.EnteredHasFreeYaw
                ? Quaternion.Euler(baseRotation)
                : Quaternion.Euler(baseRotation.x, baseRotation.y + look.y, baseRotation.z));

        camera.transform.localRotation = Quaternion.Euler(look.x, 0f, 0f);
        camera.transform.localPosition = Vector3.zero;

        ApplyNearClip(cam, Mathf.Abs(look.x));
        return true;
    }

    private static void ApplyNearClip(Camera cam, float pitchAbs)
    {
        var value = NearClipFor(pitchAbs);

        if (cam.nearClipPlane != value)
            cam.nearClipPlane = value;
    }

    [HarmonyPatch(typeof(CameraFocusPlayerCharacter))]
    public static class FirstPersonCameraPatch
    {
        [HarmonyPatch("UpdateCamera")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        static bool PrefixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera)
        {
            if (camera == null || !IsFirstPersonCamera(camera))
            {
                LeaveFirstPerson(camera);
                return true;
            }

            // Skip vanilla only when we actually produced a pose, so the camera is never solved
            // twice and never left unsolved.
            return !DriveCharacterCamera(__instance, camera);
        }
    }

    [HarmonyPatch(typeof(CameraFocusVehicle))]
    public static class FirstPersonVehicleCameraPatch
    {
        [HarmonyPatch("UpdateCamera")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        static bool PrefixUpdateCamera(CameraFocusVehicle __instance, GameplayCamera camera)
        {
            if (camera == null || !IsFirstPersonCamera(camera))
            {
                LeaveFirstPerson(camera);
                return true;
            }

            return !DriveVehicleCamera(__instance, camera);
        }

        [HarmonyPatch("OnFocus")]
        [HarmonyPostfix]
        static void PostfixOnFocus(CameraFocusVehicle __instance, GameplayCamera camera)
        {
            if (camera == null || !IsFirstPersonCamera(camera))
                return;

            var head = FindHead(camera.GetPlayerController()?.GetPlayerCharacter()?.transform);
            if (head == null)
                return;

            __instance.SetRotationEulers(head.rotation.eulerAngles);
        }
    }

    /// <summary>
    /// Per-camera first person state: the look angles we accumulate, the per-frame caches that
    /// keep the hot path allocation and lookup free, and (most importantly) every original
    /// value we have to hand back when first person ends.
    /// </summary>
    private sealed class State
    {
        public bool Active;
        public int LastFrame = -1;

        public Vector3 CharacterLook;
        public Vector3 VehicleLook;

        public bool FocusTransformOverridden;
        public Transform SavedFocusTransform;

        public bool EnteredIsVehicle;
        public bool EnteredHasFreeYaw;

        private CameraFocus focus;
        private bool savedCutoff;
        private float savedNearClip;
        private Transform savedParent;
        private Transform pivot;
        private Transform headRoot;
        private Transform head;
        private GameObject entered;

        /// <summary>Cached head bone lookup; a reference compare on the hot path, and no cache to leak.</summary>
        public Transform ResolveHead(Transform root)
        {
            if (root == null)
                return null;

            if (headRoot != root || head == null)
            {
                headRoot = root;
                head = FindHead(root);
            }

            return head;
        }

        public void ResolveEntered(GameObject value)
        {
            if (entered == value)
                return;

            entered = value;
            EnteredIsVehicle = value.GetComponent<PlayerVehicle>() != null;
            EnteredHasFreeYaw = value.GetComponent<PlayerHoverboardMovement>() != null
                             || value.GetComponent<PlayerBallMovement>() != null
                             || value.GetComponent<PlayerSpaceHopperMovement>() != null;
        }

        /// <summary>Take ownership of the camera and focus, snapshotting whatever we are about to overwrite.</summary>
        public void Activate(GameplayCamera camera, CameraFocus newFocus)
        {
            LastFrame = Time.frameCount;

            if (!Active)
            {
                var cam = camera.GetCamera();

                savedNearClip = cam != null ? cam.nearClipPlane : 0.3f;
                savedParent = camera.transform.parent;
                Active = true;
            }
            else if (focus == newFocus)
            {
                // Steady state: only touch the cutoff flag when something else changed it or the
                // setting flipped, so we don't stomp on it every single frame.
                if (focus != null && focus.IsUsingCharacterCutoff() != enableCutoff.Value)
                    focus.SetUsingCharacterCutoff(enableCutoff.Value);

                return;
            }
            else
            {
                RestoreFocus();
            }

            focus = newFocus;
            savedCutoff = newFocus.IsUsingCharacterCutoff();
            newFocus.SetUsingCharacterCutoff(enableCutoff.Value);
        }

        /// <summary>Lazily create our own pivot for the vehicle camera and parent the camera under it.</summary>
        public Transform AttachPivot(GameplayCamera camera)
        {
            if (pivot == null)
                pivot = new GameObject("lstwoMODS_FirstPersonPivot").transform;

            // Only ever reparent onto a pivot we own, so a camera someone else parented is left alone.
            if (camera.transform.parent != pivot)
                camera.transform.SetParent(pivot, true);

            return pivot;
        }

        /// <summary>Undo every change first person made. Safe to call repeatedly; <paramref name="camera"/> may be destroyed.</summary>
        public void Restore(GameplayCamera camera)
        {
            if (!Active)
                return;

            Active = false;

            RestoreFocus();

            if (camera != null)
            {
                var cam = camera.GetCamera();

                if (cam != null)
                    cam.nearClipPlane = savedNearClip;

                if (pivot != null && camera.transform.parent == pivot)
                    camera.transform.SetParent(savedParent, true);
            }

            if (pivot != null)
            {
                Object.Destroy(pivot.gameObject);
                pivot = null;
            }

            savedParent = null;
            headRoot = null;
            head = null;
            entered = null;
        }

        private void RestoreFocus()
        {
            if (focus != null)
            {
                focus.SetUsingCharacterCutoff(savedCutoff);

                if (FocusTransformOverridden && focus is CameraFocusPlayerCharacter characterFocus)
                    FocusTransformRef(characterFocus) = SavedFocusTransform;
            }

            FocusTransformOverridden = false;
            SavedFocusTransform = null;
            focus = null;
        }
    }
}
