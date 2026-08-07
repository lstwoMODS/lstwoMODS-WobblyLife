using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods;

public class PlayerScaleManager : PlayerBasedMod
{
    public override string Name => "Player Scale";
    public override string Description => "Scale the selected player's ragdoll. Baked into the prefab so it applies cleanly on (re)spawn. Host only; replicates to clients running the mod.";
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

        var scaleRoot = ResolveScaleRoot(prefab);
        if (scaleRoot == null) return;

        var target = PlayerScaleManager.GetScaleSettings(__instance).Scale;
        scaleRoot.localScale = Vector3.one * target;
    }

    /// <summary>
    /// The transform the whole character hangs off. Normally the hip, but if the hip is not one of the
    /// transforms <see cref="HawkTransformSync"/> replicates, the closest synced ancestor is used instead so
    /// the scale can actually reach remote clients. Deterministic from the prefab, so host and client agree.
    /// </summary>
    public static Transform ResolveScaleRoot(PlayerCharacter character)
    {
        var body = character != null ? character.GetComponentInChildren<PlayerBody>(true) : null;
        if (body == null) return null;

        var hipRb = body.GetRigidbody();
        var hip = hipRb ? hipRb.transform : body.transform;

        var sync = character.GetComponent<HawkTransformSync>();
        if (sync == null) return hip;

        for (var t = hip; t != null; t = t.parent)
        {
            if (IsSyncedTransform(sync, t)) return t;
            if (t == sync.transform) break;
        }

        return hip;
    }

    private static bool IsSyncedTransform(HawkTransformSync sync, Transform transform)
    {
        if (sync.transform == transform && sync.mainTransformSyncSetting.settings.IsValid()) return true;

        foreach (var ext in sync.externalTransformsToSync)
        {
            if (ext != null && ext.transform == transform && ext.syncSettings.settings.IsValid()) return true;
        }

        return false;
    }

    /// <summary>
    /// Turns on scale replication for the scale root before <see cref="HawkTransformSync.Setup"/> bakes the
    /// per-transform flags into the outgoing message. Server only: the flags travel with every transform on
    /// the wire, so vanilla clients keep parsing the packet fine, they just ignore the scale.
    /// </summary>
    [HarmonyPatch(typeof(HawkTransformSync), nameof(HawkTransformSync.Setup))]
    [HarmonyPrefix]
    public static void EnableScaleSyncOnSpawn(HawkTransformSync __instance)
    {
        if (__instance.bSetup) return;

        var manager = HawkNetworkManager.DefaultInstance;
        if (manager == null || !manager.IsServer()) return;

        var character = __instance.GetComponent<PlayerCharacter>();
        if (character == null) return;

        var scaleRoot = ResolveScaleRoot(character);
        if (scaleRoot == null) return;
        if ((scaleRoot.localScale - Vector3.one).sqrMagnitude <= 1e-6f) return;

        // Never flip the flag on an entry that syncs nothing: that would make it valid and add a transform to
        // the message, and clients build their entry list from their own prefab. The counts have to match.
        var synced = false;

        if (__instance.transform == scaleRoot && __instance.mainTransformSyncSetting.settings.IsValid())
        {
            __instance.mainTransformSyncSetting.settings.bSyncScale = true;
            synced = true;
        }

        foreach (var ext in __instance.externalTransformsToSync)
        {
            if (ext == null || ext.transform != scaleRoot) continue;
            if (!ext.syncSettings.settings.IsValid()) continue;

            ext.syncSettings.settings.bSyncScale = true;
            synced = true;
        }

        if (!synced && !bWarnedAboutUnsyncedScaleRoot)
        {
            bWarnedAboutUnsyncedScaleRoot = true;
            Plugin.LogSource.LogWarning(
                $"[PlayerScale] '{scaleRoot.name}' is not replicated by HawkTransformSync, so player scale stays local. " +
                $"Synced transforms: {string.Join(", ", SyncedTransformNames(__instance))}");
        }
    }

    private static string[] SyncedTransformNames(HawkTransformSync sync)
    {
        var names = new List<string>();

        if (sync.mainTransformSyncSetting.settings.IsValid()) names.Add(sync.transform.name);

        foreach (var ext in sync.externalTransformsToSync)
        {
            if (ext != null && ext.transform && ext.syncSettings.settings.IsValid()) names.Add(ext.transform.name);
        }

        return names.ToArray();
    }

    /// <summary>
    /// Client side of the same handshake. Vanilla only applies a received scale when the local prefab was
    /// authored to sync it, so the flag the host turned on at runtime would be dropped. Reading it straight
    /// off the received message also means a vanilla host (which sends no scale) can never move us.
    /// </summary>
    [HarmonyPatch(typeof(HawkTransformSync), nameof(HawkTransformSync.OnSnapshot))]
    [HarmonyPostfix]
    public static void ApplySyncedScale(HawkTransformSync __instance, TransformsSyncMessage snapshot)
    {
        var settings = __instance.transformSyncSettings;
        if (settings == null || snapshot == null) return;

        var messages = snapshot.transformsMessages;
        if (messages.Count != settings.Count) return;

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (!message.bSyncScale) continue;

            var setting = settings[i];
            if (setting == null || setting.transform == null) continue;
            if (setting.syncSettings.settings.bSyncScale) continue; // vanilla already lerps this one

            setting.transform.localScale = message.scale;
        }
    }

    private static bool bWarnedAboutUnsyncedScaleRoot;

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
