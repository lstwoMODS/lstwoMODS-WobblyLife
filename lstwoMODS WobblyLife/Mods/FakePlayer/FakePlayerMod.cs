using System.Linq;
using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using Button = lstwoMODS_Core.UI.Elements.Button;

namespace lstwoMODS_WobblyLife.Mods.FakePlayer;

public class FakePlayerMod : BaseMod
{
    public override string Name => "Fake Player";
    public override string Description => "Spawn fake players you can take control of (host-only).";
    public override ModsWindow ModsWindow => null; //Plugin.ExtraModsWindow;

    private readonly Ref<bool> _withController = new();
    private readonly Ref<bool> _copyClothing = new();
    private readonly Ref<int> _sourcePlayerIndex = new();
    private readonly Ref<string[]> _sourcePlayerNames = new([]);
    private readonly Ref<string> _fakeName = new("Fake Player");

    private readonly Ref<int> _selectedFakeIndex = new();
    private readonly Ref<string[]> _fakeNames = new([]);
    private readonly Ref<bool> _hasFakes = new();
    private readonly Ref<bool> _noFakes = new(true);
    private readonly Ref<string> _controlStatus = new("No fake selected");

    protected override void OnStaticInit()
    {
        new Harmony("net.lstwo.lstwoMODS.WobblyLife.FakePlayer").PatchAll(typeof(FakePlayerPatches));

        FakePlayerManager.Spawned += _ => RefreshFakeList();
        FakePlayerManager.Despawned += _ => RefreshFakeList();
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new SeparatorText("spawn-sep", "Spawn"),
            new Checkbox("Also create a PlayerController", false, b => _withController.Value = b),
            new Checkbox("Copy clothing from a player", false, b => _copyClothing.Value = b),
            new Group("clothing-source-group",
                new Combo("Copy clothing from", [], 0).WithItems(_sourcePlayerNames).WithSelectedIndex(_sourcePlayerIndex)
            ).WithVisible(_copyClothing),
            new InputText("Name").WithValue(_fakeName),
            new Button("Spawn Fake Player", Spawn).WithContentWidth(),

            new SeparatorText("active-sep", "Active Fakes"),
            new UIText("none-text", "No fake players spawned.").WithVisible(_noFakes),

            new Group("fake-controls",
                new Combo("Select fake", [], 0, _ => UpdateControlStatus()).WithItems(_fakeNames).WithSelectedIndex(_selectedFakeIndex),
                new UIText("control-status", "").WithText(_controlStatus),

                new HStack("fake-actions",
                    new Button("Take Control", TakeControlSelected),
                    new Button("Release Control", ReleaseControlSelected),
                    new Button("Despawn", DespawnSelected)
                ).WithContentWidth(),

                new Button("Despawn All", FakePlayerManager.DespawnAll).WithContentWidth()
            ).WithVisible(_hasFakes)
        );
    }

    public override void RefreshUI()
    {
        RefreshSourcePlayerList();
        RefreshFakeList();
    }

    private void RefreshSourcePlayerList()
    {
        if (!GameInstance.InstanceExists) return;
        var controllers = GameInstance.Instance.GetLocalPlayerControllers();
        if (controllers == null) return;

        _sourcePlayerNames.Value = controllers.Select(c => c.GetPlayerName() ?? "Player").ToArray();
        if (_sourcePlayerIndex.Value >= _sourcePlayerNames.Value.Length) _sourcePlayerIndex.Value = 0;
    }

    private void RefreshFakeList()
    {
        var fakes = FakePlayerManager.ActiveFakePlayers;
        _fakeNames.Value = fakes.Select((f, i) => $"{i}: {f.Name}{(f.HasController ? "" : " (no controller)")}{(f.IsBeingControlled ? " [controlled]" : "")}").ToArray();
        _hasFakes.Value = fakes.Count > 0;
        _noFakes.Value = fakes.Count == 0;
        if (_selectedFakeIndex.Value >= fakes.Count) _selectedFakeIndex.Value = 0;
        UpdateControlStatus();
    }

    private void UpdateControlStatus()
    {
        var fake = GetSelectedFake();
        _controlStatus.Value = fake == null ? "No fake selected" : fake.IsBeingControlled ? $"Currently controlled by: {fake.ControllingSource?.GetPlayerName() ?? "?"}" : "Not controlled";
    }

    private PlayerController GetClothingSourceController()
    {
        if (!GameInstance.InstanceExists) return null;
        var controllers = GameInstance.Instance.GetLocalPlayerControllers();
        if (controllers == null || controllers.Count == 0) return null;
        var idx = Mathf.Clamp(_sourcePlayerIndex.Value, 0, controllers.Count - 1);
        return controllers[idx];
    }

    private static PlayerController GetControlSource()
        => GameInstance.InstanceExists ? GameInstance.Instance.GetFirstLocalPlayerController() : null;

    private FakePlayerInstance GetSelectedFake()
    {
        var fakes = FakePlayerManager.ActiveFakePlayers;
        if (fakes.Count == 0) return null;
        var idx = Mathf.Clamp(_selectedFakeIndex.Value, 0, fakes.Count - 1);
        return fakes[idx];
    }

    private void Spawn()
    {
        if (!FakePlayerManager.CanSpawn)
        {
            Plugin.LogSource.LogWarning("[FakePlayer] Only the host can spawn fake players.");
            return;
        }

        var clothingSource = _copyClothing.Value ? GetClothingSourceController()?.GetPlayerCharacter() : null;
        var spawnAnchor = GetControlSource()?.GetPlayerCharacter() ?? clothingSource;
        var position = spawnAnchor != null ? spawnAnchor.GetPlayerPosition() + spawnAnchor.GetPlayerForward() * 2f : (Vector3?)null;

        FakePlayerManager.Spawn(_withController.Value, clothingSource, position, _fakeName.Value, _ => RefreshFakeList());
    }

    private void TakeControlSelected()
    {
        var fake = GetSelectedFake();
        var source = GetControlSource();
        if (fake == null || source == null) return;

        fake.TakeControl(source);
        UpdateControlStatus();
        RefreshFakeList();
    }

    private void ReleaseControlSelected()
    {
        var fake = GetSelectedFake();
        if (fake == null) return;

        fake.ReleaseControl();
        UpdateControlStatus();
        RefreshFakeList();
    }

    private void DespawnSelected()
    {
        var fake = GetSelectedFake();
        if (fake == null) return;

        FakePlayerManager.Despawn(fake);
    }
}
