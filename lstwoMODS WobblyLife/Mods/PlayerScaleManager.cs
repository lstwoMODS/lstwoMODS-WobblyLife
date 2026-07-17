using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class PlayerScaleManager : PlayerBasedMod
{
    public override string Name => "Player Scale";
    public override string Description => "Scale the selected player's ragdoll. Baked into the prefab so it applies cleanly on (re)spawn.";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    [ModSetting(Speed = 0.05f, Order = 10, ApplyButton = true, Label = "Player Scale", ApplyButtonLabel = "Apply & Respawn")]
    public float PlayerScale
    {
        get => GetScaleSettings(Player?.Controller).Scale;
        set
        {
            GetScaleSettings(Player?.Controller).Scale = Mathf.Max(0.05f, value);
            Player?.Character?.Kill();
        }
    }

    [ModSetting(Min = 0f, Max = 1f, Order = 20, Label = "Speed Scaling (0 = none, 1 = full)")]
    public static float SpeedScaling = 0.75f;

    public static PlayerScaleSettings GetScaleSettings(PlayerController controller) => GetPlayerSettings<PlayerScaleSettings>(typeof(PlayerScaleManager), controller);

    protected override void OnStaticInit()
    {
        new Harmony("lstwoMODS.PlayerScale").PatchAll(typeof(PlayerScalePatches));
    }
}

public class PlayerScaleSettings
{
    public float Scale = 1f;
}

public static class PlayerScalePatches
{
    [HarmonyPatch(typeof(PlayerController), "ServerSpawnPlayerCharacter")]
    [HarmonyPrefix]
    public static void ScalePrefabOnSpawn(PlayerController __instance, ref PlayerCharacter playerCharacterPrefab)
    {
        var prefab = playerCharacterPrefab ?? __instance.playerCharacterPrefab;
        if (prefab == null) return;

        var body = prefab.GetComponentInChildren<PlayerBody>(true);
        if (body == null) return;

        var hipRb = body.GetRigidbody();
        var hip = hipRb ? hipRb.transform : body.transform;

        var target = PlayerScaleManager.GetScaleSettings(__instance).Scale;
        hip.localScale = Vector3.one * target;
    }

    [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateMovement")]
    [HarmonyPrefix]
    public static void ScaleLocomotion(PlayerCharacterMovement __instance, out float[] __state)
    {
        __state = new[] { __instance.speedMultiplier, __instance.jumpMultiplier };

        var scale = GetScaleFor(__instance);
        if (Mathf.Approximately(scale, 1f)) return;

        __instance.speedMultiplier *= Mathf.Pow(scale, PlayerScaleManager.SpeedScaling);
        __instance.jumpMultiplier *= Mathf.Sqrt(scale);
    }

    [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateMovement")]
    [HarmonyPostfix]
    public static void RestoreLocomotion(PlayerCharacterMovement __instance, float[] __state)
    {
        __instance.speedMultiplier = __state[0];
        __instance.jumpMultiplier = __state[1];
    }

    private static float GetScaleFor(PlayerCharacterMovement movement)
    {
        var hip = movement.hipRigidbody;
        return hip ? hip.transform.lossyScale.x : 1f;
    }

    [HarmonyPatch(typeof(PlayerCharacterMovement), "OnRagdollCollisionEnter")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ScaleKnockoutThreshold(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value && value == 20f)
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0)
                {
                    labels = instruction.labels,
                    blocks = instruction.blocks,
                };
                yield return CodeInstruction.Call(typeof(PlayerScalePatches), nameof(KnockoutThresholdFor));
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static float KnockoutThresholdFor(PlayerCharacterMovement movement)
    {
        return 20f * Mathf.Max(1f, GetScaleFor(movement));
    }

    [HarmonyPatch(typeof(RagdollAnimTarget), "CreateDrives")]
    [HarmonyPostfix]
    public static void ScaleDrives(RagdollAnimTarget __instance)
    {
        var scale = __instance.transform.lossyScale.x;
        if (Mathf.Approximately(scale, 1f)) return;

        BoostDrive(ref __instance.drive120, scale);
        BoostDrive(ref __instance.drive300, scale);
        BoostDrive(ref __instance.drive500, scale);
        BoostDrive(ref __instance.drive1000, scale);
        BoostDrive(ref __instance.drive2000, scale);
        BoostDrive(ref __instance.drive4000, scale);
        BoostDrive(ref __instance.drive8000, scale);
        BoostDrive(ref __instance.drive16000, scale);
    }

    private static void BoostDrive(ref JointDrive drive, float scale)
    {
        drive.positionSpring *= scale;
        drive.positionDamper *= scale;
        drive.maximumForce *= scale;
    }

    [HarmonyPatch(typeof(CameraFocusPlayerCharacter), "UpdateCamera")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ScaleCameraDistance(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var lerp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Lerp),
            [typeof(float), typeof(float), typeof(float)]);

        for (var i = 0; i + 1 < code.Count; i++)
        {
            if (code[i].opcode != OpCodes.Ldc_R4 || code[i].operand is not float a || a != 6f) continue;
            if (code[i + 1].opcode != OpCodes.Ldc_R4 || code[i + 1].operand is not float b || b != 2f) continue;

            for (var j = i + 2; j < code.Count; j++)
            {
                if (!code[j].Calls(lerp)) continue;

                code.InsertRange(j + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(PlayerScalePatches), nameof(CameraDistanceScaleFor)),
                    new CodeInstruction(OpCodes.Mul),
                });
                return code;
            }
        }

        return code;
    }

    public static float CameraDistanceScaleFor(CameraFocusPlayerCharacter instance)
    {
        var character = instance.playerCharacter;
        if (character == null) return 1f;

        var body = character.GetPlayerBody();
        var hip = body ? body.GetRigidbody() : null;
        return hip ? hip.transform.lossyScale.x : 1f;
    }
}
