using lstwoMODS_WobblyLife.UI.TabMenus;
using RuntimeInspectorNamespace;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using UnityEngine.Rendering.PostProcessing;
using UnityExplorer.UI;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class RoadVehicleModifier : PlayerBasedHack
    {
        public override string Name => "Road Vehicle Modifier";
        public override string Description => "";
        public override HacksTab HacksTab => Plugin.VehicleHacksTab;

        private PlayerVehicleRoad vehicle;
        private PlayerVehicleRoadMovement movement;
        private GameObject root;

        private HacksUIHelper.LIBTrio damageSpeed, topSpeedMph, boostPow, boostSeconds, downForce, wheelStiffness;

        private Toggle indestructable, lockMovement, isPersonal, hasBoost;

        public override void ConstructUI(GameObject root)
        {
            this.root = root;
            
            var ui = new HacksUIHelper(root);
            
            ui.AddSpacer(6);
            
            indestructable = ui.CreateToggle("lstwo.RoadVehicle.indestructible", "Is indestructible", (b) => vehicle.SetIndestructable(this, b));
            lockMovement = ui.CreateToggle("lstwo.RoadVehicle.lock movement", "Lock movement", (b) => vehicle.SetLockMovement(this, b));
            isPersonal = ui.CreateToggle("lstwo.RoadVehicle.is personal", "Is personal vehicle", (b) => vehicle.SetIsPersonalVehicle(b));

            ui.AddSpacer(6);

            damageSpeed = ui.CreateLIBTrio("Set damage speed multiplier", "lstwo.RoadVehicle.damage speed", "multiplier");
            damageSpeed.Button.OnClick = () => movement.SetDamageSpeedMul(float.Parse(damageSpeed.Input.Text));
            damageSpeed.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            topSpeedMph = ui.CreateLIBTrio("Set top speed in mph", "lstwo.RoadVehicle.top speed", "Top speed in mph");
            topSpeedMph.Button.OnClick = () =>
            {
                movement.GetType().GetField("defaultTopSpeedMph", Plugin.Flags).SetValue(movement, float.Parse(topSpeedMph.Input.Text));
                movement.SetTopSpeedMPHDefault();
            };
            topSpeedMph.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            downForce = ui.CreateLIBTrio("Down force", "lstwo.RoadVehicle.down force", "Down Force");
            downForce.Button.OnClick = () => movement.GetType().GetField("downForce", Plugin.Flags).SetValue(movement, float.Parse(downForce.Input.Text));
            downForce.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            wheelStiffness = ui.CreateLIBTrio("Wheel stiffness", "lstwo.RoadVehicle.wheel stiffness");
            wheelStiffness.Button.OnClick = () => movement.GetType().GetField("wheelStiffness", Plugin.Flags).SetValue(movement, float.Parse(wheelStiffness.Input.Text));
            wheelStiffness.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(12);

            hasBoost = ui.CreateToggle("lstwo.RoadVehicle.has boost", "Has Boost", (b) =>
            {
                movement.GetType().GetField("bAllowBoost", Plugin.Flags).SetValue(movement, b);

                if (!ServerSettings.PlayerVehicleRoadMovementPatch.boostEnabled.TryAdd(movement, b))
                {
                    ServerSettings.PlayerVehicleRoadMovementPatch.boostEnabled[movement] = b;
                }

                try
                {
                    WobblyServerUtilCompat.AddToBoostList(movement, b);
                }
                catch { }
            });

            ui.AddSpacer(6);

            boostPow = ui.CreateLIBTrio("Boost power", "lstwo.RoadVehicle.boost pow", "Boost Power");
            boostPow.Button.OnClick = () => movement.GetType().GetField("boostPow", Plugin.Flags).SetValue(movement, float.Parse(boostPow.Input.Text));
            boostPow.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            boostSeconds = ui.CreateLIBTrio("Boost Seconds", "lstwo.RoadVehicle.Boost Seconds", "Seconds");
            boostSeconds.Button.OnClick = () => movement.GetType().GetField("boostSeconds", Plugin.Flags).SetValue(movement, float.Parse(boostSeconds.Input.Text));
            boostSeconds.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;

            ui.AddSpacer(6);

            ui.CreateButton("Inspect Road Vehicle Game Object", () =>
            {
                if (vehicle)
                {
                    InspectorManager.Inspect(vehicle.gameObject);
                    UIManager.ShowMenu = true;
                }
            }, "lstwo.RoadVehicle.inspect", null, 256 * 3 + 32 * 2, 32);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            if (Player == null) return;

            var a = Player.Controller.GetPlayerControllerInteractor().GetEnteredAction();
            if (a != null && a is global::ActionEnterExitInteract action && action.TryGetComponent<PlayerVehicleRoad>(out var vehicle))
            {
                this.vehicle = vehicle;
                movement = (PlayerVehicleRoadMovement)vehicle.GetVehicleMovementBase();

                var vr = new QuickReflection<PlayerVehicleRoad>(vehicle, Plugin.Flags);

                indestructable.isOn = (bool)vr.GetField("bIndestructable");
                isPersonal.isOn = (bool)vr.GetField("bIsAPersonalVehicle");

                var mr = new QuickReflection<PlayerVehicleRoadMovement>(movement, Plugin.Flags);

                lockMovement.isOn = !((List<object>)typeof(PlayerVehicleMovement).GetField("lockMovementHandles", Plugin.Flags).GetValue(movement)).IsEmpty();
                damageSpeed.Input.Text = mr.GetField("damageSpeedMul").ToString();
                topSpeedMph.Input.Text = mr.GetField("topSpeedMph").ToString();
                downForce.Input.Text = mr.GetField("downForce").ToString();
                wheelStiffness.Input.Text = mr.GetField("wheelStiffness").ToString();
                hasBoost.isOn = (bool)mr.GetField("bAllowBoost");
                boostPow.Input.Text = mr.GetField("boostPow").ToString();
                boostSeconds.Input.Text = mr.GetField("boostSeconds").ToString();

                root.SetActive(true);
            }
            else
            {
                root.SetActive(false);
            }
        }

        public override void Update()
        {
        }
    }
}
