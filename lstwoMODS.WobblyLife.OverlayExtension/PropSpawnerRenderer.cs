using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using lstwoMODS_Overlay.UiRenderers;
using lstwoMODS.ImGui.Shared;
using lstwoMODS.ImGui.Shared.UI;
using lstwoMODS.WobblyLife.SharedObjects;
using Newtonsoft.Json;

using static Hexa.NET.ImGui.ImGui;
using ImGuiChildFlags      = Hexa.NET.ImGui.ImGuiChildFlags;
using ImGuiCol            = Hexa.NET.ImGui.ImGuiCol;
using ImGuiTableColumnFlags = Hexa.NET.ImGui.ImGuiTableColumnFlags;
using ImGuiInputTextFlags = Hexa.NET.ImGui.ImGuiInputTextFlags;
using ImGuiSelectableFlags = Hexa.NET.ImGui.ImGuiSelectableFlags;
using ImGuiTableFlags     = Hexa.NET.ImGui.ImGuiTableFlags;
using ImGuiTreeNodeFlags  = Hexa.NET.ImGui.ImGuiTreeNodeFlags;
using ImGuiWindowFlags    = Hexa.NET.ImGui.ImGuiWindowFlags;

namespace lstwoMODS.WobblyLife.OverlayExtension;

public class PropSpawnerRenderer : UIRenderer
{
    private string _search     = "";
    private string _compSearch = "";
    private HashSet<string> _activeComps = new(StringComparer.OrdinalIgnoreCase);

    private List<PropCacheEntry> _filteredFavorites    = new();
    private List<PropCacheEntry> _filteredNetworked    = new();
    private List<PropCacheEntry> _filteredNonNetworked = new();

    private PropCacheEntry? _selectedLib;
    private SpawnedPropEntry? _selectedSpawned;
    private (int packIdx, int itemIdx)? _selectedCustomItem;

    private string? _lastSearch;
    private bool _lastLoaded;
    private int _stateVersion = -1;

    private bool _favOpen = true;
    private bool _netOpen = true;
    private bool _nnetOpen = false;
    private bool _customOpen = true;
    private bool _spawnedOpen = true;

    public PropSpawnerRenderer(BaseUIElementData data) : base(data) { }

    public override void ApplyState(BaseUIElementData data) { Data = data; }
    public override BaseUIElementData? GetNewState() => null;


    public override void Render()
    {
        var state = WobblyLifeOverlayPlugin.PropSpawner;

        if (!state.IsLoaded)
        {
            TextDisabled("Waiting for asset database...");
            return;
        }

        var dirty = !_lastLoaded || _lastSearch != _search || _stateVersion != state.Version;
        if (dirty)
        {
            if (_stateVersion != state.Version)
            {
                _selectedLib        = null;
                _selectedSpawned    = null;
                _selectedCustomItem = null;
            }
            RebuildFilter(state);
            _lastSearch   = _search;
            _lastLoaded   = true;
            _stateVersion = state.Version;
        }

        if (!BeginTable("##wl-prop-layout", 2,
            ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, -1), 0))
            return;

        TableSetupColumn("##wl-left",  ImGuiTableColumnFlags.WidthStretch, 0.62f);
        TableSetupColumn("##wl-right", ImGuiTableColumnFlags.WidthStretch, 0.38f);
        TableNextRow();

        TableNextColumn();

        BeginChild("##wl-left-col", new Vector2(-8f, 0f), ImGuiChildFlags.None, ImGuiWindowFlags.None);

        SetNextItemWidth(-1);
        if (InputTextWithHint("##wl-prop-search", "Search props...", ref _search, (UIntPtr)256, ImGuiInputTextFlags.None))
            RebuildFilter(state);

        Spacing();

        var compBtnLabel = _activeComps.Count > 0
            ? $"Components ({_activeComps.Count} active)###wl-comp-btn"
            : "Component Filter###wl-comp-btn";
        if (Button(compBtnLabel, new Vector2(-1, 0)))
            OpenPopup("##wl-comp-filter");
        RenderComponentFilterPopup(state);

        Spacing();

