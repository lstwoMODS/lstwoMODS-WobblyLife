using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Hacks;
using System;
using System.CodeDom;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;

namespace lstwoMODS_WobblyLife.UI.TabMenus
{
    public class PlayerBasedHacksTab : HacksTab
    {
        public List<PlayerBasedHack> PlayerBasedHacks = new();
        public PlayerRef Player;

        private Dropdown playerDropdown;
        private PlayerRef[] players;

        private GameObject infoHackRoot;
        private InfoHack infoHack;

        public PlayerBasedHacksTab(Sprite icon, string name = "Mods") : base(icon)
        {
            Name = name;
        }

        public override void ConstructUI(GameObject root)
        {
            this.root = root;
            ui = new HacksUIHelper(root);

            root.SetActive(false);

            foreach(var hack in Hacks)
            {
                if(hack is PlayerBasedHack playerBasedHack)
                {
                    PlayerBasedHacks.Add(playerBasedHack);
                }
            }

            playerDropdown = ui.CreateDropdown("lstwo.PlayerBasedHack.playerDropdown", (index) =>
            {
                if (index < players.Length)
                {
                    Player = players[index];
                    infoHackRoot.SetActive(!Player.Controller.networkObject.IsOwner());

                    foreach (var hack in PlayerBasedHacks)
                    {
                        try
                        {
                            if (Player != null)
                            {
                                hack.Player = Player;
                            }

                            hack.RefreshUI();
                        } 
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }, "No Players");

            ui.AddSpacer(5);

            bool b = true;

            infoHack = new InfoHack();
            PlayerBasedHacks.Insert(0, infoHack);

            foreach (var hack in PlayerBasedHacks)
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

                        if (Player != null)
                        {
                            hack.Player = Player;
                        }

                        if (newRoot.activeSelf)
                        {
                            hack.RefreshUI();
                        }
                    };
                    UIFactory.SetLayoutElement(hackBtn.GameObject, 0, 28, 9999, 0);

                    newRoot = UIFactory.CreateVerticalGroup(fullHackRoot, hack.Name, true, true, false, true, bgColor: bgColor);
                    UIFactory.SetLayoutElement(newRoot);

                    b = !b;

                    new ShadowLib.UIHelper(newRoot).AddSpacer(6);

                    hack.ConstructUI(newRoot);

                    new ShadowLib.UIHelper(newRoot).AddSpacer(6);

                    newRoot.SetActive(false);
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
                playerDropdown.ClearOptions();

                var controllers = GameInstance.Instance.GetPlayerControllers();

                if (controllers.Count == 0)
                {
                    return;
                }

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

            foreach (var hack in PlayerBasedHacks)
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
}
