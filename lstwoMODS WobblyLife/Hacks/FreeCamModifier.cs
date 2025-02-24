using HarmonyLib;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using ShadowLib;
using UnityEngine;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife.Hacks;

public class FreeCamModifier : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.FreeCamModifier").PatchAll(typeof(Patches));

        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);

        moveSpeedLIB = ui.CreateLIBTrio("Move Speed", "lstwo.FreeCamModifier.MoveSpeed", "5.0");
        moveSpeedLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        moveSpeedLIB.Button.OnClick = () =>
        {
	        moveSpeed = float.Parse(moveSpeedLIB.Input.Text);
        };
        
        ui.AddSpacer(6);

        upMoveSpeedLIB = ui.CreateLIBTrio("Upwards Move Speed", "lstwo.FreeCamModifier.UpMoveSpeed", "5.0");
        upMoveSpeedLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        upMoveSpeedLIB.Button.OnClick = () =>
        {
	        upMoveSpeed = float.Parse(upMoveSpeedLIB.Input.Text);
        };
        
        ui.AddSpacer(6);

        boostMultiplierLIB = ui.CreateLIBTrio("Boost Multiplier", "lstwo.FreeCamModifier.BoostMultiplier", "3.0");
        boostMultiplierLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        boostMultiplierLIB.Button.OnClick = () =>
        {
			boostMultiplier = float.Parse(boostMultiplierLIB.Input.Text);
        };
        
        ui.AddSpacer(6);
        
        lockDistanceLIB = ui.CreateLIBTrio("Lock Distance", "lstwo.FreeCamModifier.LockDistance", "10.0");
        lockDistanceLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
        lockDistanceLIB.Button.OnClick = () =>
        {
	        lockDistance = float.Parse(lockDistanceLIB.Input.Text);
        };
        
        ui.AddSpacer(6);

        infiniteDistanceToggle = ui.CreateToggle("lstwo.FreeCamModifier.InfiniteDistance", "Infinite Distance", b => infiniteDistance = b);
        
        ui.AddSpacer(6);

        ignoreCollisionToggle = ui.CreateToggle("lstwo.FreeCamModifier.IgnoreCollision", "Ignore Collision", b => ignoreCollision = b);

        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
	    moveSpeedLIB.Input.Text = moveSpeed.ToString();
	    upMoveSpeedLIB.Input.Text = upMoveSpeed.ToString();
	    boostMultiplierLIB.Input.Text = boostMultiplier.ToString();
	    lockDistanceLIB.Input.Text = lockDistance.ToString();

	    infiniteDistanceToggle.isOn = infiniteDistance;
	    ignoreCollisionToggle.isOn = ignoreCollision;
    }

    public override string Name => "Free Cam Modifier";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.ExtraHacksTab;

    public static float moveSpeed = 5f;
    public static float upMoveSpeed = 5f;
    public static float boostMultiplier = 3f;
    public static float lockDistance = 10f;
    public static bool infiniteDistance;
    public static bool ignoreCollision;
    
    private HacksUIHelper.LIBTrio moveSpeedLIB;
    private HacksUIHelper.LIBTrio upMoveSpeedLIB;
    private HacksUIHelper.LIBTrio boostMultiplierLIB;
    private HacksUIHelper.LIBTrio lockDistanceLIB;
	private Toggle infiniteDistanceToggle;
	private Toggle ignoreCollisionToggle;
    
    public class Patches
    {
        [HarmonyPatch(typeof(CameraFocusFree), "UpdateCamera")]
        [HarmonyPrefix]
        public static bool UpdateCameraPrefix(ref CameraFocusFree __instance, GameplayCamera camera)
        {
	        var r = new QuickReflection<CameraFocusFree>(__instance, Plugin.Flags);
            var playerController = camera.GetPlayerController();
            
			if (!playerController)
			{
				return false;
			}
			
			var playerTransform = playerController.GetPlayerTransform();
			
			if (!playerTransform)
			{
				return false;
			}
			
			var mouseMovementDeltaX = camera.GetAxisDeltaX() * 5f;
			var mouseMovementDeltaY = camera.GetAxisDeltaY() * 5f;
			var freeCamForwardInput = camera.GetAxisNonRelative("FreeCameraForward");
			var freeCamSidewardInput = camera.GetAxisNonRelative("FreeCameraSideward");
			var freeCamUpwardInput = camera.GetAxisNonRelative("FreeCameraUp") - camera.GetAxisNonRelative("FreeCameraDown");
			var freeCamBoostInput = camera.GetButton("FreeCameraBoost");
			
			var camMovement = Vector3.zero;
			camMovement.x = freeCamSidewardInput * moveSpeed;
			camMovement.z = freeCamForwardInput * moveSpeed;
			camMovement.y = freeCamUpwardInput * upMoveSpeed;
			
			if (freeCamBoostInput)
			{
				camMovement *= boostMultiplier;
			}
			
			var vector2 = camera.transform.position;
			var vector3 = camera.transform.position;
			vector2 += camera.transform.TransformVector(camMovement) * Time.deltaTime;
			var num4 = Vector3.Distance(vector2, playerTransform.position);
			
			if ((bool) r.GetField("bLockDistanceEnabled") && !infiniteDistance && num4 > lockDistance)
			{
				var normalized = (vector2 - playerTransform.position).normalized;
				vector2 = playerTransform.position + normalized * lockDistance;
				
				if (num4 > lockDistance * 1.5f)
				{
					vector2 = playerTransform.position;
					vector3 = playerTransform.position;
				}
			}
			
			camera.transform.Rotate(Vector3.up, mouseMovementDeltaX);
			camera.transform.Rotate(Vector3.right, -mouseMovementDeltaY);
			var eulerAngles = camera.transform.eulerAngles;
			eulerAngles.x = HawkMathUtils.ClampAngle(eulerAngles.x, -85f, 85f);
			eulerAngles.z = 0f;
			camera.transform.eulerAngles = eulerAngles;

			if (!ignoreCollision && (bool) r.GetField("bLockDistanceEnabled"))
			{
				var handleCollisionArgs = new object[] { camera, vector3, 0f, vector2, false };
				var handleCollisionResult = (bool) typeof(CameraFocusFree).GetMethod("HandleCollision", Plugin.Flags)?.Invoke(__instance, handleCollisionArgs);

				var num5 = (float)handleCollisionArgs[2];
				vector2 = (Vector3)handleCollisionArgs[3];

				if (handleCollisionResult)
				{
					int collisionLayerMask = camera.GetCollisionLayerMask();
					if (Physics.Linecast(playerTransform.position, vector2, out var raycastHit, collisionLayerMask, QueryTriggerInteraction.Ignore))
					{
						vector2 = raycastHit.point + raycastHit.normal * 0.51f;
					}
				}
			}
			
			camera.transform.position = vector2;
			return false;
        }
    }
}