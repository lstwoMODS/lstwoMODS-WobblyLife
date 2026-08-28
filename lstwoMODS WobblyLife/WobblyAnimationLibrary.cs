using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace lstwoMODS_WobblyLife;

public enum WobblyAnimationSource
{
    /// <summary>Loaded from the Addressables catalog, under the game's NPC animation folder.</summary>
    Npc,
    /// <summary>Pulled off an EmoteDataScriptableObject, which is only reachable once a player exists.</summary>
    Emote,
    /// <summary>Whatever controller a spawned player or NPC was already wearing.</summary>
    Character
}

public class WobblyControllerEntry
{
    public string Name;
    public WobblyAnimationSource Source;

    /// <summary>Null for emotes that play on whatever controller the character already has.</summary>
    public RuntimeAnimatorController Controller;

    /// <summary>Distinct clip names reachable from <see cref="Controller"/>.</summary>
    public string[] ClipNames = Array.Empty<string>();

    /// <summary>EmoteIndex to drive after the swap. Null for plain controllers.</summary>
    public int? EmoteIndex;

    public string Label => $"{Name} ({Source})";
}

public static class WobblyAnimationLibrary
{
    private const string CharacterControllerPath = "/Animation/Game/NPC/";

    private static readonly FieldInfo NpcAnimatorField =
        typeof(PlayerNPCController).GetField("animator", Plugin.Flags);
    private static readonly FieldInfo PlayerAnimatorField =
        typeof(PlayerCharacterAnimController).GetField("playerAnimator", Plugin.Flags);
    private static readonly FieldInfo EmoteDatasField =
        typeof(EmoteDataScriptableObject).GetField("emoteDatas", Plugin.Flags);

    private static readonly List<WobblyControllerEntry> _controllers = new();
    private static readonly Dictionary<string, AnimationClip> _clips = new(StringComparer.Ordinal);
    private static readonly List<AsyncOperationHandle> _heldHandles = new();

    /// <summary>Controller each animator wore before we touched it, so a reset is possible.</summary>
    private static readonly Dictionary<Animator, RuntimeAnimatorController> _defaults = new();

    private static bool _scanning;

    public static bool IsReady { get; private set; }
    public static IReadOnlyList<WobblyControllerEntry> Controllers => _controllers;
    public static IReadOnlyDictionary<string, AnimationClip> Clips => _clips;

    public static string[] ClipNames =>
        _clips.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Fires on the Unity main thread when the one-time scan finishes.</summary>
    public static event Action OnReady;