        var hasCustomItems = state.CustomItemPacks.Count > 0;
        var available = GetContentRegionAvail().Y;
        var headerCount = 4 + (hasCustomItems ? 1 : 0);
        var openCount = (_favOpen ? 1 : 0) + (_netOpen ? 1 : 0) + (_nnetOpen ? 1 : 0) + (_spawnedOpen ? 1 : 0) + (hasCustomItems && _customOpen ? 1 : 0);
        var headerH = GetFrameHeightWithSpacing();
        var listsAvail = available - headerCount * headerH;
        var perList = openCount > 0 ? Math.Max(1f, listsAvail / openCount - GetStyle().ItemSpacing.Y) : 0f;

        var isFiltering = !string.IsNullOrEmpty(_search) || _activeComps.Count > 0;

        _favOpen = CollapsingHeader("Favorites", ImGuiTreeNodeFlags.DefaultOpen);
        if (_favOpen)
        {
            if (BeginChild("##wl-fav-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (_filteredFavorites.Count == 0)
                    TextDisabled(state.Favorites.Count == 0
                        ? "none (click ★ on a prop to add)"
                        : "(none match current filter)");
                else
                    RenderFlatList(_filteredFavorites, "fav");
            }
            EndChild();
        }

        _netOpen = CollapsingHeader($"Networked ({_filteredNetworked.Count})", ImGuiTreeNodeFlags.DefaultOpen);
        if (_netOpen)
        {
            if (BeginChild("##wl-net-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (isFiltering)
                    RenderFlatList(_filteredNetworked, "net");
                else
                    RenderTree(state.NetworkedTree);
            }
            EndChild();
        }

        _nnetOpen = CollapsingHeader($"Non-Networked ({_filteredNonNetworked.Count})");
        if (_nnetOpen)
        {
            if (BeginChild("##wl-nnet-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (isFiltering)
                    RenderFlatList(_filteredNonNetworked, "nnet");
                else
                    RenderTree(state.NonNetworkedTree);
            }
            EndChild();
        }

        if (hasCustomItems)
        {
            var totalCiItems = state.CustomItemPacks.Sum(p => p.Items?.Count ?? 0);
            _customOpen = CollapsingHeader($"Custom Items ({totalCiItems})###wl-custom-items", ImGuiTreeNodeFlags.DefaultOpen);
            
            if (_customOpen)
            {
                if (BeginChild("##wl-ci-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
                    RenderCustomItemsList(state, isFiltering);
                EndChild();
            }
        }

        _spawnedOpen = CollapsingHeader("Recent Spawns", ImGuiTreeNodeFlags.DefaultOpen);
        if (_spawnedOpen)
        {
            if (BeginChild("##wl-spawned-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (state.SpawnedProps.Count == 0)
                    TextDisabled("(none yet)");
                else
                    RenderSpawnedList(state);
            }
            EndChild();
        }

        EndChild();

        TableNextColumn();
        Indent(8f);

        if (_selectedSpawned != null)
            RenderSpawnedDetail(state, _selectedSpawned);
        else if (_selectedCustomItem.HasValue)
            RenderCustomItemDetail(state);
        else
            RenderLibraryDetail(state);

        EndTable();
    }


    private void RenderTree(PropTreeNode root)
    {
        foreach (var child in root.Children.Values.OrderBy(n => n.Children.Count == 0))
            RenderTreeNode(child);
    }

    private void RenderTreeNode(PropTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            var sel = ReferenceEquals(_selectedLib, node.Entry);
            if (Selectable(LeafLabel(node) + "##wl-" + node.FullPath, sel))
                SelectLibrary(node.Entry);
        }
        else
        {
            if (TreeNodeEx(node.Segment + "##wl-" + node.FullPath, ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                if (node.Entry != null)
                {
                    var sel = ReferenceEquals(_selectedLib, node.Entry);
                    if (Selectable("(this)##wl-this-" + node.FullPath, sel))
                        SelectLibrary(node.Entry);
                }
                foreach (var child in node.Children.Values.OrderBy(n => n.Children.Count == 0))
                    RenderTreeNode(child);
                TreePop();
            }
        }
    }

    private static string LeafLabel(PropTreeNode node)
    {
        var name = node.Entry?.Name;
        if (!string.IsNullOrEmpty(name) &&
            !string.Equals(name, node.Segment, StringComparison.OrdinalIgnoreCase))
            return $"{node.Segment} ({name})";
        return node.Segment;
    }


    private void RenderFlatList(List<PropCacheEntry> entries, string idPrefix)
    {
        foreach (var entry in entries)
        {
            var sel = ReferenceEquals(_selectedLib, entry);
            if (Selectable(FlatLabel(entry) + "##wl-" + idPrefix + "-" + entry.LoadKey, sel))
                SelectLibrary(entry);
        }
    }

    private static string FlatLabel(PropCacheEntry entry)
    {
        var key  = entry.LoadKey ?? entry.Name ?? "";
        var last = key.Length > 0 ? key.Substring(key.LastIndexOf('/') + 1) : key;
        var seg  = last.IndexOf('.') >= 0 ? last.Substring(0, last.LastIndexOf('.')) : last;

        var name = entry.Name;
        if (!string.IsNullOrEmpty(name) &&
            !string.Equals(name, seg, StringComparison.OrdinalIgnoreCase))
            return $"{key} ({name})";
        return key;
    }


    private void RenderSpawnedList(PropSpawnerState state)
    {
        foreach (var entry in state.SpawnedProps)
        {
            var sel   = _selectedSpawned?.SpawnId == entry.SpawnId;
            var  tag   = entry.Networked ? "[N]" : "[L]";
            var  label = entry.Name ?? entry.Address ?? $"#{entry.SpawnId}";
            if (Selectable($"{tag} {label}##wl-sp-{entry.SpawnId}", sel))
                SelectSpawned(entry);
        }
    }


    private void RenderCustomItemsList(PropSpawnerState state, bool isFiltering)
    {
        for (var pi = 0; pi < state.CustomItemPacks.Count; pi++)
        {
            var pack = state.CustomItemPacks[pi];
            if (pack.Items == null || pack.Items.Count == 0) continue;

            if (isFiltering)
            {
                for (var ii = 0; ii < pack.Items.Count; ii++)
                {
                    var item = pack.Items[ii];
                    var nameHit = item.Name?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
                    var descHit = item.Description?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!nameHit && !descHit) continue;

                    var sel = _selectedCustomItem?.packIdx == pi && _selectedCustomItem?.itemIdx == ii;
                    if (Selectable($"[{pack.PackName}] {item.Name ?? ""}##wl-ci-{pi}-{ii}", sel))
                        SelectCustomItem(pi, ii);
                }
            }
            else
            {
                var packLabel = string.IsNullOrEmpty(pack.PackAuthor)
                    ? pack.PackName ?? $"Pack {pi}"
                    : $"{pack.PackName} ({pack.PackAuthor})";

                if (TreeNodeEx(packLabel + "##wl-ci-pack-" + pi, ImGuiTreeNodeFlags.SpanAvailWidth))
                {
                    for (var ii = 0; ii < pack.Items.Count; ii++)
                    {
                        var item = pack.Items[ii];
                        var sel = _selectedCustomItem?.packIdx == pi && _selectedCustomItem?.itemIdx == ii;
                        if (Selectable((item.Name ?? $"Item {ii}") + $"##wl-ci-{pi}-{ii}", sel))
                            SelectCustomItem(pi, ii);
                    }
                    TreePop();
                }
            }
        }
    }


    private void RenderComponentFilterPopup(PropSpawnerState state)
    {
        SetNextWindowSizeConstraints(new Vector2(260, 120), new Vector2(420, 420));
        if (!BeginPopup("##wl-comp-filter")) return;

        SetNextItemWidth(-1);
        InputText("##wl-comp-search", ref _compSearch, (UIntPtr)128, ImGuiInputTextFlags.None);

        Spacing();

        if (_activeComps.Count > 0)
        {
            if (Button("Clear All###wl-comp-clear", new Vector2(-1, 0)))
            {
                _activeComps.Clear();
                RebuildFilter(state);
            }
            Spacing();
        }

        Separator();
        Spacing();

        if (BeginChild("##wl-comp-popup-list", new Vector2(0, 300), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            foreach (var comp in state.AllComponentNames)
            {
                if (!string.IsNullOrEmpty(_compSearch) &&
                    comp.IndexOf(_compSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var active = _activeComps.Contains(comp);
                if (Checkbox(comp + "##wlcf", ref active))
                {
                    if (active) _activeComps.Add(comp);
                    else        _activeComps.Remove(comp);
                    RebuildFilter(state);
                }
            }
        }
        EndChild();

        EndPopup();
    }


    private void RenderLibraryDetail(PropSpawnerState state)
    {
        Text("Selected Prop");
        Separator();

        if (_selectedLib == null)
        {
            TextDisabled("(none selected)");
        }
        else
        {
            var isFav = state.Favorites.Contains(_selectedLib.LoadKey ?? "");
            var  star  = isFav ? "★ Unfavorite###wl-fav-toggle" : "☆ Favorite###wl-fav-toggle";
            if (Button(star, new Vector2(-1, 0)))
            {
                state.ToggleFavorite(_selectedLib.LoadKey ?? "");
                RebuildFilter(state);
            }

            Spacing();
            TextWrapped(_selectedLib.Name ?? "");
            TextWrapped(_selectedLib.Address ?? _selectedLib.Guid ?? "");

            if (_selectedLib.ComponentTypes is { Length: > 0 })
            {
                Spacing();
                Separator();
                Text("Components");
                Separator();
                Spacing();

                for (var ci = 0; ci < _selectedLib.ComponentTypes.Length; ci++)
                {
                    var comp = _selectedLib.ComponentTypes[ci];
                    var shortName = PropSpawnerState.ShortTypeName(comp);
                    var inFilter = _activeComps.Contains(shortName);

                    if (inFilter) PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 0.4f, 1f));
                    if (Selectable(shortName + "##wl-comp-" + ci + "-" + comp, inFilter,
                            ImGuiSelectableFlags.None, new Vector2(0, 0)))
                    {
                        if (inFilter) _activeComps.Remove(shortName);
                        else          _activeComps.Add(shortName);
                        RebuildFilter(state);
                    }
                    if (inFilter) PopStyleColor();
                    if (IsItemHovered()) SetTooltip(comp);
                }
            }
        }

        Spacing();
        BeginDisabled(_selectedLib == null);

        if (Button("Spawn Networked", new Vector2(-1, 0)))
            SendSpawn(networked: true);

        Spacing();

        if (Button("Spawn Local (Non-Networked)", new Vector2(-1, 0)))
            SendSpawn(networked: false);

        EndDisabled();
    }

    private void RenderSpawnedDetail(PropSpawnerState state, SpawnedPropEntry entry)
    {
        Text("Spawned Prop");
        Separator();

        var displayName = entry.Name ?? entry.Address ?? $"Prop #{entry.SpawnId}";
        TextWrapped(displayName);
        TextDisabled(entry.Address ?? "");
        TextDisabled(entry.Networked ? "Networked" : "Local (Non-Networked)");

        if (entry.LibraryEntry?.ComponentTypes is { Length: > 0 })
        {
            Spacing();
            Separator();
            Text("Components");
            Separator();
            Spacing();

            for (var ci = 0; ci < entry.LibraryEntry.ComponentTypes.Length; ci++)
            {
                var comp = entry.LibraryEntry.ComponentTypes[ci];
                var shortName = PropSpawnerState.ShortTypeName(comp);
                var inFilter = _activeComps.Contains(shortName);

                if (inFilter) PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 0.4f, 1f));
                if (Selectable(shortName + "##wl-sp-comp-" + ci + "-" + comp, inFilter,
                        ImGuiSelectableFlags.None, new Vector2(0, 0)))
                {
                    if (inFilter) _activeComps.Remove(shortName);
                    else          _activeComps.Add(shortName);
                    RebuildFilter(state);
                }
                if (inFilter) PopStyleColor();
                if (IsItemHovered()) SetTooltip(comp);
            }
        }

        Spacing();
        Separator();
        Spacing();

        if (Button("Destroy##wl-sp-del", new Vector2(-1, 0)))
            SendSpawnedAction(entry.SpawnId, "Delete");

        Spacing();

        if (Button("Inspect (Unity Explorer)##wl-sp-insp", new Vector2(-1, 0)))
            SendSpawnedAction(entry.SpawnId, "Inspect");
    }

    private void RenderCustomItemDetail(PropSpawnerState state)
    {
        Text("Custom Item");
        Separator();

        var (pi, ii) = _selectedCustomItem!.Value;
        var pack = state.CustomItemPacks[pi];
        var item = pack.Items![ii];

        TextWrapped(item.Name ?? "");

        var packLabel = string.IsNullOrEmpty(pack.PackAuthor)
            ? pack.PackName ?? ""
            : $"{pack.PackName} by {pack.PackAuthor}";
        TextDisabled(packLabel);

        if (!string.IsNullOrEmpty(item.Description))
        {
            Spacing();
            Separator();
            TextWrapped(item.Description);
        }

        Spacing();
        if (Button("Spawn##wl-ci-spawn", new Vector2(-1, 0)))
            SendSpawnCustomItem(pi, ii);
    }


    private void RebuildFilter(PropSpawnerState state)
    {
        var hasSearch = !string.IsNullOrEmpty(_search);
        var hasComps  = _activeComps.Count > 0;

        if (!hasSearch && !hasComps)
        {
            _filteredNetworked    = state.Networked;
            _filteredNonNetworked = state.NonNetworked;
            _filteredFavorites    = BuildFavoritesList(state);
            return;
        }

        _filteredNetworked    = state.Networked   .Where(Matches).ToList();
        _filteredNonNetworked = state.NonNetworked.Where(Matches).ToList();
        _filteredFavorites    = BuildFavoritesList(state).Where(Matches).ToList();
    }

    private bool Matches(PropCacheEntry e)
    {
        if (!string.IsNullOrEmpty(_search))
        {
            var hit = (e.LoadKey?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                      (e.Name   ?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hit) return false;
        }

        if (_activeComps.Count > 0)
        {
            if (e.ComponentTypes == null) return false;
            var shorts = new HashSet<string>(
                e.ComponentTypes.Select(PropSpawnerState.ShortTypeName),
                StringComparer.OrdinalIgnoreCase);
            if (!_activeComps.All(f => shorts.Contains(f))) return false;
        }

        return true;
    }

    private static List<PropCacheEntry> BuildFavoritesList(PropSpawnerState state)
    {
        if (state.Favorites.Count == 0) return new List<PropCacheEntry>();
        return state.Networked.Concat(state.NonNetworked)
            .Where(e => state.Favorites.Contains(e.LoadKey ?? ""))
            .ToList();
    }


    private void SelectLibrary(PropCacheEntry? entry)
    {
        _selectedLib        = entry;
        _selectedSpawned    = null;
        _selectedCustomItem = null;
    }

    private void SelectSpawned(SpawnedPropEntry entry)
    {
        _selectedSpawned    = entry;
        _selectedLib        = null;
        _selectedCustomItem = null;
    }

    private void SelectCustomItem(int packIdx, int itemIdx)
    {
        _selectedCustomItem = (packIdx, itemIdx);
        _selectedLib        = null;
        _selectedSpawned    = null;
    }


    private void SendSpawn(bool networked)
    {
        if (_selectedLib == null) return;
        var spawn = new SpawnPropMessage { Address = _selectedLib.LoadKey, Networked = networked };
        WobblyLifeOverlayPlugin.Context.SendToMod(JsonConvert.SerializeObject(spawn.Serialize()));
    }

    private void SendSpawnCustomItem(int packIdx, int itemIdx)
    {
        var msg = new SpawnCustomItemMessage { PackIndex = packIdx, ItemIndex = itemIdx };
        WobblyLifeOverlayPlugin.Context.SendToMod(JsonConvert.SerializeObject(msg.Serialize()));
    }

    private void SendSpawnedAction(int spawnId, string action)
    {
        var msg = new SpawnedPropActionMessage { SpawnId = spawnId, Action = action };
        WobblyLifeOverlayPlugin.Context.SendToMod(JsonConvert.SerializeObject(msg.Serialize()));

        if (action == "Delete")
        {
            WobblyLifeOverlayPlugin.PropSpawner.RemoveSpawned(spawnId);
            _selectedSpawned = null;
        }
    }
}
