using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Mods;
using System;
using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityEngine;

namespace lstwoMODS_WobblyLife.UI.TabMenus;

public class PlayerBasedModsWindow(string name, string icon = "") : ModsWindow(name, icon)
{
    public PlayerRef Player;

    private string filterText;

    public static PlayerRef[] players = Array.Empty<PlayerRef>();
    public static Ref<string[]> playerNames = new(Array.Empty<string>());

    private Ref<int> currentSelectedPlayerIndex = new();
    private Ref<bool> otherPlayerInfoEnabled = new();

    private bool hookedControllerEvents;

    public override Group ConstructUI()
    {
        return new Group(Name,

            new Group("OtherPlayerInfo",
                new SeparatorText("Info", "Info"),
                new TextWrapped("Text", "Some hacks were disabled and don't work on other players! " +
                                        "This is to prevent people from tampering with other people's save files, " +
                                        "because some hacks would allow users to reset players money, lock all their clothes, etc.")
            ).WithVisible(otherPlayerInfoEnabled),

            new Combo("PlayerDropdown", [], 0, i =>
            {
                if (i >= players.Length)
                {
                    return;
                }

                Player = players[i];
                otherPlayerInfoEnabled.Value = Player == null || !Player.Controller.networkObject.IsOwner();

                foreach (var mod in Mods.OfType<PlayerBasedMod>())
                {
                    try
                    {
                        if (Player != null)
                        {
                            mod.Player = Player;
                        }

                        mod.RefreshUI();
                    }
                    catch (Exception e)
                    {
                        Plugin.LogSource.LogError(e);
                    }
                }

            }).WithSelectedIndex(currentSelectedPlayerIndex).WithItems(playerNames),

            new Spacing("spacing"),
            new Separator("Separator"),
            new Spacing("spacing"),
            
            base.ConstructUI()
        );
    }

    public override void RefreshUI()
    {
        EnsureControllerEventsHooked();
        RebuildPlayerList();
        otherPlayerInfoEnabled.Value = Player == null || !Player.Controller.networkObject.IsOwner();
        base.RefreshUI();
    }

    private void EnsureControllerEventsHooked()
    {
        if (hookedControllerEvents) return;
        hookedControllerEvents = true;

        GameInstance.onAssignedPlayerController   += _ => MainThread.Enqueue(RebuildPlayerList);
        GameInstance.onUnassignedPlayerController += _ => MainThread.Enqueue(RebuildPlayerList);
    }

    private void RebuildPlayerList()
    {
        var controllers = GameInstance.InstanceExists ? GameInstance.Instance.GetPlayerControllers() : null;

        var previouslySelected = Player?.Controller;

        var valid = new List<PlayerController>();
        if (controllers != null)
        {
            foreach (var controller in controllers)
            {
                if (controller)
                {
                    valid.Add(controller);
                }
            }
        }

        if (valid.Count == 0)
        {
            players = Array.Empty<PlayerRef>();
            playerNames.Value = Array.Empty<string>();
            currentSelectedPlayerIndex.Value = 0;
            Player = null;
            otherPlayerInfoEnabled.Value = false;
            PropagatePlayerToMods();
            return;
        }

        var newPlayers = new PlayerRef[valid.Count];
        var newNames = new string[valid.Count];
        var selectedIndex = 0;

        for (var i = 0; i < valid.Count; i++)
        {
            var playerRef = new PlayerRef();
            playerRef.SetPlayerController(valid[i]);
            newPlayers[i] = playerRef;
            newNames[i] = valid[i].GetPlayerName();

            if (previouslySelected != null && valid[i] == previouslySelected)
            {
                selectedIndex = i;
            }
        }

        players = newPlayers;
        Player = players[selectedIndex];
        currentSelectedPlayerIndex.Value = selectedIndex;
        playerNames.Value = newNames;
        otherPlayerInfoEnabled.Value = Player?.Controller == null || !Player.Controller.networkObject.IsOwner();

        PropagatePlayerToMods();
    }

