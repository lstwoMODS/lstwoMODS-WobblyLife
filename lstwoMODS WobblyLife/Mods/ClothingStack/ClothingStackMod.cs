using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HawkNetworking;
using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

public class ClothingStackMod : BaseMod
{
    public override string Name => "Clothing Stacking";
    public override string Description =>
        "Layer multiple clothing pieces at once. Others see it only if they and the host also run lstwoMODS.";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    private const string SaveKey = "layers";
    private const string EnabledKey = "enabled";
    private const float ReconcileInterval = 0.5f;
    private const float HeartbeatInterval = 2f;

    private static ClothingStackMod _instance;

    private readonly List<StackLayer> _layers = new();
    private readonly Ref<bool> _enabled = new();

    private readonly Ref<string[]> _layerNames = new([]);
    private readonly Ref<int> _selectedLayer = new();

    private readonly ClothingStackApplier _localApplier = new();
    private readonly ClothingStackApplier _previewApplier = new();

    private Wardrobe _activeWardrobe;

    private static readonly Dictionary<ulong, List<StackLayer>> RemoteDesired = new();
    private static readonly Dictionary<ulong, ClothingStackApplier> RemoteAppliers = new();
    private static readonly List<StackLayer> EmptyLayers = new();

    private static readonly ClothingSelectionType[] Slots =
    {
        ClothingSelectionType.Hat, ClothingSelectionType.Top,
        ClothingSelectionType.Bottom, ClothingSelectionType.Outfit
    };

    private readonly HashSet<ulong> _seenPlayers = new();
    private readonly List<ulong> _stalePlayers = new();

    private float _nextReconcile;
    private float _nextHeartbeat;

    protected override void OnStaticInit()
    {
        ClothingStackNetworking.Initialize();
        ClothingStackNetworking.StackReceived += OnStackReceived;
        new Harmony("lstwo.ClothingStack.Wardrobe").PatchAll(typeof(ClothingStackWardrobePatch));
    }

