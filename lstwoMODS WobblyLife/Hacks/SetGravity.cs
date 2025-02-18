using UnityEngine;
using UniverseLib.UI;
using UnityEngine.UI;
using UniverseLib.UI.Models;
using lstwoMODS_WobblyLife.UI.TabMenus;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.Keybinds;
using static lstwoMODS_Core.Keybinds.Keybinder;

namespace lstwoMODS_WobblyLife.Hacks
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

            var lb = ui.CreateLBDuo("Set Gravity (x, y, z)", "lstwo.SetGravity.SetGravity");

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

            var keybinder = group.AddComponent<GravityKeybinder>();
            keybinder.inputX = inputX;
            keybinder.inputY = inputY;
            keybinder.inputZ = inputZ;

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

        public class GravityKeybinder : Keybinder
        {
            public InputFieldRef inputX;
            public InputFieldRef inputY;
            public InputFieldRef inputZ;

            public override void OnRightClicked()
            {
                lstwoMODS_Core.Plugin.KeybindPanel._SetActive(true);
                lstwoMODS_Core.Plugin.KeybindPanel.Keybinder = this;
            }

            public override Keybind CreateKeybind()
            {
                var keybind = new GravityKeybind(this);
                keybinds.Add(keybind);
                return keybind;
            }

            public class GravityKeybind : Keybind
            {
                public GravityKeybind(Keybinder keybinder) : base(keybinder)
                {
                }

                public override void OnPressed()
                {
                    var inputKeybinder = keybinder as GravityKeybinder;

                    if (inputKeybinder != null)
                    {
                        inputKeybinder.inputX.Text = inputXString;
                        inputKeybinder.inputY.Text = inputYString;
                        inputKeybinder.inputZ.Text = inputZString;
                    }
                }

                private Text text;

                private string inputXString = "";
                private string inputYString = "";
                private string inputZString = "";

                private InputFieldRef inputX;
                private InputFieldRef inputY;
                private InputFieldRef inputZ;

                public override void CreateScrollItem(GameObject root)
                {
                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 6, 9999, 0);

                    var dropdownGroup = UIFactory.CreateUIObject("dropdownGroup", root);
                    UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(dropdownGroup, false, false, true, true);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", dropdownGroup), 6, 0, 0, 9999);

                    var button = UIFactory.CreateButton(dropdownGroup, "button", "Set Keybind", HacksUIHelper.ButtonColor);
                    button.OnClick = StartDetectKeybind;
                    button.GameObject.GetComponent<Image>().sprite = HacksUIHelper.RoundedRect;
                    UIFactory.SetLayoutElement(button.GameObject, 128, 48, 0, 0);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", dropdownGroup), 6, 0, 0, 9999);

                    text = UIFactory.CreateLabel(dropdownGroup, "text", "No Keys Selected");
                    UIFactory.SetLayoutElement(text.gameObject, 9999, 48, 9999, 0);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 6, 9999, 0);

                    var inputGroup = UIFactory.CreateHorizontalGroup(root, "inputGroup", false, false, true, true);
                    UIFactory.SetLayoutElement(inputGroup, 0, 0, 9999, 9999);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", inputGroup), 6, 0, 0, 9999);

                    inputX = UIFactory.CreateInputField(inputGroup, "x", " X (0)");
                    inputX.Component.characterValidation = InputField.CharacterValidation.Decimal;
                    inputX.Component.image.sprite = HacksUIHelper.RoundedRect;
                    inputX.OnValueChanged += (value) => inputXString = value;
                    UIFactory.SetLayoutElement(inputX.GameObject, 163, 32);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", inputGroup), 32, 32);

                    inputY = UIFactory.CreateInputField(inputGroup, "y", " Y (19.62)");
                    inputY.Component.characterValidation = InputField.CharacterValidation.Decimal;
                    inputY.Component.image.sprite = HacksUIHelper.RoundedRect;
                    inputY.OnValueChanged += (value) => inputYString = value;
                    UIFactory.SetLayoutElement(inputY.GameObject, 163, 32);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", inputGroup), 32, 32);

                    inputZ = UIFactory.CreateInputField(inputGroup, "z", " Z (0)");
                    inputZ.Component.characterValidation = InputField.CharacterValidation.Decimal;
                    inputZ.Component.image.sprite = HacksUIHelper.RoundedRect;
                    inputZ.OnValueChanged += (value) => inputZString = value;
                    UIFactory.SetLayoutElement(inputZ.GameObject, 163, 32);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", inputGroup), 6, 0, 0, 9999);

                    UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 6, 9999, 0);
                }

                public override void RefreshScrollItem()
                {
                    if (!primaryKey.HasValue)
                    {
                        return;
                    }

                    var _text = "";
                    secondaryKeys.ForEach(key => _text += key.ToString() + " + ");
                    _text += primaryKey.ToString();

                    text.text = _text;
                }

                public override void StartDetectKeybind()
                {
                    base.StartDetectKeybind();
                    text.text = "Press Any Key...";
                }

                public override void StopDetectKeybind()
                {
                    base.StopDetectKeybind();

                    if (primaryKey == null)
                    {
                        text.text = "No Keys Selected";
                        return;
                    }

                    var _text = "";
                    secondaryKeys.ForEach(key => _text += key.ToString() + " + ");
                    _text += primaryKey.ToString();

                    text.text = _text;
                }

                public override string[] SerializeData()
                {
                    return new string[] { inputXString, inputYString, inputZString };
                }

                public override void DeserializeData(string[] data)
                {
                    inputXString = data[0];
                    inputYString = data[1];
                    inputZString = data[2];
                }
            }
        }
    }
}
