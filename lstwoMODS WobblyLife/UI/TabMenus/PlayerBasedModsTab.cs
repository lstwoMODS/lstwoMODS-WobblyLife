using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Hacks;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using lstwoMODS_Core.UI;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;

namespace lstwoMODS_WobblyLife.UI.TabMenus;

public class PlayerBasedModsTab(string name) : ModsTab(name)
{
    public PlayerRef Player;

    private PlayerRef[] players;
    private string[] playerNames;
    private string filterText;

    private int currentSelectedPlayerIndex;

    public override void RenderUI()
    {
        var oldSelectedPlayerIndex = currentSelectedPlayerIndex;
        ImGui.Combo("Select Player", ref currentSelectedPlayerIndex, playerNames, players?.Length ?? 0);

        if (oldSelectedPlayerIndex != currentSelectedPlayerIndex && currentSelectedPlayerIndex < players.Length)
        {
            Player = players[currentSelectedPlayerIndex];

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
                    Debug.LogException(e);
                }
            }
        }

        if (Player == null || !Player.Controller.networkObject.IsOwner())
        {
            ModsUIHelper.TextSeparator("Info");
            ImGui.TextWrapped("Some hacks were disabled and don't work on other players! " +
                       "This is to prevent people from tampering with other people's save files, " +
                       "because some hacks would allow users to reset players money, lock all their clothes, etc.");
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var mod in Mods.OfType<PlayerBasedMod>())
        {
            try
            {
                RenderModUI(mod);
            } 
            catch (Exception e)
            {
                Plugin.LogSource.LogError(e);
            }
        }
    }

    public override void RefreshUI()
    {
        if(GameInstance.InstanceExists && GameInstance.Instance.GetPlayerControllers() != null)
        {
            var controllers = GameInstance.Instance.GetPlayerControllers();

            if (controllers.Count == 0)
            {
                return;
            }

            players = new PlayerRef[controllers.Count];
            playerNames = new string[controllers.Count];

            for (var i = 0; i < players.Length; i++)
            {
                var playerRef = new PlayerRef();
                playerRef.SetPlayerController(controllers[i]);
                players[i] = playerRef;
                playerNames[i] = controllers[i].GetPlayerName();
            }

            Player = players[0];
        }

        foreach (var mod in Mods.OfType<PlayerBasedMod>())
        {
            try
            {
                mod.Player = Player;
                mod.RefreshUI();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}

public class PlayerRef
{
    public PlayerController Controller { get; private set; }

    public PlayerControllerInputManager ControllerInputManager => Controller?.GetPlayerControllerInputManager();
    public PlayerControllerUnlocker ControllerUnlocker => Controller?.GetPlayerControllerUnlocker();
    public PlayerControllerInputHint ControllerInputHint => Controller?.GetPlayerControllerInputHint();
    public PlayerControllerUI ControllerUI => Controller?.GetPlayerControllerUI();
    public PlayerControllerSettings ControllerSettings => Controller?.GetPlayerControllerSettings();
    public PlayerControllerSpectate ControllerSpectate => Controller?.GetPlayerControllerSpectate();
    public PlayerControllerWaypoint ControllerWaypoint => Controller?.GetPlayerControllerWaypoint();
    public PlayerControllerEmployment ControllerEmployment => Controller?.GetPlayerControllerEmployment();
    public PlayerControllerInteractor ControllerInteractor => Controller?.GetPlayerControllerInteractor();
    public MinimapIcon MinimapIcon => Controller?.GetComponent<MinimapIcon>();
    public PlayerControllerPet ControllerPet => Controller?.GetPlayerControllerPet();

    public PlayerCharacter Character => Controller?.GetPlayerCharacter();
    public WorldDynamicObject WorldDynamicObject => Character?.GetWorldDynamicObject();
    public RagdollController RagdollController => Character?.GetRagdollController();
    public PlayerCharacterMovement CharacterMovement => Character?.GetPlayerCharacterMovement();
    public PlayerCharacterInput CharacterInput => Character?.GetPlayerCharacterInput();
    public PlayerCharacterSound CharacterSound => Character?.GetPlayerCharacterSound();
    public PlayerNPCDialog NPCDialog => Character?.GetComponent<PlayerNPCDialog>();
    public CharacterCustomize CharacterCustomize => Character?.GetPlayerCharacterCustomize();
    public CharacterFace CharacterFace => Character?.GetComponent<CharacterFace>();

    public void SetPlayerController(PlayerController controller)
    {
        Controller = controller;
    }

    public void SetPlayerCharacter(PlayerCharacter character)
    {
        SetPlayerController(character.GetPlayerController());
    }
}