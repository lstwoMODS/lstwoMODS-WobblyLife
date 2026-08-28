using System;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Mods;

public class NpcManager : PlayerBasedMod
{
    public override string Name => "NPC Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    //public static string NpcGuid = "14fb04f12bc2f5440b0b6052b6c46af7";

    public static AllClothingAssetReferences AllClothingAssetReferences => ClothingManager.Instance.allClothingAssetReferences;

    public static List<GameObject> SpawnedNpcs = [];

    private static Ref<int> _currentSelectedNpcIndex = new();
    private static Ref<string[]> _dropdownNpcItems = new([]);
    private static Ref<bool> _isNpcSelectionInvalid = new(true);

    private Ref<string> npcNameInput = new("New NPC");

    private readonly Ref<int> _poseIndex = new(0);
    private readonly Ref<int> _bodyIndex = new(0);
    private readonly Ref<string[]> _controllerItems = new([]);
    private readonly Ref<int> _controllerIndex = new(0);
    private readonly Ref<string[]> _slotItems = new([]);
    private readonly Ref<int> _slotIndex = new(0);
    private readonly Ref<string[]> _clipItems = new([]);
    private readonly Ref<int> _clipIndex = new(0);
    private readonly Ref<string> _compatibilityText = new("");

    /// <summary>Backs the controller dropdown: it is filtered, so its indices are not library indices.</summary>
    private List<WobblyControllerEntry> _compatibleControllers = new();

    private static GameObject SelectedNpc =>
        _currentSelectedNpcIndex.Value >= 0 && _currentSelectedNpcIndex.Value < SpawnedNpcs.Count
            ? SpawnedNpcs[_currentSelectedNpcIndex.Value]
            : null;

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new SeparatorText("Spawn NPC", "Spawn NPC"),
            
            new InputText("NPC Name").WithValue(npcNameInput),
            new Button("Spawn New NPC", () =>
            {
                var playerBody = GameInstance.Instance.GetFirstLocalPlayerController();
            
                SpawnNewNpc(behaviour =>
                {
                    AddNpcToList(behaviour.gameObject, npcNameInput.Value);
                    npcNameInput.Value = "New NPC (" + _dropdownNpcItems.Value.Length + ")";
                    _currentSelectedNpcIndex.Value = _dropdownNpcItems.Value.Length - 1;
                    behaviour.transform.position = playerBody.transform.position;

                    RefreshUI();

                }, playerBody.transform.position, bSendTransform: true);
            }),
            
            new SeparatorText("Edit NPC", "Edit NPC"),
            
            new Combo("Select NPC", [], 0, _ => RefreshUI()).WithItems(_dropdownNpcItems).WithSelectedIndex(_currentSelectedNpcIndex),
            new Spacing("spacer"),
            
