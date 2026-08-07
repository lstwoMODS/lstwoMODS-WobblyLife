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
using ImGuiChildFlags       = Hexa.NET.ImGui.ImGuiChildFlags;
using ImGuiCol              = Hexa.NET.ImGui.ImGuiCol;
using ImGuiComboFlags       = Hexa.NET.ImGui.ImGuiComboFlags;
using ImGuiCond             = Hexa.NET.ImGui.ImGuiCond;
using ImGuiTableColumnFlags = Hexa.NET.ImGui.ImGuiTableColumnFlags;
using ImGuiInputTextFlags   = Hexa.NET.ImGui.ImGuiInputTextFlags;
using ImGuiKey              = Hexa.NET.ImGui.ImGuiKey;
using ImGuiPopupFlags       = Hexa.NET.ImGui.ImGuiPopupFlags;
using ImGuiSelectableFlags  = Hexa.NET.ImGui.ImGuiSelectableFlags;
using ImGuiTableFlags       = Hexa.NET.ImGui.ImGuiTableFlags;
using ImGuiTreeNodeFlags    = Hexa.NET.ImGui.ImGuiTreeNodeFlags;
using ImGuiWindowFlags      = Hexa.NET.ImGui.ImGuiWindowFlags;

namespace lstwoMODS.WobblyLife.OverlayExtension;

public class PropSpawnerRenderer : UIRenderer
{
    private enum ConfirmKind { None, DeleteGroup, DestroyGroup, DestroySelected, DestroyAll, SaveLossy }

    private const string ModalNewGroup = "New Prop Group##wl-modal-newgrp";
    private const string ModalRename   = "Rename Group##wl-modal-ren";
    private const string ModalConfirm  = "Confirm##wl-modal-confirm";
    private const string ModalLoad     = "Spawn Saved Layout##wl-modal-load";

    private static readonly Vector4 DirtyColor  = new(0.95f, 0.78f, 0.30f, 1f);
    private static readonly Vector4 ErrorColor  = new(0.95f, 0.45f, 0.40f, 1f);
    private static readonly Vector4 ActiveColor = new(0.45f, 0.85f, 0.55f, 1f);

    private static readonly List<SpawnedPropEntry> EmptyMembers = new();

    /// <summary>The groups tree earns more room than a plain list: it is the working area.</summary>
    private const float GroupsWeight = 2f;

    private string _search     = "";
    private string _compSearch = "";
    private readonly HashSet<string> _activeComps = new(StringComparer.OrdinalIgnoreCase);

    private List<PropCacheEntry> _filteredFavorites    = new();
    private List<PropCacheEntry> _filteredNetworked    = new();
    private List<PropCacheEntry> _filteredNonNetworked = new();

    private PropCacheEntry? _selectedLib;
    private (int packIdx, int itemIdx)? _selectedCustomItem;

    // Selection is by id, never by reference: every snapshot allocates fresh entry objects, so a
    // held reference would render stale text while its buttons acted on a live id.
    private readonly HashSet<int> _selectedSpawnIds = new();
    private int?    _selAnchor;
    private string? _selAnchorGroup;
    private string? _selectedGroupId;

    private string? _lastSearch;
    private bool _lastLoaded;
    private int  _stateVersion    = -1;
    private int  _groupsRevision  = -1;

    // Modal requests are raised from inside child windows, tree nodes and context-menu bodies.
    // OpenPopup has to be called at the root ID scope instead, so the request is deferred.
    private string?     _modalToOpen;
    private string?     _modalGroupId;
    private string      _nameBuf     = "";
    private string      _confirmText = "";
    private ConfirmKind _confirmKind;
    private bool _deleteAlsoFile;
    private bool _loadReplace;
    private bool _newGroupTakesSelection;
    private PropGroupLoadMode _loadMode = PropGroupLoadMode.AtSavedCoords;

    public PropSpawnerRenderer(BaseUIElementData data) : base(data) { }

    public override void ApplyState(BaseUIElementData data) { Data = data; }
    public override BaseUIElementData? GetNewState() => null;


    public override void Render()
    {
        var state = WobblyLifeOverlayPlugin.PropSpawner;

        // Swap in anything the IPC thread queued, before any collection is walked.
        state.Pump();

        if (!state.IsLoaded)
        {
            TextDisabled("Waiting for asset database...");
            return;
        }

        if (_groupsRevision != state.GroupsRevision)
        {
            _groupsRevision = state.GroupsRevision;
            PruneSelection(state);
        }

        var dirty = !_lastLoaded || _lastSearch != _search || _stateVersion != state.Version;
        if (dirty)
        {
            if (_stateVersion != state.Version)
            {
                _selectedLib        = null;
                _selectedCustomItem = null;
            }
            RebuildFilter(state);
            _lastSearch   = _search;
            _lastLoaded   = true;
            _stateVersion = state.Version;
        }

        RenderActiveGroupBar(state);
        Separator();

        if (!BeginTable("##wl-prop-layout", 2,
            ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp,
            new Vector2(0, -1), 0))
            return;

        TableSetupColumn("##wl-left",  ImGuiTableColumnFlags.WidthStretch, 0.62f);
        TableSetupColumn("##wl-right", ImGuiTableColumnFlags.WidthStretch, 0.38f);
        TableNextRow();

        TableNextColumn();
        RenderLeftColumn(state);

        TableNextColumn();
        Indent(8f);
        RenderDetailPane(state);

        EndTable();

        // Root ID scope: the only place a modal may be opened or declared from.
        if (_modalToOpen != null)
        {
            OpenPopup(_modalToOpen);
            _modalToOpen = null;
        }

        RenderNewGroupModal(state);
        RenderRenameModal(state);
        RenderConfirmModal(state);
        RenderLoadModal(state);

        state.SaveUiStateIfChanged();
    }


