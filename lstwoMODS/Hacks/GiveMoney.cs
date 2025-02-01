using IngameDebugConsole;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using lstwoMODS_WobblyLife.UI.TabMenus;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class GiveMoney : PlayerBasedHack
    {
        public override string Name => "Give Money";

        public override string Description => "Give yourself any Amount of Money!";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var giveMoney = ui.CreateLIBTrio("Give Money", "GiveMoney", "Money to Give", null, "Give");
            giveMoney.Button.OnClick = () => _GiveMoney(int.Parse(giveMoney.Input.Text));
            giveMoney.Input.Component.characterValidation = InputField.CharacterValidation.Integer;

            ui.AddSpacer(6);

            ui.CreateLBDuo("Reset Money", "ResetMoney", ResetMoney, "Reset");

            ui.AddSpacer(6);

            var spawnMoney = ui.CreateLIBTrio("Spawn Money Bag", "SpawnMoney", "Money to Spawn", null, "Give");
            spawnMoney.Button.OnClick = () => SpawnMoney(int.Parse(spawnMoney.Input.Text));
            spawnMoney.Input.Component.characterValidation = InputField.CharacterValidation.Integer;

            ui.AddSpacer(6);
        }

        public override void Update() { }

        public override void RefreshUI() { }

        public void _GiveMoney(int money)
        {
            if (Player == null) return;
            if (!Player.Controller.networkObject.IsOwner()) return;

            Player.Controller.GetPlayerControllerEmployment().UpdateMoney(money);
        }

        public void ResetMoney()
        {
            if (Player == null) return;
            if (!Player.Controller.networkObject.IsOwner()) return;

            Player.Controller.GetPlayerControllerEmployment().UpdateMoney(-Player.Controller.GetPlayerControllerEmployment().GetLocalMoney());
        }

        public void SpawnMoney(int amount)
        {
            RewardManagerInstance.Instance.ServerReward(Player.Controller, RewardType.MoneyBag, amount);
        }
    }
}
