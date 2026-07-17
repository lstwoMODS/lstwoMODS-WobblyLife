using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class FreeCamModifier : BaseMod
{
    public override string Name => "Free Cam Modifier";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    [ModSetting(Order = 10)]
    public static float moveSpeed = 5f;
    
    [ModSetting(Order = 20)]
    public static float upMoveSpeed = 5f;
    
    [ModSetting(Order = 30)]
    public static float boostMultiplier = 3f;
    
    [ModSetting(Order = 40, Min = 0)]
    public static float lockDistance = 10f;
    
    [ModSetting(Order = 50)]
    public static bool infiniteDistance;
    
    [ModSetting(Order = 60)]
    public static bool ignoreCollision;

    protected override void OnStaticInit()
    {
        new Harmony("lstwo.lstwoMODS_WobblyLife.FreeCamModifier").PatchAll(typeof(Patches));
    }
    
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