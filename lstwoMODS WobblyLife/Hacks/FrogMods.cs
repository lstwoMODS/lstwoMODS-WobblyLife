﻿using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class FrogMods : PlayerBasedHack
    {
        public override string Name => "Frog Mods";
        public override string Description => "";
        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public PlayerFrog Frog
        {
            get
            {
                if (Player == null) return null;

                foreach (var frog in UnityEngine.Object.FindObjectsOfType<PlayerFrog>())
                {
                    if (frog.GetPlayerController() == Player.Controller)
                    {
                        return frog;
                    }
                }

                return null;
            }
        }

        public float MaxSpeed
        {
            get
            {
                var frog = Frog;

                if (!frog) return 0;

                var movement = frog.GetComponent<PlayerFrogMovement>();
                return (float)movement.GetType().GetField("maxSpeed", Plugin.Flags).GetValue(movement);
            }
            set
            {
                var frog = Frog;

                if (!frog) return;

                var movement = frog.GetComponent<PlayerFrogMovement>();
                movement.GetType().GetField("maxSpeed", Plugin.Flags).SetValue(movement, value);
            }
        }

        public float MovementSpeed
        {
            get
            {
                var frog = Frog;

                if (!frog) return 0;

                var movement = frog.GetComponent<PlayerFrogMovement>();
                return (float)movement.GetType().GetField("movementSpeed", Plugin.Flags).GetValue(movement);
            }
            set
            {
                var frog = Frog;

                if (!frog) return;

                var movement = frog.GetComponent<PlayerFrogMovement>();
                movement.GetType().GetField("movementSpeed", Plugin.Flags).SetValue(movement, value);
            }
        }

        public float JumpForce
        {
            get
            {
                var frog = Frog;

                if (!frog) return 0;

                var movement = frog.GetComponent<PlayerFrogMovement>();
                return (float)movement.GetType().GetField("jumpForce", Plugin.Flags).GetValue(movement);
            }
            set
            {
                var frog = Frog;

                if (!frog) return;

                var movement = frog.GetComponent<PlayerFrogMovement>();
                movement.GetType().GetField("jumpForce", Plugin.Flags).SetValue(movement, value);
            }
        }

        private InputFieldRef maxSpeedInput, moveSpeedInput, jumpForceInput;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var maxSpeedLIB = ui.CreateLIBTrio("Max Frog Speed", "lstwo.FrogMods.MaxFrogSpeed", "10.0");
            maxSpeedLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
            maxSpeedLIB.Button.OnClick = () => MaxSpeed = float.Parse(maxSpeedLIB.Input.Text);

            maxSpeedInput = maxSpeedLIB.Input;

            ui.AddSpacer(6);

            var moveSpeedLIB = ui.CreateLIBTrio("Frog Movement Speed", "lstwo.FrogMods.FrogMoveSpeed", "20.0");
            moveSpeedLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
            moveSpeedLIB.Button.OnClick = () => MovementSpeed = float.Parse(moveSpeedLIB.Input.Text);

            moveSpeedInput = moveSpeedLIB.Input;

            ui.AddSpacer(6);

            var jumpForceLIB = ui.CreateLIBTrio("Frog Jump Force", "lstwo.FrogMods.FrogJumpForce", "20.0");
            jumpForceLIB.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
            jumpForceLIB.Button.OnClick = () => JumpForce = float.Parse(jumpForceLIB.Input.Text);

            jumpForceInput = jumpForceLIB.Input;

            ui.AddSpacer(6);

            ui.CreateButton("Inspect \"Player Frog\" Component", () =>
            {
                if (Frog)
                {
                    InspectorManager.Inspect(Frog);
                    UIManager.ShowMenu = true;
                }
            }, "lstwo.FrogMods.Inspect", null, 256 * 3 + 32 * 2, 32);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            maxSpeedInput.Text = MaxSpeed.ToString();
            moveSpeedInput.Text = MovementSpeed.ToString();
            jumpForceInput.Text = JumpForce.ToString();
        }

        public override void Update()
        {
        }
    }
}