    /// <summary>
    /// Kicks off the one-time scan. Safe to call repeatedly. Emote data and the default character
    /// controllers only exist once a level with a player character is loaded, so the natural
    /// trigger is <c>GameInstance.onAssignedPlayerCharacter</c>.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (IsReady || _scanning) return;
        _scanning = true;
        Plugin._StartCoroutine(ScanRoutine());
    }

    /// <summary>
    /// Merges in anything that was not loaded during the first scan. Entries dedupe by controller
    /// reference, so re-running is cheap and additive.
    /// </summary>
    public static void Rescan()
    {
        if (_scanning) return;
        IsReady = false;
        EnsureInitialized();
    }

    private static IEnumerator ScanRoutine()
    {
        yield return ScanCatalogAsync();
        ScanLoadedCharacters();

        _scanning = false;
        IsReady = true;

        Plugin.LogSource.LogInfo(
            $"[WobblyAnimationLibrary] {_controllers.Count} controllers, {_clips.Count} clips " +
            $"(asset database initialized: {AssetDatabase.IsInitialized}).");

        try { OnReady?.Invoke(); }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"[WobblyAnimationLibrary] OnReady handler threw: {ex.Message}");
        }
    }

    private static IEnumerator ScanCatalogAsync()
    {
        if (!AssetDatabase.IsInitialized) yield break;

        var entries = AssetDatabase.GetAll().Where(e =>
                (e.ResourceTypeName == "UnityEngine.RuntimeAnimatorController" ||
                 e.ResourceTypeName == "UnityEngine.AnimatorOverrideController") &&
                !string.IsNullOrEmpty(e.Address) &&
                e.Address.IndexOf(CharacterControllerPath, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        foreach (var entry in entries)
        {
            var handle = Addressables.LoadAssetAsync<RuntimeAnimatorController>(entry.LoadKey);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                // Held for the session on purpose: Release destroys the controller and its clips.
                _heldHandles.Add(handle);
                Add(handle.Result, WobblyAnimationSource.Npc,
                    Path.GetFileNameWithoutExtension(entry.Address));
            }
            else if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }

    private static void ScanLoadedCharacters()
    {
        foreach (var emoteDataObject in Resources.FindObjectsOfTypeAll<EmoteDataScriptableObject>())
        {
            if (EmoteDatasField?.GetValue(emoteDataObject) is not EmoteData[] datas) continue;

            foreach (var data in datas)
            {
                if (data == null) continue;

                Add(data.overrideController, WobblyAnimationSource.Emote,
                    string.IsNullOrEmpty(data.key) ? data.emote.ToString() : data.key,
                    (int)data.emote);
            }
        }

        // CharacterCustomize sits on both players and NPCs, so this picks up the default
        // controllers without having to guess at prefab addresses.
        foreach (var customize in Resources.FindObjectsOfTypeAll<CharacterCustomize>())
        {
            var animator = customize.GetComponentInChildren<Animator>(true);
            if (animator != null)
                Add(animator.runtimeAnimatorController, WobblyAnimationSource.Character, null);
        }
    }

    private static void Add(RuntimeAnimatorController controller, WobblyAnimationSource source,
                            string name, int? emoteIndex = null)
    {
        // A null controller is only meaningful for emotes: those play on the current controller.
        if (controller == null && emoteIndex == null) return;

        if (controller != null && _controllers.Any(e => ReferenceEquals(e.Controller, controller))) return;
        if (controller == null && _controllers.Any(e => e.Controller == null && e.Name == name)) return;

        var entry = new WobblyControllerEntry
        {
            Name = string.IsNullOrEmpty(name) ? (controller != null ? controller.name : "Emote") : name,
            Source = source,
            Controller = controller,
            EmoteIndex = emoteIndex
        };

        if (controller != null)
        {
            try
            {
                var names = new List<string>();

                foreach (var clip in controller.animationClips)
                {
                    if (clip == null) continue;
                    if (!_clips.ContainsKey(clip.name)) _clips[clip.name] = clip;
                    if (!names.Contains(clip.name)) names.Add(clip.name);
                }

                entry.ClipNames = names.ToArray();
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogWarning(
                    $"[WobblyAnimationLibrary] Could not read clips off '{entry.Name}': {ex.Message}");
            }
        }

        _controllers.Add(entry);
    }

    /// <summary>The animator that actually drives the wobbly rig, for a player or an NPC root.</summary>
    public static Animator GetAnimator(GameObject character)
    {
        if (character == null) return null;

        var npc = character.GetComponentInChildren<PlayerNPCController>(true);
        if (npc != null && NpcAnimatorField?.GetValue(npc) is Animator npcAnimator && npcAnimator != null)
            return npcAnimator;

        var animController = character.GetComponentInChildren<PlayerCharacterAnimController>(true);
        if (animController != null &&
            PlayerAnimatorField?.GetValue(animController) is Animator playerAnimator && playerAnimator != null)
            return playerAnimator;

        return character.GetComponentInChildren<Animator>(true);
    }

    /// <summary>
    /// Hands the animator over to us. Two things otherwise keep a spawned NPC frozen no matter
    /// which controller it wears:
    ///
    /// 1. <c>AnimatorCullingGroup</c> owns <c>animator.enabled</c>. It disables the animator in
    ///    Awake and only ever re-enables it from a culling callback, and its bounding sphere uses a
    ///    position baked at edit time (BakeEditor sets bIsStatic for anything without a
    ///    WorldDynamicObject or HawkTransformSync among its parents, which the NPC prefabs are).
    ///    A runtime-spawned NPC therefore has its culling sphere sitting wherever the prefab was
    ///    authored rather than where the NPC is, so the visible mask never turns on and the
    ///    animator is held at disabled forever.
    /// 2. <c>PlayerNPCController</c> only enables the animator and seeds CycleOffset on its first
    ///    SetAnimation, and SetAnimation early-outs whenever the value has not changed. A default
    ///    NPCAnimation equals the field's initial value, so the obvious call does nothing.
    /// </summary>
    public static Animator PrepareAnimator(GameObject character)
    {
        var animator = GetAnimator(character);
        if (animator == null) return null;

        foreach (var cullingGroup in character.GetComponentsInChildren<AnimatorCullingGroup>(true))
        {
            if (!cullingGroup.enabled) continue;

            // OnDisable unassigns it from AnimatorCullingManager, so nothing writes enabled again.
            cullingGroup.enabled = false;
            Plugin.LogSource.LogInfo(
                $"[WobblyAnimationLibrary] Disabled AnimatorCullingGroup on '{cullingGroup.name}'; " +
                "it was holding the animator disabled.");
        }

        ForceAnimationInit(character);
        animator.enabled = true;
        return animator;
    }

    /// <summary>
    /// Re-pushes the NPC's current NPCAnimation with bForce, which is what actually enables the
    /// animator and writes the parameters. Needed after every controller swap too, because a swap
    /// resets all parameters while PlayerNPCController's cached struct stays put, so the next
    /// ordinary SetAnimation would early-out and leave the new controller with default parameters.
    /// </summary>
    private static void ForceAnimationInit(GameObject character)
    {
        var npc = character != null ? character.GetComponentInChildren<PlayerNPCController>(true) : null;
        if (npc == null) return;

        npc.SetAnimation(npc.GetPlayerNPCAnimation(), false, true);
    }

    /// <summary>
    /// Swaps the character's controller and, for emote entries, fires the emote. Local only:
    /// no part of a controller swap is replicated, so everyone else keeps seeing the default.
    /// </summary>
    public static bool ApplyController(GameObject character, WobblyControllerEntry entry)
    {
        if (entry == null) return false;

        var animator = PrepareAnimator(character);
        if (animator == null)
        {
            Plugin.LogSource.LogWarning("[WobblyAnimationLibrary] ApplyController: no animator found.");
            return false;
        }

        PruneDefaults();

        var previous = animator.runtimeAnimatorController;
        var swapped = false;

        if (entry.Controller != null && !ReferenceEquals(previous, entry.Controller))
        {
            if (!_defaults.ContainsKey(animator))
                _defaults[animator] = previous;

            animator.runtimeAnimatorController = entry.Controller;
            ForceAnimationInit(character);
            swapped = true;
        }

        animator.enabled = true;

        var emoteDriven = false;

        if (entry.EmoteIndex.HasValue && HasParameter(animator, "EmoteIndex") && HasParameter(animator, "tEmote"))
        {
            animator.SetInteger("EmoteIndex", entry.EmoteIndex.Value);
            animator.SetTrigger("tEmote");
            emoteDriven = true;
        }

        animator.Update(Time.deltaTime);

        // Without this, every no-op case (same controller, emote parameters the NPC controller does
        // not have) is indistinguishable from a broken swap.
        Plugin.LogSource.LogInfo(
            $"[WobblyAnimationLibrary] ApplyController '{entry.Label}' on animator '{animator.name}': " +
            $"swapped={swapped} (was '{(previous != null ? previous.name : "none")}'), " +
            $"emoteDriven={emoteDriven}, " +
            $"clipsOnController={animator.runtimeAnimatorController?.animationClips?.Length ?? 0}");

        if (!swapped && !emoteDriven)
            Plugin.LogSource.LogWarning(
                $"[WobblyAnimationLibrary] '{entry.Label}' changed nothing: " +
                (entry.Controller == null
                    ? "it is an emote that needs EmoteIndex/tEmote, which this controller does not have."
                    : "the animator was already wearing that controller."));

        return true;
    }

    /// <summary>Puts back the controller the animator wore before the first swap or clip override.</summary>
    public static bool RestoreController(GameObject character)
    {
        var animator = GetAnimator(character);
        if (animator == null || !_defaults.TryGetValue(animator, out var original)) return false;

        animator.runtimeAnimatorController = original;
        _defaults.Remove(animator);
        animator.Update(Time.deltaTime);
        return true;
    }

    /// <summary>
    /// Clip slots that can be overridden on the character's current controller. These are the
    /// original clip names, which is what <see cref="AnimatorOverrideController"/> keys on.
    /// </summary>
    public static string[] GetOverrideSlots(GameObject character)
    {
        var animator = GetAnimator(character);
        var controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null) return Array.Empty<string>();

        if (controller is AnimatorOverrideController existing)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(existing.overridesCount);
            existing.GetOverrides(pairs);
            return pairs.Where(p => p.Key != null).Select(p => p.Key.name).Distinct().ToArray();
        }

        return controller.animationClips.Where(c => c != null).Select(c => c.name).Distinct().ToArray();
    }

    /// <summary>
    /// Substitutes <paramref name="replacement"/> for the clip named <paramref name="slot"/>,
    /// wrapping the current controller in an <see cref="AnimatorOverrideController"/> the first
    /// time. Same trick the game plays with NPCController_WalkWithSound, and the only way to get an
    /// arbitrary clip onto an NPC: the existing state machine keeps driving, it just plays a
    /// different clip. A clip authored for another rig will bind to nothing and the NPC will
    /// simply stop moving.
    /// </summary>
    public static bool OverrideClip(GameObject character, string slot, AnimationClip replacement)
    {
        if (replacement == null || string.IsNullOrEmpty(slot)) return false;

        var animator = PrepareAnimator(character);
        if (animator == null) return false;

        var current = animator.runtimeAnimatorController;
        if (current == null) return false;

        PruneDefaults();

        if (current is not AnimatorOverrideController aoc)
        {
            if (!_defaults.ContainsKey(animator))
                _defaults[animator] = current;

            aoc = new AnimatorOverrideController(current) { name = current.name + " (lstwoMODS)" };
            animator.runtimeAnimatorController = aoc;
        }

        // Setting an unknown key logs a Unity error, so check the originals first.
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
        aoc.GetOverrides(pairs);
        if (!pairs.Any(p => p.Key != null && p.Key.name == slot)) return false;

        aoc[slot] = replacement;
        ForceAnimationInit(character);
        animator.Update(Time.deltaTime);
        return true;
    }

    /// <summary>The poses the vanilla NPC controller exposes, in <see cref="NPCAnimation"/> order.</summary>
    public static readonly string[] PoseNames =
    {
        "Idle", "Walking", "Wave", "Look Around", "Hold Arm Out",
        "Hold Object Up", "Sit Down", "Saddle", "Drive"
    };

    /// <summary>Builds the flag struct for a <see cref="PoseNames"/> index.</summary>
    public static NPCAnimation BuildPose(int poseIndex, int bodyIndex)
    {
        var animation = new NPCAnimation { fullBodyIndex = (byte)Mathf.Clamp(bodyIndex, 0, 255) };

        switch (poseIndex)
        {
            case 1: animation.bWalking = true; break;
            case 2: animation.bWave = true; break;
            case 3: animation.bLookAround = true; break;
            case 4: animation.bHoldArmOut = true; break;
            case 5: animation.bHoldObjectUp = true; break;
            case 6: animation.bSitdown = true; break;
            case 7: animation.bSaddle = true; break;
            case 8: animation.bDrive = true; break;
        }

        return animation;
    }

    /// <summary>
    /// The only replicated animation path. PlayerNPCController.SetAnimation sends its RPC only when
    /// the caller is the server, so on a client this stays a local visual change.
    ///
    /// Forced, because an unforced call early-outs when the struct is unchanged: selecting Idle at
    /// body index 0 on a fresh NPC is exactly the field's initial value, which would otherwise skip
    /// the branch that enables the animator in the first place.
    /// </summary>
    public static bool ApplyPose(GameObject npc, NPCAnimation animation)
    {
        var controller = npc != null ? npc.GetComponentInChildren<PlayerNPCController>(true) : null;
        if (controller == null) return false;

        PrepareAnimator(npc);
        controller.SetAnimation(animation, true, true);
        return true;
    }

    /// <summary>Clip names known to bind, per animator instance id.</summary>
    private static readonly Dictionary<int, HashSet<string>> _bindingCache = new();

    /// <summary>
    /// Whether a clip's curves resolve against this rig.
    ///
    /// Clips are keyed by transform path relative to the animator's GameObject, and those paths
    /// cannot be read at runtime (AnimationUtility is editor only), so the only way to know is to
    /// sample the clip onto the hierarchy and look for movement. The rig is snapshotted and put
    /// back afterwards, so this is non-destructive.
    /// </summary>
    public static bool ClipBindsTo(Animator animator, AnimationClip clip)
    {
        if (animator == null || clip == null) return false;

        var root = animator.gameObject;
        var transforms = root.GetComponentsInChildren<Transform>(true);

        var positions = new Vector3[transforms.Length];
        var rotations = new Quaternion[transforms.Length];
        var scales = new Vector3[transforms.Length];

        for (var i = 0; i < transforms.Length; i++)
        {
            positions[i] = transforms[i].localPosition;
            rotations[i] = transforms[i].localRotation;
            scales[i] = transforms[i].localScale;
        }

        var bound = false;

        try
        {
            // Both samples compare against the rest pose, so a clip that is static across its
            // length still registers as long as its pose differs from rest.
            bound = SampleMoves(root, clip, transforms, positions, rotations, scales, 0f) ||
                    SampleMoves(root, clip, transforms, positions, rotations, scales, clip.length * 0.5f);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"[WobblyAnimationLibrary] Could not sample '{clip.name}': {ex.Message}");
        }
        finally
        {
            for (var i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = positions[i];
                transforms[i].localRotation = rotations[i];
                transforms[i].localScale = scales[i];
            }
        }

        return bound;
    }

    private static bool SampleMoves(GameObject root, AnimationClip clip, Transform[] transforms,
                                    Vector3[] positions, Quaternion[] rotations, Vector3[] scales, float time)
    {
        clip.SampleAnimation(root, time);

        for (var i = 0; i < transforms.Length; i++)
        {
            if ((transforms[i].localPosition - positions[i]).sqrMagnitude > 1e-8f) return true;
            if (Quaternion.Angle(transforms[i].localRotation, rotations[i]) > 0.01f) return true;
            if ((transforms[i].localScale - scales[i]).sqrMagnitude > 1e-8f) return true;
        }

        return false;
    }

    /// <summary>
    /// Clip names that actually bind to this character's rig, cached per animator. Clips are keyed
    /// by name here, so two same-named clips from different controllers collapse into one entry.
    /// </summary>
    public static string[] GetCompatibleClipNames(GameObject character)
    {
        var animator = GetAnimator(character);
        if (animator == null) return Array.Empty<string>();

        var key = animator.GetInstanceID();

        if (!_bindingCache.TryGetValue(key, out var bound))
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bound = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in _clips)
                if (ClipBindsTo(animator, pair.Value)) bound.Add(pair.Key);

            _bindingCache[key] = bound;

            Plugin.LogSource.LogInfo(
                $"[WobblyAnimationLibrary] Probed {_clips.Count} clips against '{animator.name}': " +
                $"{bound.Count} bind, {_clips.Count - bound.Count} rejected " +
                $"({stopwatch.ElapsedMilliseconds} ms).");
        }

        return bound.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// A controller is usable on this character when at least one of its clips resolves against the
    /// rig. Emote entries carry no controller of their own, so for those the test is whether the
    /// parameters they drive exist at all.
    /// </summary>
    public static bool IsControllerCompatible(GameObject character, WobblyControllerEntry entry)
    {
        if (entry == null) return false;

        var animator = GetAnimator(character);
        if (animator == null) return false;

        if (entry.Controller == null)
            return entry.EmoteIndex.HasValue &&
                   HasParameter(animator, "EmoteIndex") &&
                   HasParameter(animator, "tEmote");

        if (entry.ClipNames.Length == 0) return false;

        var bound = GetCompatibleClipNames(character);

        foreach (var name in entry.ClipNames)
            if (Array.IndexOf(bound, name) >= 0) return true;

        return false;
    }

    /// <summary>Controllers worth showing for this character, in library order.</summary>
    public static List<WobblyControllerEntry> GetCompatibleControllers(GameObject character) =>
        _controllers.Where(e => IsControllerCompatible(character, e)).ToList();

    /// <summary>Total clip count, so the UI can say how many were filtered out.</summary>
    public static int ClipCount => _clips.Count;

    /// <summary>A scene change destroys the animators we remembered. Drop the dead keys.</summary>
    private static void PruneDefaults()
    {
        var dead = _defaults.Keys.Where(a => a == null).ToList();
        foreach (var key in dead) _defaults.Remove(key);
    }

    private static bool HasParameter(Animator animator, string name)
    {
        foreach (var parameter in animator.parameters)
            if (parameter.name == name) return true;

        return false;
    }
}
