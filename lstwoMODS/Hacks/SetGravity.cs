using UnityEngine;
using UniverseLib.UI;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using NotAzzamods.UI.TabMenus;

namespace NotAzzamods.Hacks
{
    public class SetGravity : BaseHack
    {
        public override string Name => "Set Gravity";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ServerHacksTab;

        private InputFieldRef inputX;
        private InputFieldRef inputY;
        private InputFieldRef inputZ;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var lb = ui.CreateLBDuo("Set Gravity (x, y, z)", "SetGravity");

            ui.AddSpacer(6);

            var group = ui.CreateHorizontalGroup("GravityInputs", true, true, true, true);

            inputX = UIFactory.CreateInputField(group, "x", " X (0)");
            inputX.Component.characterValidation = InputField.CharacterValidation.Decimal;
            inputX.Component.image.sprite = HacksUIHelper.RoundedRect;
            UIFactory.SetLayoutElement(inputX.GameObject, 256, 32);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", group), 32, 32);

            inputY = UIFactory.CreateInputField(group, "y", " Y (19.62)");
            inputY.Component.characterValidation = InputField.CharacterValidation.Decimal;
            inputY.Component.image.sprite = HacksUIHelper.RoundedRect;
            UIFactory.SetLayoutElement(inputY.GameObject, 256, 32);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", group), 32, 32);

            inputZ = UIFactory.CreateInputField(group, "z", " Z (0)");
            inputZ.Component.characterValidation = InputField.CharacterValidation.Decimal;
            inputZ.Component.image.sprite = HacksUIHelper.RoundedRect;
            UIFactory.SetLayoutElement(inputZ.GameObject, 256, 32);

            ui.AddSpacer(6);

            lb.Button.OnClick = () =>
            {
                Physics.gravity = new Vector3(float.Parse(inputX.Text.Trim()), float.Parse(inputY.Text.Trim()), float.Parse(inputZ.Text.Trim()));
            };

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            inputX.Text = Physics.gravity.x.ToString();
            inputY.Text = Physics.gravity.y.ToString();
            inputZ.Text = Physics.gravity.z.ToString();
        }

        public override void Update()
        {
        }
    }
}
