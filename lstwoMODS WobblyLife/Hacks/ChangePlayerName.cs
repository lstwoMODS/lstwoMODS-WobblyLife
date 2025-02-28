using UnityEngine;
using UniverseLib.UI.Models;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class ChangePlayerName : PlayerBasedHack
    {
        public override string Name => "Change Player Name";

        public override string Description => "Allows you to Change the Players Name!";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        private InputFieldRef nameInput;

        public void execute(string newName)
        {
            if (Player == null) return;

            Player.Controller.SetServerPlayerName(newName);
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var lib = ui.CreateLIBTrio("Player Name", "lstwo.ChangePlayerName.Name", "Name");
            lib.Button.OnClick = () => execute(lib.Input.Text);
            nameInput = lib.Input;

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            if (Player != null)
            {
                nameInput.Text = Player.Controller.GetPlayerName();
            }
        }

        public override void Update()
        {

        }
    }
}