    // ── active group bar ─────────────────────────────────────────────────────

    /// <summary>
    /// Sits above the table, spanning both columns: it governs every spawn button in the panel,
    /// including the ones in the right-hand pane, so it must not read as a left-column filter.
    /// </summary>
    private void RenderActiveGroupBar(PropSpawnerState state)
    {
        var active = state.ActiveGroupId != null && state.GroupsById.TryGetValue(state.ActiveGroupId, out var g)
            ? g : null;

        AlignTextToFramePadding();
        TextDisabled("Spawn into:");
        SameLine();

        var buttonWidth = 120f;
        SetNextItemWidth(-(buttonWidth + GetStyle().ItemSpacing.X));

        var preview = active != null ? $"{active.Name}  ({active.LiveCount})" : "(Ungrouped)";
        if (BeginCombo("##wl-active-group", preview, ImGuiComboFlags.None))
        {
            if (Selectable("(Ungrouped)##wl-ag-none", state.ActiveGroupId == null))
                SendGroupCommand(PropGroupCommand.SetActiveGroup, null);

            foreach (var group in state.Groups)
            {
                var label = $"{group.Name}  ({group.LiveCount}/{group.SavedCount})###wl-ag-{group.Id}";
                if (Selectable(label, group.Id == state.ActiveGroupId))
                    SendGroupCommand(PropGroupCommand.SetActiveGroup, group.Id);
            }

            EndCombo();
        }

        SameLine();
        if (Button("New Group...##wl-newgrp-btn", new Vector2(buttonWidth, 0)))
        {
            _newGroupTakesSelection = false;
            RequestModal(ModalNewGroup);
        }
    }


    // ── left column ──────────────────────────────────────────────────────────

