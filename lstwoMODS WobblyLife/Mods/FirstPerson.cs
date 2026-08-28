using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class FirstPerson : BaseMod
{
    public const string HarmonyId = "lstwo.NotAzza.FirstPerson";

    public const float DefaultNearClip = 0.05f;
    public const float DefaultMinNearClip = 0.02f;
    public const float DefaultHeightOffsetHeadUp = 0.15f;
    public const float DefaultHeightOffsetWorldUp = 0.15f;
    public const float DefaultVehicleHeightOffset = 0.7f;

    private const float NearClipPitchRamp = 0.00175f;

    private const float LookSpeed = 10f;
    private const float PitchClamp = 89f;

    [ModSetting(Label = "Enable First Person", Order = 10)] public static Ref<bool> FirstPersonEnabled = new();
    [ModSetting(Label = "Enable First Person for Player 1 only", Order = 20)] public static Ref<bool> FirstPersonEnabledPlayer1 = new();
    [ModSetting(Label = "Enable Character Cutoff when in First Person", Order = 30)] public static Ref<bool> EnableCutoff = new();

    [ModSetting(
        Label = "Hide Hat when in First Person",
        Description = "Hides whatever is worn in the hat slot while the camera is inside the head, "
                    + "since a lot of hats sit low enough to fill the screen. Purely local (nobody "
                    + "else sees it change) and it does nothing while an outfit is worn, because "
                    + "outfits draw their own hat and the hat slot is already hidden by the game.",
        Order = 40
    )]
    public static Ref<bool> HideHat = new();

    [ModSetting(
        Label = "Near Clip Plane",
        Min = 0.005f, Max = 0.3f, Format = "%.3f",
        SeparatorText = "Near Clip",
        Description = "Camera near plane while in first person. Raise it if the inside of the head "
                    + "pokes into view, lower it to see more of what is right in front of you "
                    + "(too low costs depth buffer precision and causes z-fighting in the distance).",
        Order = 50
    )]
    public static Ref<float> NearClip = new(DefaultNearClip);

    [ModSetting(
        Label = "Min Near Clip Plane",
        Min = 0.001f, Max = 0.3f, Format = "%.3f",
        Description = "Only used while the character cutoff is off: the near plane ramps from this "
                    + "value up to Near Clip Plane as you look further up or down, so the body clips "
                    + "away instead of filling the screen. Clamped to at most Near Clip Plane.",
        Order = 60
    )]
    public static Ref<float> MinNearClip = new(DefaultMinNearClip);

    [ModSetting(
        Label = "Height Offset (Head Up)",
        Min = 0f, Max = 1.5f,
        Order = 70
    )]
    public static Ref<float> HeightOffsetHeadUp = new(DefaultHeightOffsetHeadUp);

    [ModSetting(
        Label = "Height Offset (World Up)",
        Min = 0f, Max = 1.5f,
        Order = 80
    )]
    public static Ref<float> HeightOffsetWorldUp = new(DefaultHeightOffsetWorldUp);

    [ModSetting(
        Label = "Vehicle Height Offset",
        Min = 0f, Max = 2f,
        Order = 90
    )]
    public static Ref<float> VehicleHeightOffset = new(DefaultVehicleHeightOffset);
    
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
        if (FirstPersonEnabled.Value)
            return true;

        return FirstPersonEnabledPlayer1.Value && firstGameplayCamera == camera;
    }

    /// <summary>Null-safe lookup of the "Player/Wobbly/Hip/Chest/Head" bone.</summary>
    public static Transform FindHead(Transform root)
        => root == null ? null : root.Find("Player")?.Find("Wobbly")?.Find("Hip")?.Find("Chest")?.Find("Head");

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
        Harmony harmony = new(HarmonyId);
        harmony.PatchAll(typeof(FirstPersonCameraPatch));
        harmony.PatchAll(typeof(FirstPersonVehicleCameraPatch));
        
        BindData(EnableCutoff, nameof(EnableCutoff));
        BindData(HideHat, nameof(HideHat));
        
        BindData(NearClip, nameof(NearClip), DefaultNearClip);
        BindData(MinNearClip, nameof(MinNearClip), DefaultMinNearClip);
        
        BindData(HeightOffsetHeadUp, nameof(HeightOffsetHeadUp), DefaultHeightOffsetHeadUp);
        BindData(HeightOffsetWorldUp, nameof(HeightOffsetWorldUp), DefaultHeightOffsetWorldUp);
        BindData(VehicleHeightOffset, nameof(VehicleHeightOffset), DefaultVehicleHeightOffset);
    }

    private static float Wrap360(float angle) => Mathf.Repeat(angle, 360f);

    /// <summary>Near plane for the current settings at the given absolute pitch, in degrees.</summary>
    private static float NearClipFor(float pitchAbs)
    {
        var max = Mathf.Max(NearClip.Value, 0.001f);

        if (EnableCutoff.Value)
            return max;

        var min = Mathf.Clamp(MinNearClip.Value, 0.001f, max);
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

        state.ApplyHatVisibility(character);

        var ragdollController = character == null ? null : character.GetRagdollController();

        if (ragdollController == null || !ragdollController.IsActiveRagdoll())
        {
            cam.transform.SetPositionAndRotation(head.position + Vector3.up * .3f, head.rotation);
            ApplyNearClip(cam, 90f);
            return true;
        }

        if (!state.FocusTransformOverridden)
        {
            state.SavedFocusTransform = FocusTransformRef(focus);
            state.FocusTransformOverridden = true;
        }

        if (FocusTransformRef(focus) != head)
            FocusTransformRef(focus) = head;

        var look = state.CharacterLook;
        look.y = Wrap360(look.y + camera.GetAxisDeltaX() * LookSpeed);
        look.x = Mathf.Clamp(look.x - camera.GetAxisDeltaY() * LookSpeed, -PitchClamp, PitchClamp);
        state.CharacterLook = look;

        focus.SetRotationAxis(look);

        cam.transform.SetPositionAndRotation(
            head.position + head.up * HeightOffsetHeadUp.Value + Vector3.up * HeightOffsetWorldUp.Value,
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
        state.ApplyHatVisibility(character);

        var pivot = state.AttachPivot(camera);

        state.ResolveEntered(entered);

        var delta = new Vector3(-camera.GetAxisDeltaY() * LookSpeed, camera.GetAxisDeltaX() * LookSpeed, 0f);

        var look = state.VehicleLook + delta;
        look.x = Mathf.Clamp(look.x, -PitchClamp, PitchClamp);
        look.y = Wrap360(look.y);
        state.VehicleLook = look;

        var eulers = focus.GetRotationEulers() + delta;
        eulers.y = Wrap360(eulers.y);
        focus.SetRotationEulers(eulers);

        var baseRotation = state.EnteredIsVehicle
            ? entered.transform.rotation.eulerAngles
            : head.rotation.eulerAngles;

        pivot.SetPositionAndRotation(
            head.position + Vector3.up * VehicleHeightOffset.Value,
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
        private CharacterCustomize hatCustomize;
        private readonly List<ClothingPiece> hiddenHats = new();

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

        public void ApplyHatVisibility(PlayerCharacter character)
        {
            var customize = character == null ? null : character.GetPlayerCharacterCustomize();

            if (hatCustomize != customize || !HideHat.Value)
                RestoreHats();

            if (customize == null || !HideHat.Value)
                return;

            hatCustomize = customize;

            var attached = customize.GetAttachedClothing();
            if (attached == null)
                return;

            for (var i = 0; i < attached.Count; i++)
            {
                var piece = attached[i];

                if (piece == null || piece.IsDestroyed())
                    continue;

                if (piece.GetClothingSelectionType() != ClothingSelectionType.Hat || !IsVisible(piece))
                    continue;

                piece.Hide();

                if (!hiddenHats.Contains(piece))
                    hiddenHats.Add(piece);
            }
        }

        /// <summary>Show every hat we hid again, minus the ones an outfit has taken over in the meantime.</summary>
        private void RestoreHats()
        {
            if (hiddenHats.Count > 0)
            {
                var slotHat = hatCustomize != null && hatCustomize.IsWearingOutfit()
                    ? hatCustomize.GetClothingHat()
                    : null;

                for (var i = 0; i < hiddenHats.Count; i++)
                {
                    var piece = hiddenHats[i];

                    if (piece != null && !piece.IsDestroyed() && piece != slotHat)
                        piece.Show();
                }

                hiddenHats.Clear();
            }

            hatCustomize = null;
        }

        private static bool IsVisible(ClothingPiece piece)
        {
            var skinned = piece.GetSkinnedMeshRenderer();

            if (skinned != null)
                return skinned.enabled;

            var external = piece.GetExternalRenderers();
            return external != null && external.Length > 0 && external[0] != null && external[0].enabled;
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
                if (focus != null && focus.IsUsingCharacterCutoff() != EnableCutoff.Value)
                    focus.SetUsingCharacterCutoff(EnableCutoff.Value);

                return;
            }
            else
            {
                RestoreFocus();
            }

            focus = newFocus;
            savedCutoff = newFocus.IsUsingCharacterCutoff();
            newFocus.SetUsingCharacterCutoff(EnableCutoff.Value);
        }

        /// <summary>Lazily create our own pivot for the vehicle camera and parent the camera under it.</summary>
        public Transform AttachPivot(GameplayCamera camera)
        {
            if (pivot == null)
                pivot = new GameObject("lstwoMODS_FirstPersonPivot").transform;

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
            RestoreHats();

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
