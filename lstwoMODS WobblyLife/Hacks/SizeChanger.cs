using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class SizeChanger : PlayerBasedHack
    {
        public override string Name => "Size Changer";
        public override string Description => "";
        public override HacksTab HacksTab => null;

        public float PlayerSize
        {
            get
            {
                var ragdollParts = Player.Character.GetComponentsInChildren<RagdollPart>();
                return ragdollParts[0].transform.localScale.x;
            }
            set
            {
                var ragdollParts = Player.Character.GetComponentsInChildren<RagdollPart>();

                foreach(var part in ragdollParts)
                {
                    part.transform.localScale = new(value, value, value);
                }
            }
        }

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var lib = ui.CreateLIBTrio("Change Player Character Size", "lstwo.SizeChanger.ChangeSize", "1.0");
            lib.Input.Component.characterValidation = InputField.CharacterValidation.Decimal;
            lib.Button.OnClick = () => PlayerSize = float.Parse(lib.Input.Text);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