    private void RenderLeftColumn(PropSpawnerState state)
    {
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

        var ui = state.Ui;
        var hasCustomItems = state.CustomItemPacks.Count > 0;

        // Measured inside this child, so the active-group bar above the table is already
        // accounted for. No compensating subtraction belongs here.
        var available   = GetContentRegionAvail().Y;
        var headerCount = 4 + (hasCustomItems ? 1 : 0);
        var headerH     = GetFrameHeightWithSpacing();
        var spacing     = GetStyle().ItemSpacing.Y;
        var listsAvail  = available - headerCount * headerH;

        var weight = (ui.FavOpen ? 1f : 0f)
                   + (ui.NetOpen ? 1f : 0f)
                   + (ui.NnetOpen ? 1f : 0f)
                   + (hasCustomItems && ui.CustomOpen ? 1f : 0f)
                   + (ui.GroupsOpen ? GroupsWeight : 0f);

        float Share(float w) => weight > 0f ? Math.Max(1f, listsAvail * (w / weight) - spacing) : 0f;

        var perList = Share(1f);
        var groupsH = Share(GroupsWeight);

        var isFiltering = !string.IsNullOrEmpty(_search) || _activeComps.Count > 0;

        SetNextItemOpen(ui.FavOpen, ImGuiCond.Once);
        ui.FavOpen = CollapsingHeader("Favorites###wl-fav");
        if (ui.FavOpen)
        {
            if (BeginChild("##wl-fav-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (_filteredFavorites.Count == 0)
                    TextDisabled(state.Favorites.Count == 0
                        ? "none (click the star on a prop to add)"
                        : "(none match current filter)");
                else
                    RenderFlatList(_filteredFavorites, "fav");
            }
            EndChild();
        }

        SetNextItemOpen(ui.NetOpen, ImGuiCond.Once);
        ui.NetOpen = CollapsingHeader($"Networked ({_filteredNetworked.Count})###wl-net");
        if (ui.NetOpen)
        {
            if (BeginChild("##wl-net-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (isFiltering) RenderFlatList(_filteredNetworked, "net");
                else             RenderTree(state.NetworkedTree);
            }
            EndChild();
        }

        SetNextItemOpen(ui.NnetOpen, ImGuiCond.Once);
        ui.NnetOpen = CollapsingHeader($"Non-Networked ({_filteredNonNetworked.Count})###wl-nnet");
        if (ui.NnetOpen)
        {
            if (BeginChild("##wl-nnet-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                if (isFiltering) RenderFlatList(_filteredNonNetworked, "nnet");
                else             RenderTree(state.NonNetworkedTree);
            }
            EndChild();
        }

        if (hasCustomItems)
        {
            var totalCiItems = state.CustomItemPacks.Sum(p => p.Items?.Count ?? 0);
            SetNextItemOpen(ui.CustomOpen, ImGuiCond.Once);
            ui.CustomOpen = CollapsingHeader($"Custom Items ({totalCiItems})###wl-custom-items");

            if (ui.CustomOpen)
            {
                if (BeginChild("##wl-ci-list", new Vector2(0, perList), ImGuiChildFlags.None, ImGuiWindowFlags.None))
                    RenderCustomItemsList(state, isFiltering);
                EndChild();
            }
        }

        SetNextItemOpen(ui.GroupsOpen, ImGuiCond.Once);
        ui.GroupsOpen = CollapsingHeader($"Groups ({state.Spawned.Count} live)###wl-groups");
        if (ui.GroupsOpen)
        {
            if (BeginChild("##wl-groups-list", new Vector2(0, groupsH), ImGuiChildFlags.None, ImGuiWindowFlags.None))
                RenderGroupsTree(state);
            EndChild();
        }

        EndChild();
    }


    // ── groups tree ──────────────────────────────────────────────────────────

    private void RenderGroupsTree(PropSpawnerState state)
    {
        // Inside the child, so it costs nothing in the height maths above.
        if (SmallButton("New##wl-gt-new"))
        {
            _newGroupTakesSelection = false;
            RequestModal(ModalNewGroup);
        }
        SameLine();

        BeginDisabled(state.Spawned.Count == 0);
        if (SmallButton("Destroy All##wl-gt-da"))
            RequestConfirm(ConfirmKind.DestroyAll, $"Destroy all {state.Spawned.Count} spawned prop(s)?");
        EndDisabled();
        SameLine();

        if (SmallButton("Refresh##wl-gt-ref"))
            SendGroupCommand(PropGroupCommand.Refresh, null);
        if (IsItemHovered())
            SetTooltip("Re-scan the prop_groups folder, so a group file someone shared shows up.");

        Separator();

        foreach (var group in state.Groups)
            RenderGroupNode(state, group);

        RenderUngroupedNode(state);

        if (state.TruncatedSpawned > 0)
            TextDisabled($"... and {state.TruncatedSpawned} more not shown");

        if (BeginPopupContextWindow("##wl-groups-ctx",
                ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (MenuItem("New Group...##wl-gctx-new"))
            {
                _newGroupTakesSelection = false;
                RequestModal(ModalNewGroup);
            }
            if (MenuItem("Refresh##wl-gctx-ref")) SendGroupCommand(PropGroupCommand.Refresh, null);
            Separator();
            if (MenuItem("Destroy All Spawned##wl-gctx-da"))
                RequestConfirm(ConfirmKind.DestroyAll, $"Destroy all {state.Spawned.Count} spawned prop(s)?");
            EndPopup();
        }
    }

    private void RenderGroupNode(PropSpawnerState state, PropGroupData group)
    {
        var members = state.MembersByGroup.TryGetValue(group.Id, out var m) ? m : EmptyMembers;
        var isActive = group.Id == state.ActiveGroupId;

        var label = $"{group.Name}{(isActive ? "  >" : "")}  ({group.LiveCount}/{group.SavedCount})" +
                    $"{(group.HasUnsavedChanges ? "  *" : "")}###wl-grp-{group.Id}";

        var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                  | ImGuiTreeNodeFlags.OpenOnArrow
                  | ImGuiTreeNodeFlags.OpenOnDoubleClick;
        if (_selectedGroupId == group.Id) flags |= ImGuiTreeNodeFlags.Selected;

        var wasExpanded = state.Ui.ExpandedGroupIds.Contains(group.Id);
        SetNextItemOpen(wasExpanded, ImGuiCond.Once);

        if (group.HasUnsavedChanges) PushStyleColor(ImGuiCol.Text, DirtyColor);
        else if (isActive)           PushStyleColor(ImGuiCol.Text, ActiveColor);
        var open = TreeNodeEx(label, flags);
        if (group.HasUnsavedChanges || isActive) PopStyleColor();

        if (BeginPopupContextItem($"##wl-grp-ctx-{group.Id}"))
        {
            RenderGroupContextItems(state, group);
            EndPopup();
        }

        // Without the toggle check, clicking the expand arrow would also re-target the detail pane.
        if (IsItemClicked() && !IsItemToggledOpen()) SelectGroup(group.Id);

        if (open != wasExpanded)
        {
            if (open) state.Ui.ExpandedGroupIds.Add(group.Id);
            else      state.Ui.ExpandedGroupIds.Remove(group.Id);
        }

        if (!open) return;

        if (members.Count == 0) TextDisabled("(empty)");
        else foreach (var entry in members) RenderMemberRow(state, entry, group.Id, members);

        TreePop();
    }

    private void RenderUngroupedNode(PropSpawnerState state)
    {
        var members = state.MembersByGroup.TryGetValue("", out var m) ? m : EmptyMembers;

        var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                  | ImGuiTreeNodeFlags.OpenOnArrow
                  | ImGuiTreeNodeFlags.OpenOnDoubleClick;

        if (!TreeNodeEx($"(Ungrouped) ({members.Count})###wl-grp-none", flags)) return;

        if (members.Count == 0) TextDisabled("(empty)");
        else foreach (var entry in members) RenderMemberRow(state, entry, "", members);

        TreePop();
    }

    private void RenderMemberRow(PropSpawnerState state, SpawnedPropEntry entry,
                                 string groupKey, List<SpawnedPropEntry> siblings)
    {
        var tag   = entry.Networked ? "[N]" : "[L]";
        var label = entry.Name ?? entry.Address ?? $"#{entry.SpawnId}";
        var selected = _selectedSpawnIds.Contains(entry.SpawnId);

        if (Selectable($"    {tag} {label}###wl-sp-{entry.SpawnId}", selected,
                       ImGuiSelectableFlags.None, new Vector2(0, 0)))
        {
            if (CtrlHeld())
            {
                if (!_selectedSpawnIds.Remove(entry.SpawnId)) _selectedSpawnIds.Add(entry.SpawnId);
                SetAnchor(entry.SpawnId, groupKey);
            }
            else if (ShiftHeld() && _selAnchor.HasValue && _selAnchorGroup == groupKey)
            {
                SelectRange(siblings, _selAnchor.Value, entry.SpawnId);
            }
            else
            {
                _selectedSpawnIds.Clear();
                _selectedSpawnIds.Add(entry.SpawnId);
                SetAnchor(entry.SpawnId, groupKey);
            }

            _selectedGroupId    = null;
            _selectedLib        = null;
            _selectedCustomItem = null;
        }

        if (BeginPopupContextItem($"##wl-sp-ctx-{entry.SpawnId}"))
        {
            // Explorer semantics: right-clicking outside the selection re-selects just that row.
            if (!_selectedSpawnIds.Contains(entry.SpawnId))
            {
                _selectedSpawnIds.Clear();
                _selectedSpawnIds.Add(entry.SpawnId);
                SetAnchor(entry.SpawnId, groupKey);
                _selectedGroupId = null;
            }

            RenderMemberContextItems(state);
            EndPopup();
        }
    }

    private void RenderMemberContextItems(PropSpawnerState state)
    {
        var count = _selectedSpawnIds.Count;
        TextDisabled(count == 1 ? "1 prop selected" : $"{count} props selected");
        Separator();

        if (BeginMenu("Move to Group##wl-mv"))
        {
            foreach (var group in state.Groups)
                if (MenuItem($"{group.Name}###wl-mv-{group.Id}"))
                    SendAssign(group.Id);

            Separator();
            if (MenuItem("(Ungrouped)##wl-mv-none")) SendAssign(null);
            if (MenuItem("New Group...##wl-mv-new"))
            {
                _newGroupTakesSelection = true;
                RequestModal(ModalNewGroup);
            }
            EndMenu();
        }

        Separator();

        if (MenuItem($"Destroy ({count})##wl-ctx-del"))
            RequestConfirm(ConfirmKind.DestroySelected, $"Destroy {count} spawned prop(s)?");

        if (MenuItem("Inspect (Unity Explorer)##wl-ctx-insp", "", false, count == 1))
            SendSpawnedAction(new[] { _selectedSpawnIds.First() }, "Inspect");
    }

    private void RenderGroupContextItems(PropSpawnerState state, PropGroupData group)
    {
        TextDisabled(group.Name);
        Separator();

        if (MenuItem($"Set Active##wl-gc-act-{group.Id}", "", false, group.Id != state.ActiveGroupId))
            SendGroupCommand(PropGroupCommand.SetActiveGroup, group.Id);

        if (MenuItem($"Save Layout ({group.LiveCount})##wl-gc-save-{group.Id}", "", false, group.LiveCount > 0))
            RequestSave(group);

        if (MenuItem($"Spawn Saved Layout ({group.SavedCount})##wl-gc-load-{group.Id}", "", false,
                     group.HasFile && group.SavedCount > 0))
            RequestLoad(group);

        Separator();

        if (MenuItem($"Rename...##wl-gc-ren-{group.Id}"))
        {
            _modalGroupId = group.Id;
            _nameBuf      = group.Name;
            RequestModal(ModalRename);
        }

        if (MenuItem($"Destroy Live Members ({group.LiveCount})##wl-gc-destroy-{group.Id}", "", false,
                     group.LiveCount > 0))
        {
            _modalGroupId = group.Id;
            RequestConfirm(ConfirmKind.DestroyGroup,
                $"Destroy {group.LiveCount} live prop(s) in \"{group.Name}\"?\nThe saved file is kept.");
        }

        if (MenuItem($"Reveal File in Explorer##wl-gc-reveal-{group.Id}", "", false, group.HasFile))
            SendGroupCommand(PropGroupCommand.RevealGroupFile, group.Id);

        Separator();

        if (MenuItem($"Delete Group...##wl-gc-del-{group.Id}"))
        {
            _modalGroupId   = group.Id;
            _deleteAlsoFile = group.HasFile;
            RequestConfirm(ConfirmKind.DeleteGroup, $"Delete the group \"{group.Name}\"?");
        }
    }


    // ── library lists (unchanged behaviour) ──────────────────────────────────

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


    // ── detail pane ──────────────────────────────────────────────────────────

    private void RenderDetailPane(PropSpawnerState state)
    {
        // Above the detail, not below it: the group pane ends with a members list that fills the
        // rest of the column, so anything after it would be pushed out of view.
        RenderStatusBanner(state);

        if (_selectedGroupId != null && state.GroupsById.TryGetValue(_selectedGroupId, out var group))
            RenderGroupDetail(state, group);
        else if (_selectedSpawnIds.Count == 1 && state.SpawnedById.TryGetValue(_selectedSpawnIds.First(), out var one))
            RenderSpawnedDetail(state, one);
        else if (_selectedSpawnIds.Count > 1)
            RenderMultiSelectDetail(state);
        else if (_selectedCustomItem.HasValue)
            RenderCustomItemDetail(state);
        else
            RenderLibraryDetail(state);
    }

    private void RenderStatusBanner(PropSpawnerState state)
    {
        if (string.IsNullOrEmpty(state.StatusText)) return;

        if (state.StatusIsBad) PushStyleColor(ImGuiCol.Text, ErrorColor);
        TextWrapped(state.StatusText!);
        if (state.StatusIsBad) PopStyleColor();

        SameLine();
        if (SmallButton("x##wl-status-dismiss")) state.ClearStatus();

        Separator();
        Spacing();
    }

    private void RenderGroupDetail(PropSpawnerState state, PropGroupData group)
    {
        Text("Prop Group");
        Separator();

        TextWrapped(group.Name);
        TextDisabled($"{group.LiveCount} live | {group.SavedCount} saved");
        if (group.SavedAtUnix > 0) TextDisabled($"saved {FormatAge(group.SavedAtUnix)}");

        if (group.HasUnsavedChanges) TextColored(DirtyColor, "* unsaved changes");
        else if (group.HasFile)      TextDisabled("up to date");
        else                         TextDisabled("never saved");

        if (!string.IsNullOrEmpty(group.FilePath))
        {
            var slash = group.FilePath!.LastIndexOfAny(new[] { '/', '\\' });
            TextDisabled(slash >= 0 ? group.FilePath.Substring(slash + 1) : group.FilePath);
            if (IsItemHovered()) SetTooltip($"{group.FilePath}\n\nShare this file to share the build.");
        }

        Spacing();
        Separator();
        Spacing();

        BeginDisabled(group.Id == state.ActiveGroupId);
        if (Button("Set Active##wl-gd-active", new Vector2(-1, 0)))
            SendGroupCommand(PropGroupCommand.SetActiveGroup, group.Id);
        EndDisabled();

        Spacing();
        BeginDisabled(group.LiveCount == 0 && group.SavedCount == 0);
        if (Button($"Save Layout ({group.LiveCount})##wl-gd-save", new Vector2(-1, 0)))
            RequestSave(group);
        EndDisabled();
        if (IsItemHovered())
            SetTooltip("Capture where the group's props are right now into its file.");

        Spacing();
        BeginDisabled(!group.HasFile || group.SavedCount == 0);
        if (Button($"Spawn Saved Layout ({group.SavedCount})##wl-gd-load", new Vector2(-1, 0)))
            RequestLoad(group);
        EndDisabled();

        Spacing();
        Separator();
        Spacing();

        if (Button("Rename...##wl-gd-ren", new Vector2(-1, 0)))
        {
            _modalGroupId = group.Id;
            _nameBuf      = group.Name;
            RequestModal(ModalRename);
        }

        Spacing();
        BeginDisabled(group.LiveCount == 0);
        if (Button($"Destroy Live Members ({group.LiveCount})##wl-gd-destroy", new Vector2(-1, 0)))
        {
            _modalGroupId = group.Id;
            RequestConfirm(ConfirmKind.DestroyGroup,
                $"Destroy {group.LiveCount} live prop(s) in \"{group.Name}\"?\nThe saved file is kept.");
        }
        EndDisabled();

        Spacing();
        BeginDisabled(!group.HasFile);
        if (Button("Reveal File in Explorer##wl-gd-reveal", new Vector2(-1, 0)))
            SendGroupCommand(PropGroupCommand.RevealGroupFile, group.Id);
        EndDisabled();

        Spacing();
        if (Button("Delete Group...##wl-gd-del", new Vector2(-1, 0)))
        {
            _modalGroupId   = group.Id;
            _deleteAlsoFile = group.HasFile;
            RequestConfirm(ConfirmKind.DeleteGroup, $"Delete the group \"{group.Name}\"?");
        }

        Spacing();
        Separator();
        Text("Members");
        Separator();
        Spacing();

        // The tree already drew rows with these ids this frame; a scope keeps the two apart.
        PushID("gd");
        if (BeginChild("##wl-gd-members", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            var members = state.MembersByGroup.TryGetValue(group.Id, out var m) ? m : EmptyMembers;
            if (members.Count == 0) TextDisabled("(empty)");
            else foreach (var entry in members) RenderMemberRow(state, entry, group.Id, members);
        }
        EndChild();
        PopID();
    }

    private void RenderMultiSelectDetail(PropSpawnerState state)
    {
        var count = _selectedSpawnIds.Count;

        Text("Selection");
        Separator();
        TextWrapped($"{count} spawned props selected.");

        Spacing();
        Separator();
        Spacing();

        SetNextItemWidth(-1);
        if (BeginCombo("##wl-ms-move", "Move to Group...", ImGuiComboFlags.None))
        {
            foreach (var group in state.Groups)
                if (Selectable($"{group.Name}###wl-ms-mv-{group.Id}")) SendAssign(group.Id);

            Separator();
            if (Selectable("(Ungrouped)##wl-ms-mv-none")) SendAssign(null);
            EndCombo();
        }

        Spacing();
        if (Button($"Destroy ({count})##wl-ms-del", new Vector2(-1, 0)))
            RequestConfirm(ConfirmKind.DestroySelected, $"Destroy {count} spawned prop(s)?");

        Spacing();
        if (Button("Clear Selection##wl-ms-clear", new Vector2(-1, 0)))
            _selectedSpawnIds.Clear();
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
            var star  = isFav ? "Unfavorite###wl-fav-toggle" : "Favorite###wl-fav-toggle";
            if (Button(star, new Vector2(-1, 0)))
            {
                state.ToggleFavorite(_selectedLib.LoadKey ?? "");
                RebuildFilter(state);
            }

            Spacing();
            TextWrapped(_selectedLib.Name ?? "");
            TextWrapped(_selectedLib.Address ?? _selectedLib.Guid ?? "");

            RenderComponentList(state, _selectedLib.ComponentTypes, "wl-comp");
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

        TextWrapped(entry.Name ?? entry.Address ?? $"Prop #{entry.SpawnId}");
        TextDisabled(entry.Address ?? "");
        TextDisabled(entry.Networked ? "Networked" : "Local (Non-Networked)");

        Spacing();
        var groupName = entry.GroupId != null && state.GroupsById.TryGetValue(entry.GroupId, out var g)
            ? g.Name : "(Ungrouped)";

        AlignTextToFramePadding();
        TextDisabled("Group:");
        SameLine();
        SetNextItemWidth(-1);
        if (BeginCombo("##wl-sp-group", groupName, ImGuiComboFlags.None))
        {
            if (Selectable("(Ungrouped)##wl-sp-grp-none", entry.GroupId == null))
                SendAssign(null, new[] { entry.SpawnId });

            foreach (var group in state.Groups)
                if (Selectable($"{group.Name}###wl-sp-grp-{group.Id}", group.Id == entry.GroupId))
                    SendAssign(group.Id, new[] { entry.SpawnId });

            EndCombo();
        }

        RenderComponentList(state, entry.LibraryEntry?.ComponentTypes, "wl-sp-comp");

        Spacing();
        Separator();
        Spacing();

        if (Button("Destroy##wl-sp-del", new Vector2(-1, 0)))
            RequestConfirm(ConfirmKind.DestroySelected, "Destroy this prop?");

        Spacing();

        if (Button("Inspect (Unity Explorer)##wl-sp-insp", new Vector2(-1, 0)))
            SendSpawnedAction(new[] { entry.SpawnId }, "Inspect");
    }

    private void RenderComponentList(PropSpawnerState state, string[]? components, string idPrefix)
    {
        if (components is not { Length: > 0 }) return;

        Spacing();
        Separator();
        Text("Components");
        Separator();
        Spacing();

        for (var ci = 0; ci < components.Length; ci++)
        {
            var comp = components[ci];
            var shortName = PropSpawnerState.ShortTypeName(comp);
            var inFilter = _activeComps.Contains(shortName);

            if (inFilter) PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.9f, 0.4f, 1f));
            if (Selectable(shortName + "##" + idPrefix + "-" + ci + "-" + comp, inFilter,
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

    private void RenderCustomItemDetail(PropSpawnerState state)
    {
        Text("Custom Item");
        Separator();

        var (pi, ii) = _selectedCustomItem!.Value;
        if (pi >= state.CustomItemPacks.Count)
        {
            _selectedCustomItem = null;
            return;
        }

        var pack = state.CustomItemPacks[pi];
        if (pack.Items == null || ii >= pack.Items.Count)
        {
            _selectedCustomItem = null;
            return;
        }

        var item = pack.Items[ii];

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


    // ── modals ───────────────────────────────────────────────────────────────
    //
    // None of these use the `ref bool open` BeginPopupModal overload: Hexa.NET reports open ==
    // false on a modal's first visible frame, which closes it before it is ever seen. Every modal
    // gets an explicit Cancel instead of a title-bar close.
    //
    // Widths are pinned with ImGuiCond.Always, not Appearing, and the 0 height means "auto-fit
    // this axis". AlwaysAutoResize derives the window width from the content, while
    // SetNextItemWidth(-1) and TextWrapped derive the content width from the window — left to
    // themselves those two chase each other. A negative item width resolves to `avail - 1`, so
    // the content measures one pixel narrower than the space it was given and the window creeps
    // inward a pixel per frame. Setting the width every frame overrides auto-resize on X (which
    // is exactly what ImGui documents SetNextWindowSize as doing) and breaks the loop.

    private void RenderNewGroupModal(PropSpawnerState state)
    {
        SetNextWindowSize(new Vector2(380, 0), ImGuiCond.Always);
        if (!BeginPopupModal(ModalNewGroup,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        if (IsWindowAppearing())
        {
            _nameBuf = "";
            SetKeyboardFocusHere();
        }

        SetNextItemWidth(-1);
        var submitted = InputTextWithHint("##wl-newgrp-name", "Group name", ref _nameBuf, (UIntPtr)48,
                                          ImGuiInputTextFlags.EnterReturnsTrue);

        if (_newGroupTakesSelection)
            TextDisabled($"{_selectedSpawnIds.Count} selected prop(s) will move into it.");

        Spacing();
        Separator();
        Spacing();

        var valid = !string.IsNullOrWhiteSpace(_nameBuf);

        BeginDisabled(!valid);
        if ((Button("Create##wl-newgrp-ok", new Vector2(140, 0)) || submitted) && valid)
        {
            WobblyLifeOverlayPlugin.SendCommand(new PropGroupCommandMessage
            {
                Command    = PropGroupCommand.CreateGroup,
                Name       = _nameBuf.Trim(),
                // Assigning and activating are separate intents: creating a group to hold the
                // current selection should not silently redirect the next spawn as well.
                SpawnIds   = _newGroupTakesSelection ? _selectedSpawnIds.ToArray() : null,
                MakeActive = !_newGroupTakesSelection,
            });
            _newGroupTakesSelection = false;
            CloseCurrentPopup();
        }
        EndDisabled();

        SameLine();
        if (Button("Cancel##wl-newgrp-no", new Vector2(140, 0)) || IsKeyPressed(ImGuiKey.Escape))
        {
            _newGroupTakesSelection = false;
            CloseCurrentPopup();
        }

        EndPopup();
    }

    private void RenderRenameModal(PropSpawnerState state)
    {
        SetNextWindowSize(new Vector2(380, 0), ImGuiCond.Always);
        if (!BeginPopupModal(ModalRename,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        // A snapshot can delete the group out from under an open dialog.
        if (_modalGroupId == null || !state.GroupsById.ContainsKey(_modalGroupId))
        {
            CloseCurrentPopup();
            EndPopup();
            return;
        }

        if (IsWindowAppearing()) SetKeyboardFocusHere();

        SetNextItemWidth(-1);
        var submitted = InputTextWithHint("##wl-ren-name", "Group name", ref _nameBuf, (UIntPtr)48,
                                          ImGuiInputTextFlags.EnterReturnsTrue);

        TextDisabled("The file name stays the same, so copies others already have keep working.");

        Spacing();
        Separator();
        Spacing();

        var valid = !string.IsNullOrWhiteSpace(_nameBuf);

        BeginDisabled(!valid);
        if ((Button("Rename##wl-ren-ok", new Vector2(140, 0)) || submitted) && valid)
        {
            WobblyLifeOverlayPlugin.SendCommand(new PropGroupCommandMessage
            {
                Command = PropGroupCommand.RenameGroup,
                GroupId = _modalGroupId,
                Name    = _nameBuf.Trim(),
            });
            CloseCurrentPopup();
        }
        EndDisabled();

        SameLine();
        if (Button("Cancel##wl-ren-no", new Vector2(140, 0)) || IsKeyPressed(ImGuiKey.Escape))
            CloseCurrentPopup();

        EndPopup();
    }

    private void RenderConfirmModal(PropSpawnerState state)
    {
        SetNextWindowSize(new Vector2(420, 0), ImGuiCond.Always);
        if (!BeginPopupModal(ModalConfirm,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        var needsGroup = _confirmKind is ConfirmKind.DeleteGroup or ConfirmKind.DestroyGroup or ConfirmKind.SaveLossy;
        if (needsGroup && (_modalGroupId == null || !state.GroupsById.ContainsKey(_modalGroupId)))
        {
            CloseCurrentPopup();
            EndPopup();
            return;
        }

        TextWrapped(_confirmText);

        if (_confirmKind == ConfirmKind.DeleteGroup)
        {
            Spacing();
            Checkbox("Also delete the saved file##wl-del-file", ref _deleteAlsoFile);
            if (!_deleteAlsoFile)
                TextDisabled("The file stays on disk and the group reappears on Refresh.");
        }

        Spacing();
        Separator();
        Spacing();

        if (Button("Confirm##wl-cf-ok", new Vector2(160, 0)))
        {
            ExecuteConfirm(state);
            CloseCurrentPopup();
        }

        SameLine();
        if (Button("Cancel##wl-cf-no", new Vector2(160, 0)) || IsKeyPressed(ImGuiKey.Escape))
            CloseCurrentPopup();

        EndPopup();
    }

    private void ExecuteConfirm(PropSpawnerState state)
    {
        switch (_confirmKind)
        {
            case ConfirmKind.DeleteGroup:
                WobblyLifeOverlayPlugin.SendCommand(new PropGroupCommandMessage
                {
                    Command    = PropGroupCommand.DeleteGroup,
                    GroupId    = _modalGroupId,
                    DeleteFile = _deleteAlsoFile,
                });
                if (_selectedGroupId == _modalGroupId) _selectedGroupId = null;
                break;

            case ConfirmKind.DestroyGroup:
                SendGroupCommand(PropGroupCommand.DestroyGroup, _modalGroupId);
                break;

            case ConfirmKind.DestroySelected:
                SendSpawnedAction(_selectedSpawnIds.Where(state.SpawnedById.ContainsKey).ToArray(), "Delete");
                _selectedSpawnIds.Clear();
                break;

            case ConfirmKind.DestroyAll:
                SendGroupCommand(PropGroupCommand.DestroyAll, null);
                _selectedSpawnIds.Clear();
                break;

            case ConfirmKind.SaveLossy:
                SendGroupCommand(PropGroupCommand.SaveGroup, _modalGroupId);
                break;
        }

        _confirmKind = ConfirmKind.None;
    }

    private void RenderLoadModal(PropSpawnerState state)
    {
        SetNextWindowSize(new Vector2(440, 0), ImGuiCond.Always);
        if (!BeginPopupModal(ModalLoad,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            return;

        if (_modalGroupId == null || !state.GroupsById.TryGetValue(_modalGroupId, out var group))
        {
            CloseCurrentPopup();
            EndPopup();
            return;
        }

        TextWrapped($"Spawn {group.SavedCount} saved prop(s) from \"{group.Name}\".");

        Spacing();
        Separator();
        Spacing();

        Text("Placement");
        var atSaved = _loadMode == PropGroupLoadMode.AtSavedCoords;
        if (RadioButton("At saved world coordinates##wl-ld-saved", atSaved))
            _loadMode = PropGroupLoadMode.AtSavedCoords;
        if (RadioButton("At my position (rebased)##wl-ld-player", !atSaved))
            _loadMode = PropGroupLoadMode.RebaseToPlayer;

        Spacing();
        Text("Props already live in this group");
        if (RadioButton("Add alongside##wl-ld-add", !_loadReplace)) _loadReplace = false;
        if (RadioButton("Replace (destroy them first)##wl-ld-rep", _loadReplace)) _loadReplace = true;

        if (_loadReplace && group.LiveCount > 0)
            TextColored(DirtyColor, $"{group.LiveCount} live prop(s) will be destroyed.");

        Spacing();
        Separator();
        Spacing();

        if (Button("Spawn##wl-ld-ok", new Vector2(160, 0)))
        {
            WobblyLifeOverlayPlugin.SendCommand(new PropGroupCommandMessage
            {
                Command         = PropGroupCommand.LoadGroup,
                GroupId         = group.Id,
                LoadMode        = _loadMode,
                ReplaceExisting = _loadReplace,
            });
            CloseCurrentPopup();
        }

        SameLine();
        if (Button("Cancel##wl-ld-no", new Vector2(160, 0)) || IsKeyPressed(ImGuiKey.Escape))
            CloseCurrentPopup();

        EndPopup();
    }


    // ── selection helpers ────────────────────────────────────────────────────

    private void PruneSelection(PropSpawnerState state)
    {
        _selectedSpawnIds.RemoveWhere(id => !state.SpawnedById.ContainsKey(id));

        if (_selectedGroupId != null && !state.GroupsById.ContainsKey(_selectedGroupId))
            _selectedGroupId = null;

        if (_selAnchor.HasValue && !state.SpawnedById.ContainsKey(_selAnchor.Value))
        {
            _selAnchor      = null;
            _selAnchorGroup = null;
        }
    }

    private void SetAnchor(int spawnId, string groupKey)
    {
        _selAnchor      = spawnId;
        _selAnchorGroup = groupKey;
    }

    /// <summary>
    /// Ranges are scoped to one group's member list. That keeps the result deterministic without
    /// having to remember last frame's visible order, and a range spanning collapsible groups has
    /// no meaning anyway.
    /// </summary>
    private void SelectRange(List<SpawnedPropEntry> siblings, int fromId, int toId)
    {
        var from = siblings.FindIndex(e => e.SpawnId == fromId);
        var to   = siblings.FindIndex(e => e.SpawnId == toId);
        if (from < 0 || to < 0) return;

        if (from > to) (from, to) = (to, from);

        _selectedSpawnIds.Clear();
        for (var i = from; i <= to; i++) _selectedSpawnIds.Add(siblings[i].SpawnId);
    }

    private void SelectGroup(string groupId)
    {
        _selectedGroupId    = groupId;
        _selectedSpawnIds.Clear();
        _selectedLib        = null;
        _selectedCustomItem = null;
    }

    private void SelectLibrary(PropCacheEntry? entry)
    {
        _selectedLib        = entry;
        _selectedGroupId    = null;
        _selectedCustomItem = null;
        _selectedSpawnIds.Clear();
    }

    private void SelectCustomItem(int packIdx, int itemIdx)
    {
        _selectedCustomItem = (packIdx, itemIdx);
        _selectedLib        = null;
        _selectedGroupId    = null;
        _selectedSpawnIds.Clear();
    }

    // The overlay's key state is forwarded from the game window; these are the checks the rest of
    // the overlay already uses successfully.
    private static bool CtrlHeld()  => IsKeyDown(ImGuiKey.LeftCtrl)  || IsKeyDown(ImGuiKey.RightCtrl);
    private static bool ShiftHeld() => IsKeyDown(ImGuiKey.LeftShift) || IsKeyDown(ImGuiKey.RightShift);


    // ── filtering ────────────────────────────────────────────────────────────

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


    // ── requests and sends ───────────────────────────────────────────────────

    private void RequestModal(string id) => _modalToOpen = id;

    private void RequestConfirm(ConfirmKind kind, string text)
    {
        _confirmKind = kind;
        _confirmText = text;
        _modalToOpen = ModalConfirm;
    }

    private void RequestLoad(PropGroupData group)
    {
        _modalGroupId = group.Id;
        _loadReplace  = false;
        RequestModal(ModalLoad);
    }

    /// <summary>
    /// Save replaces the file with exactly what is live, so warn when that would drop props the
    /// file currently has — after a world unload, or when a restore could not place everything.
    /// </summary>
    private void RequestSave(PropGroupData group)
    {
        var dropped = group.SavedCount - group.LiveCount;
        if (dropped <= 0)
        {
            SendGroupCommand(PropGroupCommand.SaveGroup, group.Id);
            return;
        }

        _modalGroupId = group.Id;
        RequestConfirm(ConfirmKind.SaveLossy,
            $"\"{group.Name}\" has {group.SavedCount} saved prop(s) but only {group.LiveCount} live.\n\n" +
            $"Saving now replaces the file with the {group.LiveCount} live one(s); the other " +
            $"{dropped} would be dropped.");
    }

    private static void SendGroupCommand(PropGroupCommand command, string? groupId)
        => WobblyLifeOverlayPlugin.SendCommand(new PropGroupCommandMessage
        {
            Command = command,
            GroupId = groupId,
        });

    private void SendAssign(string? groupId, int[]? spawnIds = null)
        => WobblyLifeOverlayPlugin.SendCommand(new PropGroupCommandMessage
        {
            Command  = PropGroupCommand.AssignProps,
            GroupId  = groupId,
            SpawnIds = spawnIds ?? _selectedSpawnIds.ToArray(),
        });

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

    /// <summary>No optimistic local removal: the snapshot is the source of truth, and removing a
    /// row here only to have the next snapshot put it back reads as a flicker.</summary>
    private static void SendSpawnedAction(int[] spawnIds, string action)
    {
        if (spawnIds.Length == 0) return;

        var msg = new SpawnedPropActionMessage
        {
            SpawnId  = spawnIds[0],
            SpawnIds = spawnIds,
            Action   = action,
        };
        WobblyLifeOverlayPlugin.Context.SendToMod(JsonConvert.SerializeObject(msg.Serialize()));
    }

    private static string FormatAge(long unixSeconds)
    {
        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        if (age.TotalSeconds < 60) return "just now";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes} min ago";
        if (age.TotalHours   < 24) return $"{(int)age.TotalHours} h ago";
        return $"{(int)age.TotalDays} d ago";
    }
}