    private void PropagatePlayerToMods()
    {
        foreach (var mod in Mods.OfType<PlayerBasedMod>())
        {
            try { mod.Player = Player; }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}

public class PlayerRef
{
    public PlayerController Controller { get; private set; }

    public PlayerControllerInputManager ControllerInputManager => Controller?.GetPlayerControllerInputManager();
    public PlayerControllerUnlocker ControllerUnlocker => Controller?.GetPlayerControllerUnlocker();
    public PlayerControllerInputHint ControllerInputHint => Controller?.GetPlayerControllerInputHint();
    public PlayerControllerSettings ControllerSettings => Controller?.GetPlayerControllerSettings();
    public PlayerControllerSpectate ControllerSpectate => Controller?.GetPlayerControllerSpectate();
    public PlayerControllerWaypoint ControllerWaypoint => Controller?.GetPlayerControllerWaypoint();
    public PlayerControllerEmployment ControllerEmployment => Controller?.GetPlayerControllerEmployment();
    public PlayerControllerInteractor ControllerInteractor => Controller?.GetPlayerControllerInteractor();
    public PlayerControllerPet ControllerPet => Controller?.GetPlayerControllerPet();
    
    public MinimapCamera MinimapCamera => Controller?.GetMinimapCamera();
    public GameplayCamera GameplayCamera => Controller?.GetGameplayCamera();
    public FreeCamera FreeCamera => Controller?.GetFreeCamera();
    public CameraFocus CurrentCameraFocus => Controller?.GetCurrentCameraFocus();
    
    public MinimapIcon MinimapIcon => Controller?.GetComponent<MinimapIcon>();
    public PlayerBasedUI PlayerBasedUI => Controller?.GetPlayerBasedUI();
    public PlayerControllerUI ControllerUI => Controller?.GetPlayerControllerUI();
    public BaseGameState GameState => Controller?.GetGameState();
    public SavePlayerSettingsData Settings => Controller?.GetPlayerSettings();
    public SavePlayerPersistentData PersistentData => Controller?.GetPlayerPersistentData();
    public List<RespawnOption> RespawnOptions => Controller?.GetRespawnOptions();
    public RespawnSystem RespawnSystem => Controller?.GetRespawnSystem();

    public PlayerCharacter Character => Controller?.GetPlayerCharacter();
    public WorldDynamicObject WorldDynamicObject => Character?.GetWorldDynamicObject();
    public RagdollController RagdollController => Character?.GetRagdollController();
    public PlayerCharacterMovement CharacterMovement => Character?.GetPlayerCharacterMovement();
    public PlayerCharacterInput CharacterInput => Character?.GetPlayerCharacterInput();
    public PlayerCharacterSound CharacterSound => Character?.GetPlayerCharacterSound();
    public PlayerNPCDialog NPCDialog => Character?.GetComponent<PlayerNPCDialog>();
    public CharacterCustomize CharacterCustomize => Character?.GetPlayerCharacterCustomize();
    public CharacterFace CharacterFace => Character?.GetComponent<CharacterFace>();
    public PlayerBody Body => Character?.GetPlayerBody();

    public void SetPlayerController(PlayerController controller)
    {
        Controller = controller;
    }

    public void SetPlayerCharacter(PlayerCharacter character)
    {
        SetPlayerController(character.GetPlayerController());
    }

    public static implicit operator PlayerRef(PlayerController controller)
    {
        var playerRef = new PlayerRef();
        playerRef.SetPlayerController(controller);
        return playerRef;
    }

    public static implicit operator PlayerController(PlayerRef playerRef)
    {
        return playerRef.Controller;
    }
    
    public static explicit operator PlayerRef(PlayerCharacter character)
    {
        var playerRef = new PlayerRef();
        playerRef.SetPlayerCharacter(character);
        return playerRef;
    }

    public static explicit operator PlayerCharacter(PlayerRef playerRef)
    {
        return playerRef.Character;
    }
}