            new UIText("No NPC Selected", "No NPC Selected").WithVisible(_isNpcSelectionInvalid),
            new Group("NPC Settings",
                
                new Button("Transfer Clothing to NPC", () =>
                {
                    var playerCustomize = Player.CharacterCustomize;
                    var npcCustomize = SpawnedNpcs[_currentSelectedNpcIndex.Value].GetComponent<CharacterCustomize>();

                    if (playerCustomize.GetClothingHat())
                    {
                        var hatReference = AllClothingAssetReferences.GetClothing(playerCustomize.GetClothingHat().GetGuid());
                        npcCustomize.SetClothingPiece(hatReference, ClothingSelectionType.Hat, data: new ClothingPieceData { clothingPrimaryColor = playerCustomize.GetClothingHat().GetPrimaryColor() });
                    }
            
                    if (playerCustomize.GetClothingTop())
                    {
                        var hatReference = AllClothingAssetReferences.GetClothing(playerCustomize.GetClothingTop().GetGuid());
                        npcCustomize.SetClothingPiece(hatReference, ClothingSelectionType.Top, data: new ClothingPieceData { clothingPrimaryColor = playerCustomize.GetClothingTop().GetPrimaryColor() });
                    }
            
                    if (playerCustomize.GetClothingBottom())
                    {
                        var hatReference = AllClothingAssetReferences.GetClothing(playerCustomize.GetClothingBottom().GetGuid());
                        npcCustomize.SetClothingPiece(hatReference, ClothingSelectionType.Bottom, data: new ClothingPieceData { clothingPrimaryColor = playerCustomize.GetClothingBottom().GetPrimaryColor() });
                    }
                    
                }).WithContentWidth(),
                
                new Button("Teleport NPC to Player", () =>
                {
                    SpawnedNpcs[_currentSelectedNpcIndex.Value]?.transform.position = Player.Character.GetPlayerBody().transform.position;
                    
                }).WithContentWidth(),
                
                new Button("Inspect NPC Object", () =>
                {
                    InspectorManager.Inspect(SpawnedNpcs[_currentSelectedNpcIndex.Value]);
                    UnityExplorer.UI.UIManager.ShowMenu = true;

                }).WithContentWidth(),

                new SeparatorText("NPC Animation", "Animation"),
                
                new Checkbox("Enable Animator", true, b => SelectedNpc?.GetComponent<PlayerNPCController>()?.animator?.enabled = b),

                new Combo("Pose", WobblyAnimationLibrary.PoseNames, 0, _ => ApplySelectedPose())
                    .WithSelectedIndex(_poseIndex)
                    .WithTooltip("Vanilla NPC poses. The only path that replicates, and only while you are the host."),

                new InputInt("Body Index", 0, onValueChanged: _ => ApplySelectedPose())
                    .WithValue(_bodyIndex)
                    .WithTooltip("Full body animation index inside NPCController."),

                new TextDisabled("Animation Compatibility", "").WithText(_compatibilityText),

                new SearchableCombo("Animator Controller", [], 0, _ => ApplySelectedController())
                    .WithItems(_controllerItems).WithSelectedIndex(_controllerIndex)
                    .WithTooltip("Swaps the whole controller. Filtered to controllers whose clips bind to this " +
                                 "NPC's rig. Local only: other clients keep seeing the default."),

                new Button("Reset Controller", () =>
                {
                    WobblyAnimationLibrary.RestoreController(SelectedNpc);
                    ApplySelectedPose();
                    RefreshAnimationLists();

                }).WithContentWidth(),

                new SearchableCombo("Replace Clip", [])
                    .WithItems(_slotItems).WithSelectedIndex(_slotIndex)
                    .WithTooltip("Clip slot on the NPC's current controller."),

                new SearchableCombo("With Clip", [])
                    .WithItems(_clipItems).WithSelectedIndex(_clipIndex)
                    .WithTooltip("Clips whose curves resolve against this NPC's bone hierarchy. Clips authored " +
                                 "for a rig with a different layout are filtered out because they would bind to " +
                                 "nothing and freeze the NPC."),

                new Button("Apply Clip Override", ApplySelectedClipOverride).WithContentWidth()

            ).WithDisabled(_isNpcSelectionInvalid)
        );
    }

    public override void RefreshUI()
    {
        _isNpcSelectionInvalid.Value = SpawnedNpcs.Count == 0 || _currentSelectedNpcIndex.Value >= SpawnedNpcs.Count;

        WobblyAnimationLibrary.EnsureInitialized();
        RefreshAnimationLists();
    }

    protected override void Awake()
    {
        if (IsDetached) return;

        WobblyAnimationLibrary.OnReady += () => MainThread.Enqueue(RefreshAnimationLists);
    }

    private void RefreshAnimationLists()
    {
        if (!WobblyAnimationLibrary.IsReady) return;

        var npc = SelectedNpc;

        _compatibleControllers = npc != null
            ? WobblyAnimationLibrary.GetCompatibleControllers(npc)
            : new List<WobblyControllerEntry>();

        var previousControllers = _controllerItems.Value;
        var previousClips = _clipItems.Value;
        var previousSlots = _slotItems.Value;

        var controllers = _compatibleControllers.Select(c => c.Label).ToArray();
        if (!SameItems(previousControllers, controllers))
            _controllerItems.Value = controllers;

        var clips = npc != null ? WobblyAnimationLibrary.GetCompatibleClipNames(npc) : [];
        if (!SameItems(previousClips, clips))
            _clipItems.Value = clips;

        var slots = npc != null ? WobblyAnimationLibrary.GetOverrideSlots(npc) : [];
        if (!SameItems(previousSlots, slots))
            _slotItems.Value = slots;

        PreserveSelection(_controllerIndex, previousControllers, controllers);
        PreserveSelection(_clipIndex, previousClips, clips);
        PreserveSelection(_slotIndex, previousSlots, slots);

        _compatibilityText.Value = npc == null
            ? ""
            : $"{controllers.Length} of {WobblyAnimationLibrary.Controllers.Count} controllers and " +
              $"{clips.Length} of {WobblyAnimationLibrary.ClipCount} clips bind to this NPC's rig.";
    }

    /// <summary>
    /// Keeps a dropdown pointing at the same entry when its list changes underneath it.
    ///
    /// The controller list is deliberately live: emote entries carry no controller of their own and
    /// are only usable when the animator's *current* controller has EmoteIndex and tEmote, so
    /// swapping the controller genuinely changes which entries can work. Re-finding the selection
    /// by label is what stops that from silently dragging the selection to index 0.
    /// </summary>
    private static void PreserveSelection(Ref<int> index, string[] previous, string[] current)
    {
        var selected = previous != null && index.Value >= 0 && index.Value < previous.Length
            ? previous[index.Value]
            : null;

        if (selected != null)
        {
            var found = Array.IndexOf(current, selected);
            if (found >= 0)
            {
                index.Value = found;
                return;
            }
        }

        if (index.Value >= current.Length || index.Value < 0)
            index.Value = 0;
    }

    private void ApplySelectedPose()
    {
        var npc = SelectedNpc;
        if (npc == null) return;

        WobblyAnimationLibrary.ApplyPose(npc,
            WobblyAnimationLibrary.BuildPose(_poseIndex.Value, _bodyIndex.Value));
    }

    private void ApplySelectedController()
    {
        var npc = SelectedNpc;
        var index = _controllerIndex.Value;
        if (npc == null || index < 0 || index >= _compatibleControllers.Count) return;

        WobblyAnimationLibrary.ApplyController(npc, _compatibleControllers[index]);

        // The swap zeroed every parameter, so put the selected pose back on the new controller.
        ApplySelectedPose();
        RefreshAnimationLists();
    }

    private void ApplySelectedClipOverride()
    {
        var npc = SelectedNpc;
        if (npc == null) return;

        var slots = _slotItems.Value;
        var clips = _clipItems.Value;

        if (_slotIndex.Value < 0 || _slotIndex.Value >= slots.Length) return;
        if (_clipIndex.Value < 0 || _clipIndex.Value >= clips.Length) return;
        if (!WobblyAnimationLibrary.Clips.TryGetValue(clips[_clipIndex.Value], out var clip)) return;

        WobblyAnimationLibrary.OverrideClip(npc, slots[_slotIndex.Value], clip);
        RefreshAnimationLists();
    }

    /// <summary>Manual compare: Enumerable.SequenceEqual binds to the span overload and NREs under Mono.</summary>
    private static bool SameItems(string[] a, string[] b)
    {
        if (a == null || b == null) return ReferenceEquals(a, b);
        if (a.Length != b.Length) return false;

        for (var i = 0; i < a.Length; i++)
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;

        return true;
    }

    [ModAction(ShowInUI = false)]
    public static void SpawnNewNpc(Action<HawkNetworkBehaviour> callback = null, Vector3? position = null, 
        Quaternion? rotation = null, HawkConnection owner = null, bool bUseChunkSystem = true, 
        bool bSendTransform = true, bool bCheckChunk = false, bool bMarkAsReplaced = true)
    {
        NetworkPrefab.SpawnNetworkPrefab("NPC World", callback, position, rotation, owner, bUseChunkSystem, bSendTransform, bCheckChunk, bMarkAsReplaced);
    }

    public static void AddNpcToList(GameObject npc, string name)
    {
        SpawnedNpcs.Add(npc);

        var dropdownList = _dropdownNpcItems.Value.ToList();
        dropdownList.Add(name);
        _dropdownNpcItems.Value = dropdownList.ToArray();
    }
}