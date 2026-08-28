using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class PlayerCameraModifier : BaseMod
{
    public const string HarmonyId = "lstwo.lstwoMODS_WobblyLife.PlayerCameraModifier";

    private const float CollisionPadding = 1.5f;
    private const float CollisionPullInLerp = 10f;
    private const float LerpMulEase = 3f;
    private const float ArmPitchReference = 50f;

    [ModSetting(
        Label = "Enable Camera Modifier",
        Description = "Replaces the vanilla third person solve for the character camera. "
                    + "First Person always wins when it is driving the same camera.",
        Order = 10)]
    public static readonly Ref<bool> Enabled = new();

    
    [ModSetting(
        Label = "Look Speed",
        Min = 0.1f, Max = 5f, Format = "%.2fx",
        SeparatorText = "Look",
        Description = "Multiplier on the focus rotation speed (vanilla is 5). Stacks on top of "
                    + "the game's own sensitivity setting, which is already applied by GetAxisDelta.",
        Order = 20)]
    public static readonly Ref<float> LookSpeed = new(1f);

    [ModSetting(
        Label = "Min Pitch",
        Min = -89f, Max = 0f, Format = "%.0f deg",
        Description = "How far down you can look. Vanilla is -85.",
        Order = 30)]
    public static readonly Ref<float> MinPitch = new(-85f);

    [ModSetting(
        Label = "Max Pitch",
        Min = 0f, Max = 89f, Format = "%.0f deg",
        Description = "How far up you can look. Vanilla is 85.",
        Order = 40)]
    public static readonly Ref<float> MaxPitch = new(85f);

    [ModSetting(
        Label = "Arm Pitch Clamp",
        Min = 5f, Max = 89f, Format = "%.0f deg",
        Description = "The camera arm is clamped tighter than your look angle, so looking straight "
                    + "up or down swings the camera less than the aim. Vanilla is 50. Raising this "
                    + "toward Max Pitch makes the camera follow the aim more literally.",
        Order = 50)]
    public static readonly Ref<float> ARMPitchClamp = new(50f);

    [ModSetting(
        Label = "Pitch Offset",
        Min = -30f, Max = 30f, Format = "%.0f deg",
        Description = "Constant downward tilt baked into the arm, so the neutral shot looks slightly "
                    + "down at the character. Vanilla is 10.",
        Order = 60)]
    public static readonly Ref<float> PitchOffset = new(10f);

    [ModSetting(
        Label = "View Angle",
        Min = -180f, Max = 180f, Format = "%.0f deg",
        SeparatorText = "View Angle",
        Description = "Swings the camera around the character without changing which way they face "
                    + "or move. 0 is behind them (vanilla), 90 and -90 are side views, 180 puts the "
                    + "camera in front looking back at them.",
        Order = 62)]
    public static readonly Ref<float> YawOffset = new(0f);

    [ModSetting(
        Label = "Fixed View Angle",
        Description = "Treats View Angle as an absolute compass bearing instead of an offset from "
                    + "the character, so the camera holds one world direction and no longer swings "
                    + "when you turn. Mouse look still steers the character. Pair it with a low Arm "
                    + "Pitch Clamp for a fixed side-scroller camera.",
        Order = 64)]
    public static readonly Ref<bool> FixedYaw = new();

    [ModSetting(
        Label = "Invert Vertical",
        Description = "Flips which way the camera swings when you look up and down, without "
                    + "touching where the character aims or moves. Turn this on for mirrored views "
                    + "(a View Angle near 180) so looking up lifts the camera and keeps it pointed "
                    + "at the character instead of dropping below them.",
        Order = 66)]
    public static readonly Ref<bool> InvertPitch = new();


    [ModSetting(
        Label = "Max Distance",
        Min = 0.5f, Max = 30f, Format = "%.2f m",
        SeparatorText = "Framing",
        Description = "Distance at neutral pitch. Vanilla is 6.",
        Order = 70)]
    public static readonly Ref<float> MaxDistance = new(6f);

    [ModSetting(
        Label = "Min Distance",
        Min = 0.1f, Max = 30f, Format = "%.2f m",
        Description = "Distance at full arm pitch, and the floor the collision pull-in clamps to. "
                    + "Vanilla is 2.",
        Order = 80)]
    public static readonly Ref<float> MinDistance = new(2f);

    [ModSetting(
        Label = "Height Offset",
        Min = -3f, Max = 5f, Format = "%.2f m",
        Description = "Raises or lowers the point the camera orbits and aims at. Vanilla is 0, "
                    + "the focus transform exactly.",
        Order = 90)]
    public static readonly Ref<float> HeightOffset = new(0f);

    [ModSetting(
        Label = "Shoulder Offset",
        Min = -3f, Max = 3f, Format = "%.2f m",
        Description = "Slides the camera sideways along the orbit for an over-the-shoulder shot. "
                    + "The aim point slides with it, so the character sits off-centre instead of "
                    + "the camera just orbiting. Negative is left. Vanilla is 0 (dead centre).",
        Order = 100)]
    public static readonly Ref<float> ShoulderOffset = new(0f);

    [ModSetting(
        Label = "Pitch Height Lift",
        Min = 0f, Max = 4f, Format = "%.2f m",
        Description = "Extra height added at neutral pitch and faded out as the arm pitches, so the "
                    + "camera sits above the character when looking level. Vanilla is 1.",
        Order = 110)]
    public static readonly Ref<float> PitchHeightLift = new(1f);

    
    [ModSetting(
        Label = "Position Smoothing",
        Min = 0f, Max = 40f, Format = "%.1f",
        SeparatorText = "Feel",
        Description = "Lerp rate per second for camera position. Vanilla is 10. Zero snaps the "
                    + "camera with no smoothing at all.",
        Order = 120)]
    public static readonly Ref<float> PosSmoothing = new(10f);

    [ModSetting(
        Label = "Rotation Smoothing",
        Min = 0f, Max = 1f, Format = "%.3f s",
        Description = "SmoothDamp time for camera rotation. Vanilla is 0.125. Zero snaps.",
        Order = 130)]
    public static readonly Ref<float> RotSmoothing = new(0.125f);

    [ModSetting(
        Label = "Distance Lerp",
        Min = 0.1f, Max = 30f, Format = "%.1f",
        Description = "How fast the camera pushes back out to its target distance after a wall or "
                    + "a pitch change, in metres per second. Vanilla is 3.",
        Order = 140)]
    public static readonly Ref<float> DistanceLerp = new(3f);

    [ModSetting(
        Label = "Snap Distance",
        Min = 5f, Max = 200f, Format = "%.0f m",
        Description = "Past this gap between camera and character the camera teleports instead of "
                    + "smoothing, so respawns and teleports don't fly across the map. Vanilla is 20.",
        Order = 150)]
    public static readonly Ref<float> SnapDistance = new(20f);

    
    [ModSetting(
        Label = "Camera Collision",
        SeparatorText = "Collision",
        Description = "Vanilla wall avoidance: pulls the camera in when geometry blocks the view. "
                    + "Turning it off lets the camera pass through walls.",
        Order = 160)]
    public static readonly Ref<bool> Collision = new(true);

    
    public override string Name => "Player Camera Modifier";

    public override string Description =>
        "Retunes the third person character camera: look limits, view angle, orbit distance, framing "
        + "offsets, smoothing and wall collision. Purely local, nothing goes on the wire.";

    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    protected override void OnStaticInit()
    {
        new Harmony(HarmonyId).PatchAll(typeof(HarmonyPatches));
    }

    private static float Wrap360(float angle) => Mathf.Repeat(angle, 360f);

    private static bool DriveCharacter(CameraFocusPlayerCharacter focus, GameplayCamera camera)
    {
        var target = focus.focusTransform;
        var sync = focus.transformSync;

        if (target == null || sync == null || !sync.HasRecievedInitialWithDelay())
            return false;

        var pitchLo = Mathf.Min(MinPitch.Value, MaxPitch.Value);
        var pitchHi = Mathf.Max(MinPitch.Value, MaxPitch.Value);

        var look = focus.rotationAxis;
        look.y = Wrap360(look.y + camera.GetAxisDeltaX() * focus.rotationSpeed * LookSpeed.Value);
        look.x = Mathf.Clamp(look.x - camera.GetAxisDeltaY() * focus.rotationSpeed * LookSpeed.Value,
                             pitchLo, pitchHi);

        focus.rotationAxis = look;

        var armClamp = Mathf.Max(ARMPitchClamp.Value, 0f);
        var lookPitch = InvertPitch.Value ? -look.x : look.x;
        var armPitch = Mathf.Clamp(lookPitch + PitchOffset.Value, -armClamp, armClamp);
        var pitchFactor = Mathf.Clamp01(Mathf.Abs(armPitch) / ArmPitchReference);

        var near = Mathf.Min(MinDistance.Value, MaxDistance.Value);
        var far = Mathf.Max(MinDistance.Value, MaxDistance.Value);

        var wanted = Mathf.Lerp(far, near, pitchFactor) * PlayerScalePatches.CameraDistanceScaleFor(focus);

        var camYaw = FixedYaw.Value ? YawOffset.Value : look.y + YawOffset.Value;

        var yaw = Quaternion.AngleAxis(camYaw, Vector3.up);
        var pitch = Quaternion.AngleAxis(armPitch, Vector3.right);
        var lateral = yaw * Vector3.right * ShoulderOffset.Value;

        var pivot = target.position + Vector3.up * HeightOffset.Value;

        var camPos = pivot - yaw * (pitch * (Vector3.forward * focus.distance));
        camPos += Vector3.up * (1f - pitchFactor) * PitchHeightLift.Value;
        camPos += lateral;

        var camRot = Quaternion.LookRotation(pivot + lateral - camPos, Vector3.up);

        if (Collision.Value && focus.HandleCollision(camera, pivot, out var hitDistance, ref camPos, false))
        {
            var pulled = Mathf.Clamp(hitDistance + CollisionPadding, near, far);

            focus.distance = pulled < wanted
                ? Mathf.Lerp(focus.distance, pulled, Time.deltaTime * CollisionPullInLerp)
                : Mathf.MoveTowards(focus.distance, wanted, Time.deltaTime * DistanceLerp.Value);
        }
        else
        {
            focus.distance = Mathf.MoveTowards(focus.distance, wanted, Time.deltaTime * DistanceLerp.Value);
        }

        focus.currentLerpMul = Mathf.Lerp(focus.currentLerpMul, focus.lerpMul, Time.deltaTime * LerpMulEase);

        if (PosSmoothing.Value <= 0f || Vector3.Distance(pivot, camera.transform.position) >= SnapDistance.Value)
        {
            camera.transform.SetPositionAndRotation(camPos, camRot);
            return true;
        }

        camera.transform.position = Vector3.Lerp(camera.transform.position, camPos,
            Time.deltaTime * PosSmoothing.Value * focus.currentLerpMul);

        camera.transform.rotation = RotSmoothing.Value <= 0f ? camRot : UnityExtensions.SmoothDamp(camera.transform.rotation, camRot, ref focus.currentRotVelocity, RotSmoothing.Value, Time.deltaTime);

        return true;
    }

    public static class HarmonyPatches
    {
        [HarmonyPatch(typeof(CameraFocusPlayerCharacter), "UpdateCamera")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter(FirstPerson.HarmonyId)]
        private static bool PrefixUpdateCamera(CameraFocusPlayerCharacter __instance, GameplayCamera camera,
                                               bool __runOriginal)
        {
            if (!__runOriginal)
                return false;

            if (camera == null || !Enabled.Value)
                return true;

            return !DriveCharacter(__instance, camera);
        }
    }
}
