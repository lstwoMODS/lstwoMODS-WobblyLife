using NotAzzamods.Hacks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;

namespace NotAzzamods.UI.TabMenus
{
    public class HacksTab : BaseTab
    {
        public bool enablePlayerDropdown;

        public List<BaseHack> Hacks = new();
        public PlayerRef Player;

        private Dropdown playerDropdown;
        private PlayerRef[] players;

        private GameObject infoHackRoot;
        private InfoHack infoHack;

        public HacksTab(string name = "Mods", bool enablePlayerDropdown = true)
        {
            Name = name;

            this.enablePlayerDropdown = enablePlayerDropdown;
        }

        public override void ConstructUI(GameObject root)
        {
            base.ConstructUI(root);

            playerDropdown = ui.CreateDropdown("playerDropdown", (index) =>
            {
                if (index < players.Length)
                {
                    Player = players[index];
                    infoHackRoot.SetActive(!Player.Controller.networkObject.IsOwner());

                    foreach (var hack in Hacks)
                    {
                        try
                        {
                            if (Player != null) hack.Player = Player;

                            hack.RefreshUI();
                        } catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }, "No Players");

            ui.AddSpacer(5);

            bool b = true;

            infoHack = new InfoHack();
            Hacks.Insert(0, infoHack);

            foreach (var hack in Hacks)
            {
                try
                {
                    GameObject newRoot = null;

                    new ShadowLib.UIHelper(root).AddSpacer(6);

                    var bgColor = b ? HacksUIHelper.BGColor1 : HacksUIHelper.BGColor2;

                    var fullHackRoot = UIFactory.CreateVerticalGroup(root, hack.Name, false, false, true, true, bgColor: bgColor);
                    UIFactory.SetLayoutElement(fullHackRoot);

                    if (hack == infoHack)
                    {
                        infoHackRoot = fullHackRoot;
                    }

                    var hackBtn = UIFactory.CreateButton(fullHackRoot, hack.Name + " Button", hack.Name, bgColor);
                    hackBtn.OnClick = () =>
                    {
                        newRoot.SetActive(!newRoot.activeSelf);

                        if (Player != null) hack.Player = Player;

                        if (newRoot.activeSelf)
                            hack.RefreshUI();
                    };
                    UIFactory.SetLayoutElement(hackBtn.GameObject, 0, 28, 9999, 0);

                    newRoot = UIFactory.CreateVerticalGroup(fullHackRoot, hack.Name, true, true, false, true, bgColor: bgColor);
                    UIFactory.SetLayoutElement(newRoot);

                    b = !b;

                    new ShadowLib.UIHelper(newRoot).AddSpacer(6);

                    hack.ConstructUI(newRoot);

                    new ShadowLib.UIHelper(newRoot).AddSpacer(6);

                    newRoot.SetActive(false);
                } catch (Exception e)
                {
                    Plugin.LogSource.LogError(e);
                }
            }
        }

        public override void RefreshUI()
        {
            playerDropdown.gameObject.SetActive(enablePlayerDropdown);

            if(enablePlayerDropdown && GameInstance.InstanceExists && GameInstance.Instance.GetPlayerControllers() != null)
            {
                playerDropdown.ClearOptions();

                var controllers = GameInstance.Instance.GetPlayerControllers();

                if (controllers.Count == 0) return;

                players = new PlayerRef[controllers.Count];

                for (int i = 0; i < players.Length; i++)
                {
                    var playerRef = new PlayerRef();
                    playerRef.SetPlayerController(controllers[i]);
                    players[i] = playerRef;

                    playerDropdown.options.Add(new(playerRef.Controller.GetPlayerName()));
                }

                Player = players[0];
                playerDropdown.value = 0;

                playerDropdown.RefreshShownValue();

                infoHackRoot.SetActive(!Player.Controller.networkObject.IsOwner());
            } 
            
            else if(!enablePlayerDropdown)
            {
                infoHackRoot.SetActive(false);
            }

            foreach (var hack in Hacks)
            {
                try
                {
                    hack.Player = Player;
                    hack.RefreshUI();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public override void UpdateUI()
        {

        }
    }

    public class PlayerRef
    {
        public PlayerController Controller { get; private set; }
        public PlayerControllerInputManager ControllerInputManager { get; private set; }
        public PlayerControllerUnlocker ControllerUnlocker { get; private set; }
        public PlayerControllerInputHint ControllerInputHint { get; private set; }
        public PlayerControllerUI ControllerUI { get; private set; }
        public PlayerControllerSettings ControllerSettings { get; private set; }
        public PlayerControllerSpectate ControllerSpectate { get; private set; }
        public PlayerControllerWaypoint ControllerWaypoint { get; private set; }
        public PlayerControllerEmployment Employment { get; private set; }

        public PlayerCharacter Character { get; private set; }
        public WorldDynamicObject WorldDynamicObject { get; private set; }
        public RagdollController RagdollController { get; private set; }
        public PlayerCharacterMovement CharacterMovement { get; private set; }
        public PlayerCharacterInput CharacterInput { get; private set; }
        public PlayerCharacterSound CharacterSound { get; private set; }
        public PlayerNPCDialog NPCDialog { get; private set; }

        public void SetPlayerController(PlayerController controller)
        {
            Controller = controller;
            ControllerInputManager = controller.GetPlayerControllerInputManager();
            ControllerUnlocker = controller.GetPlayerControllerUnlocker();
            ControllerInputHint = controller.GetPlayerControllerInputHint();
            ControllerUI = controller.GetPlayerControllerUI();
            ControllerSettings = controller.GetPlayerControllerSettings();
            ControllerSpectate = controller.GetPlayerControllerSpectate();
            ControllerWaypoint = controller.GetPlayerControllerWaypoint();
            Employment = controller.GetPlayerControllerEmployment();

            Character = controller.GetPlayerCharacter();
            WorldDynamicObject = Character.GetWorldDynamicObject();
            RagdollController = Character.GetRagdollController();
            CharacterMovement = Character.GetPlayerCharacterMovement();
            CharacterInput = Character.GetPlayerCharacterInput();
            CharacterSound = Character.GetPlayerCharacterSound();
            NPCDialog = Character.GetComponent<PlayerNPCDialog>();
        }

        public void SetPlayerCharacter(PlayerCharacter character)
        {
            SetPlayerController(character.GetPlayerController());
        }
    }
}