    protected override void Awake()
    {
        _instance = this;

        BindData(_enabled, EnabledKey, false);
        _enabled.Changed += _ => MainThread.Enqueue(ApplyAndBroadcast);

        var loaded = LoadData<List<StackLayer>>(SaveKey);
        if (loaded != null)
            _layers.AddRange(loaded.Where(l => l != null && !string.IsNullOrEmpty(l.Guid)));
        RefreshLayerNames();
        ClothingManagerMod.AllClothingNames.Changed += _ => RefreshLayerNames();
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new Checkbox("Enable Wardrobe Stacking", _enabled.Value).WithValue(_enabled),
            new UIText("StackHelp",
                "While enabled, open any wardrobe and click a clothing piece to add it to your stack " +
                "instead of switching. Click the empty (None) option to clear that slot."),

            new SeparatorText("Current Layers", "Current Layers"),
            new SearchableCombo("Selected Layer", [])
                .WithItems(_layerNames).WithSelectedIndex(_selectedLayer),
            new HStack("Layer Actions",
                ActionMenu(new Button("Remove Selected Layer", RemoveSelectedLayer), nameof(RemoveSelectedLayer)),
                ActionMenu(new Button("Clear All Layers", ClearLayers), nameof(ClearLayers))
            ).WithContentWidth()
        );
    }



    /// <summary>Add a piece to the stack from a wardrobe click (real piece, not the None option).</summary>
    private void AddLayerFromWardrobe(Guid guid, Color? color)
    {
        if (guid == Guid.Empty) return;
        if (_layers.Count >= ClothingStackCodec.MaxLayers)
        {
            Plugin.LogSource.LogWarning($"[ClothingStack] Layer limit ({ClothingStackCodec.MaxLayers}) reached.");
            return;
        }

        var layer = new StackLayer { Guid = guid.ToString() };
        if (color.HasValue)
        {
            // A non-negative alpha marks the colour as an explicit override for the applier/codec.
            layer.R = color.Value.r;
            layer.G = color.Value.g;
            layer.B = color.Value.b;
            layer.A = Mathf.Max(0f, color.Value.a);
        }

        _layers.Add(layer);
        Persist();
        RefreshLayerNames();
        ApplyAndBroadcast();
    }

    /// <summary>Remove every stacked layer belonging to a slot (the None option was clicked for it).</summary>
    private void ClearSlotFromWardrobe(ClothingSelectionType selectionType)
    {
        if (selectionType == ClothingSelectionType.None) return;

        var removed = _layers.RemoveAll(l => CategoryOf(l.Guid) == selectionType);
        if (removed == 0) return;

        Persist();
        RefreshLayerNames();
        ApplyAndBroadcast();
    }

    [ModAction(ShowInUI = false)]
    public void RemoveSelectedLayer()
    {
        var i = _selectedLayer.Value;
        if (i < 0 || i >= _layers.Count) return;

        _layers.RemoveAt(i);
        Persist();
        RefreshLayerNames();
        ApplyAndBroadcast();
    }

    private void ReplaceLayers(List<StackLayer> layers)
    {
        _layers.Clear();
        if (layers != null)
            _layers.AddRange(layers
                .Where(l => l != null && !string.IsNullOrEmpty(l.Guid))
                .Select(CloneLayer));

        _enabled.Value = _layers.Count > 0;

        Persist();
        RefreshLayerNames();
        ApplyAndBroadcast();
    }

    private static StackLayer CloneLayer(StackLayer l) => new()
    {
        Guid = l.Guid, R = l.R, G = l.G, B = l.B, A = l.A
    };

    [ModAction(ShowInUI = false)]
    public void ClearLayers()
    {
        if (_layers.Count == 0) return;

        _layers.Clear();
        Persist();
        RefreshLayerNames();
        ApplyAndBroadcast();
    }



    public override void Update()
    {
        if (!GameInstance.InstanceExists) return;
        var instance = GameInstance.Instance;
        if (instance == null) return;

        var now = Time.time;

        if (now >= _nextReconcile)
        {
            _nextReconcile = now + ReconcileInterval;
            ReconcileLocal(instance);
            ReconcileRemote(instance);
        }

        // Mirror onto the wardrobe preview every frame so clicks feel responsive.
        ReconcilePreview();

        if (now >= _nextHeartbeat)
        {
            _nextHeartbeat = now + HeartbeatInterval;
            BroadcastLocal();
        }
    }

    /// <summary>The layers we should currently be showing: none while the mod is disabled.</summary>
    private List<StackLayer> Desired => _enabled.Value ? _layers : EmptyLayers;

    private void ReconcileLocal(GameInstance instance)
    {
        var controller = instance.GetFirstLocalPlayerController();
        var customize = controller?.GetPlayerCharacter()?.GetPlayerCharacterCustomize();

        // Keep each slot's bottom piece a real, saved clothing piece. Skip while a wardrobe is open:
        // it owns base editing and persists the base on Apply (see the TryOn prefix).
        if (_enabled.Value && _activeWardrobe == null && controller != null && customize)
            PromoteOrphanLayers(controller, customize);

        _localApplier.Reconcile(customize, Desired);
    }

    /// <summary>
    /// Model invariant: the lowest layer of every slot is a real clothing piece in the actual vanilla
    /// slot. When a slot has stacked layers but no real base piece (e.g. after loading an outfit whose
    /// base slot was empty), move the lowest such layer into the real slot via the game's own clothes
    /// path so it saves to the Wobbly Life save file and networks to everyone. The rest stay as layers.
    /// </summary>
    private void PromoteOrphanLayers(PlayerController controller, CharacterCustomize customize)
    {
        if (_layers.Count == 0) return;

        var clothes = customize.GetClothesData();
        var changed = false;

        foreach (var slot in Slots)
        {
            if (!SlotBaseEmpty(clothes, slot)) continue;

            var index = _layers.FindIndex(l => CategoryOf(l.Guid) == slot);
            if (index < 0) continue;

            clothes = WithSlot(clothes, slot, _layers[index]);
            _layers.RemoveAt(index);
            changed = true;
        }

        if (!changed) return;

        customize.SetClothesData(clothes);
        var persistent = controller.GetPlayerPersistentData();
        if (persistent != null) persistent.CurrentClothes = clothes;
        controller.ClientUpdateClothes(clothes);

        Persist();
        RefreshLayerNames();
        BroadcastLocal();
    }

    private void ReconcilePreview()
    {
        CharacterCustomize preview = null;
        if (_enabled.Value && _activeWardrobe && _activeWardrobe.characterUI)
            preview = _activeWardrobe.characterUI.GetCharacterCustomize();

        _previewApplier.Reconcile(preview, Desired);
    }

    private void ReconcileRemote(GameInstance instance)
    {
        var controllers = instance.GetPlayerControllers();
        if (controllers == null) return;

        _seenPlayers.Clear();
        for (var i = 0; i < controllers.Count; i++)
        {
            var pc = controllers[i];
            if (!pc || pc.IsLocal()) continue;

            var sid = SteamIdOf(pc.networkObject?.GetOwner());
            if (sid == 0) continue;

            _seenPlayers.Add(sid);
            var customize = pc.GetPlayerCharacter()?.GetPlayerCharacterCustomize();
            RemoteDesired.TryGetValue(sid, out var desired);
            GetRemoteApplier(sid).Reconcile(customize, desired ?? EmptyLayers);
        }

        if (RemoteAppliers.Count == 0) return;
        _stalePlayers.Clear();
        foreach (var kv in RemoteAppliers)
            if (!_seenPlayers.Contains(kv.Key)) _stalePlayers.Add(kv.Key);
        for (var i = 0; i < _stalePlayers.Count; i++)
        {
            var sid = _stalePlayers[i];
            RemoteAppliers[sid].Clear();
            RemoteAppliers.Remove(sid);
            RemoteDesired.Remove(sid);
        }
    }

    private void ApplyAndBroadcast()
    {
        if (GameInstance.InstanceExists && GameInstance.Instance != null)
            ReconcileLocal(GameInstance.Instance);
        BroadcastLocal();
    }

    private void BroadcastLocal()
    {
        if (!ClothingStackNetworking.IsReady()) return;
        ClothingStackNetworking.Broadcast(ClothingStackCodec.Encode(Desired));
    }



    private static void OnStackReceived(ulong sid, string payload)
    {
        if (sid == 0) return;
        RemoteDesired[sid] = ClothingStackCodec.Decode(payload);
    }

    private static ClothingStackApplier GetRemoteApplier(ulong sid)
    {
        if (!RemoteAppliers.TryGetValue(sid, out var applier))
        {
            applier = new ClothingStackApplier();
            RemoteAppliers[sid] = applier;
        }
        return applier;
    }

    private static ulong SteamIdOf(HawkConnection connection)
        => connection is SteamConnection sc ? sc.steamId.Value : 0UL;



    public static bool StackingEnabled => _instance != null && _instance._enabled.Value;

    /// <summary>
    /// True when a wardrobe pick should clear its slot instead of layering. That's the blank
    /// GUID or one of the invisible "None" pieces the game wears to mean "nothing" (named
    /// <c>_Hat_None</c>, <c>_Top_None</c>, ...), which must never end up as a stacked layer.
    /// </summary>
    internal static bool WardrobeIsClearPiece(Guid guid) => _instance != null && _instance.IsNonePiece(guid);

    internal static void WardrobeAddLayer(Guid guid, Color? color) => _instance?.AddLayerFromWardrobe(guid, color);

    /// <summary>
    /// True when the wardrobe's slot has no real clothing piece yet, so the next pick should go into
    /// the actual vanilla slot (becoming the saved bottom piece) instead of stacking on top.
    /// </summary>
    internal static bool WardrobeSlotBaseEmpty(Wardrobe wardrobe, ClothingSelectionType slot)
        => _instance == null || _instance.IsWardrobeSlotBaseEmpty(wardrobe, slot);

    public static List<StackLayer> GetWornLayers()
        => _instance != null && _instance._enabled.Value
            ? _instance._layers.Select(CloneLayer).ToList()
            : new List<StackLayer>();

    public static void ApplyLayers(List<StackLayer> layers) => _instance?.ReplaceLayers(layers);

    internal static void WardrobeClearSlot(ClothingSelectionType selectionType)
        => _instance?.ClearSlotFromWardrobe(selectionType);

    internal static void SetActiveWardrobe(Wardrobe wardrobe)
    {
        if (_instance != null) _instance._activeWardrobe = wardrobe;
    }

    internal static void ClearActiveWardrobe(Wardrobe wardrobe)
    {
        if (_instance != null && _instance._activeWardrobe == wardrobe)
            _instance._activeWardrobe = null;
    }



    private void RefreshLayerNames()
    {
        var all = ClothingManagerMod.AllClothing;
        var names = ClothingManagerMod.AllClothingNames?.Value;

        // Two worn layers can resolve to the same display name (stacking the same item
        // twice, or two GUIDs mapping to one prefab name). ImGui keys Selectables by their
        // string, so append a hidden ##index to keep every entry uniquely selectable.
        _layerNames.Value = _layers.Select((l, i) => $"{NameForGuid(l.Guid, all, names)}##{i}").ToArray();

        if (_selectedLayer.Value >= _layers.Count)
            _selectedLayer.Value = Math.Max(0, _layers.Count - 1);
    }

    private static string NameForGuid(string guid, ClothingAssetReference[] all, string[] names)
    {
        if (all != null && names != null)
            for (var i = 0; i < all.Length && i < names.Length; i++)
                if (all[i] != null && all[i].clothingGuid.ToString() == guid)
                    return names[i];
        return guid;
    }

    /// <summary>
    /// A "None" piece is one of the game's invisible slot-clearing pieces (prefab name ends in
    /// <c>_None</c>), or a blank reference. Detected by name via the pre-loaded clothing list.
    /// </summary>
    private bool IsNonePiece(Guid guid)
    {
        if (guid == Guid.Empty) return true;
        var name = NameForGuid(guid.ToString(), ClothingManagerMod.AllClothing, ClothingManagerMod.AllClothingNames?.Value);
        if (name == null) return false;

        // AllClothingNames entries carry a hidden "##index" disambiguation suffix (see
        // ClothingManagerMod), so the prefab name is only the part before it. Strip that before
        // testing for the "_None" suffix, otherwise every None piece reads as a normal one and
        // gets stacked as a layer instead of clearing the slot.
        var hash = name.IndexOf("##", StringComparison.Ordinal);
        if (hash >= 0) name = name.Substring(0, hash);
        return name.EndsWith("_None", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The wardrobe edits a separate preview character; read that slot's current base piece.</summary>
    private bool IsWardrobeSlotBaseEmpty(Wardrobe wardrobe, ClothingSelectionType slot)
    {
        var preview = wardrobe && wardrobe.characterUI ? wardrobe.characterUI.GetCharacterCustomize() : null;
        if (!preview) return true; // preview not ready yet: treat as empty so the first pick becomes the base
        return SlotBaseEmpty(preview.GetClothesData(), slot);
    }

    /// <summary>A slot is "empty" when it holds no piece or one of the invisible None pieces.</summary>
    private bool SlotBaseEmpty(PlayerClothesData clothes, ClothingSelectionType slot)
    {
        var guid = SlotData(clothes, slot).clothingPrefabGUID;
        return guid == Guid.Empty || IsNonePiece(guid);
    }

    private static ClothingPieceData SlotData(PlayerClothesData clothes, ClothingSelectionType slot) => slot switch
    {
        ClothingSelectionType.Hat => clothes.ClothingHat,
        ClothingSelectionType.Top => clothes.ClothingTop,
        ClothingSelectionType.Bottom => clothes.ClothingBottom,
        ClothingSelectionType.Outfit => clothes.ClothingOutfit,
        _ => default
    };

    /// <summary>Return a copy of <paramref name="clothes"/> with one slot set to a stacked layer's piece.</summary>
    private static PlayerClothesData WithSlot(PlayerClothesData clothes, ClothingSelectionType slot, StackLayer layer)
    {
        var data = new ClothingPieceData
        {
            clothingPrefabGUID = Guid.Parse(layer.Guid),
            clothingPrimaryColor = new SerializableColor
            {
                r = layer.R, g = layer.G, b = layer.B, a = layer.A >= 0f ? layer.A : 1f
            }
        };

        switch (slot)
        {
            case ClothingSelectionType.Hat: clothes.ClothingHat = data; break;
            case ClothingSelectionType.Top: clothes.ClothingTop = data; break;
            case ClothingSelectionType.Bottom: clothes.ClothingBottom = data; break;
            case ClothingSelectionType.Outfit: clothes.ClothingOutfit = data; break;
        }
        return clothes;
    }

    /// <summary>Resolve which clothing slot a stacked layer belongs to, or None if unknown.</summary>
    private static ClothingSelectionType CategoryOf(string guidStr)
    {
        if (Guid.TryParse(guidStr, out var guid) && guid != Guid.Empty && ClothingManager.Instance)
        {
            var reference = ClothingManager.Instance.GetClothingReference(guid);
            if (reference != null) return reference.selectionType;
        }
        return ClothingSelectionType.None;
    }

    private void Persist() => SaveData(SaveKey, _layers);
}
