using NotAzzamods.Keybinds;
using ShadowLib;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace NotAzzamods
{
    public class HacksUIHelper : UIHelper
    {
        public static Color ButtonColor { get; private set; } = new Color(.063f, .094f, .129f);
        public static Color BGColor1 { get; private set; } = new Color(.129f, .145f, .176f);
        public static Color BGColor2 { get; private set; } = new Color(.114f, .129f, .161f);
        public static Sprite RoundedRect { get; private set; }

        public GameObject root { get; private set; }

        public HacksUIHelper(GameObject root) : base(root)
        {
            RoundedRect ??= Plugin.AssetBundle.LoadAsset<Sprite>("RoundedRect");

            this.root = root;
        }

        new public InputFieldRef CreateInputField(string placeholder, string name = "")
        {
            return CreateInputField(placeholder, name, 256, 32);
        }

        public InputFieldRef CreateInputField(string placeholder, string name = "", int inputWidth = 256, int height = 32)
        {
            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var inputFieldRef = UIFactory.CreateInputField(group, name, placeholder);
            inputFieldRef.Component.image.sprite = RoundedRect;

            UIFactory.SetLayoutElement(inputFieldRef.GameObject, inputWidth, height, 0, 0);

            var keybinder = inputFieldRef.GameObject.AddComponent<InputFieldKeybinder>();
            keybinder.input = inputFieldRef;

            return inputFieldRef;
        }

        public GameObject AddSpacer(int height = 0, int width = 0)
        {
            GameObject gameObject = UIFactory.CreateUIObject("Spacer", root);
            UIFactory.SetLayoutElement(gameObject, width, height, 0, 0);
            return gameObject;
        }

        new public Text CreateLabel(string text, string name = "", TextAnchor alignment = TextAnchor.MiddleLeft, Color? color = null, bool richTextSupport = true, int fontSize = 14)
        {
            if (name == "")
            {
                name = text;
            }

            if (!color.HasValue)
            {
                color = Color.white;
            }

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group, 256 * 3, 32, 9999);

            var spacer = UIFactory.CreateUIObject("spacer", group);
            UIFactory.SetLayoutElement(spacer, 4);

            Text text2 = UIFactory.CreateLabel(group, name, text, alignment, color.Value, richTextSupport, fontSize);
            UIFactory.SetLayoutElement(text2.gameObject, 256 * 3, 32, 9999);
            return text2;
        }

        /// <summary>
        /// Don't
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        new public Slider CreateSlider(string name)
        {
            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var gameObject = UIFactory.CreateSlider(group, name, out var slider);

            foreach(var image in slider.GetComponentsInChildren<Image>())
            {
                image.sprite = RoundedRect;
                image.type = Image.Type.Sliced;
            }

            UIFactory.SetLayoutElement(gameObject, 256 * 3, 32, 0, 0);

            var keybinder = gameObject.AddComponent<SliderKeybinder>();
            keybinder.slider = gameObject.GetComponent<Slider>();

            return slider;
        }

        new public Toggle CreateToggle(string name = "", string label = "", UnityAction<bool> onValueChanged = null, bool defaultState = false, Color bgColor = default, int checkWidth = 20, int checkHeight = 20)
        {
            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var spacer = UIFactory.CreateUIObject("spacer", group);
            UIFactory.SetLayoutElement(spacer, 4);

            GameObject gameObject = UIFactory.CreateToggle(group, name, out var toggle, out var text, bgColor, checkWidth, checkHeight);
            text.text = label;
            toggle.isOn = defaultState;
            toggle.onValueChanged.AddListener(onValueChanged);
            ((Image)toggle.targetGraphic).sprite = RoundedRect;
            ((Image)toggle.graphic).sprite = RoundedRect;
            UIFactory.SetLayoutElement(gameObject, 768, 32, 0, 0);
            gameObject.transform.Find("Background").Find("Checkmark").GetComponent<Image>().color = new Color(.565f, .792f, .976f);
            return toggle;
        }

        public ButtonRef CreateButton(string text, Action onClick, string name = "", Color? color = null, int buttonWidth = 256, int height = 32)
        {
            if(color == null)
            {
                color = ButtonColor;
            }

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UnityEngine.Object.Destroy(group.GetComponent<Image>());
            UIFactory.SetLayoutElement(group);

            var button = UIFactory.CreateButton(group, text + " Button", text, color);
            button.OnClick = onClick;
            button.Component.image = button.Transform.GetComponent<Image>();
            button.Component.image.sprite = RoundedRect;

            var buttonKeybinder = button.GameObject.AddComponent<ButtonKeybinder>();
            buttonKeybinder.button = button;

            UIFactory.SetLayoutElement(button.GameObject, buttonWidth, height, 0, 0);

            return button;
        }

        /// <summary>
        /// Creates a label, input and button next to each other horizontally
        /// </summary>
        /// <returns>
        /// A reference to the three components
        /// </returns>
        public LIBTrio CreateLIBTrio(string label, string name = null, string inputPlaceHolder = "", Action onClick = null, string buttonText = "Apply", int labelWidth = 256, int inputWidth = 256, 
            int spacerWidth = 32, int buttonWidth = 256, int height = 32)
        {
            if (name == null) name = label;

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var _label = UIFactory.CreateLabel(group, label + " Label", " " + label);

            UIFactory.SetLayoutElement(_label.gameObject, labelWidth, height);

            var spacer1 = UIFactory.CreateUIObject("spacer1", group);
            UIFactory.SetLayoutElement(spacer1, spacerWidth);

            var input = UIFactory.CreateInputField(group, inputPlaceHolder + " Input", " " + inputPlaceHolder);
            input.Component.image.sprite = RoundedRect;

            UIFactory.SetLayoutElement(input.GameObject, inputWidth, height, 0, 0);

            var spacer2 = UIFactory.CreateUIObject("spacer2", group);
            UIFactory.SetLayoutElement(spacer2, spacerWidth);

            var button = UIFactory.CreateButton(group, buttonText + " Button", buttonText, ButtonColor);
            button.OnClick = onClick;

            button.Component.image = button.Transform.GetComponent<Image>();
            button.Component.image.sprite = RoundedRect;

            UIFactory.SetLayoutElement(button.GameObject, buttonWidth, height, 0, 0);

            var keybinder = _label.gameObject.AddComponent<LIBKeybinder>();
            keybinder.input = input;
            keybinder.button = button;

            return new(_label, input, button);
        }

        public class LIBTrio
        {
            public Text Label { get; private set; }
            public InputFieldRef Input { get; private set; }
            public ButtonRef Button { get; private set; }

            public LIBTrio(Text label, InputFieldRef input, ButtonRef button)
            {
                Label = label;
                Input = input;
                Button = button;
            }
        }

        /// <summary>
        /// Creates a label, dropdown and button next to each other horizontally
        /// </summary>
        /// <returns>
        /// A reference to the three components
        /// </returns>
        public LDBTrio CreateLDBTrio(string label, string name = null, string defaultItemText = "", int itemFontSize = 16, Action<int> onValueChanged = null, Action onClick = null, string buttonText = "Apply", 
            int labelWidth = 256, int dropdownWidth = 256, int spacerWidth = 32, int buttonWidth = 256, int height = 32)
        {
            if (name == null) name = label;

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var _label = UIFactory.CreateLabel(group, label + " Label", " " + label);
            UIFactory.SetLayoutElement(_label.gameObject, labelWidth, height);

            var spacer1 = UIFactory.CreateUIObject("spacer1", group);
            UIFactory.SetLayoutElement(spacer1, spacerWidth);

            var dropdownObj = UIFactory.CreateDropdown(group, "Dropdown", out var dropdown, defaultItemText, itemFontSize, onValueChanged);
            dropdown.image.sprite = RoundedRect;
            UIFactory.SetLayoutElement(dropdownObj, dropdownWidth, height, 0, 0);

            var spacer2 = UIFactory.CreateUIObject("spacer2", group);
            UIFactory.SetLayoutElement(spacer2, spacerWidth);

            var button = UIFactory.CreateButton(group, buttonText + " Button", buttonText, ButtonColor);
            button.OnClick = onClick;
            button.Component.image = button.Transform.GetComponent<Image>();
            button.Component.image.sprite = RoundedRect;

            UIFactory.SetLayoutElement(button.GameObject, buttonWidth, height, 0, 0);

            return new(_label, dropdown, button);
        }

        public class LDBTrio
        {
            public Text Label { get; private set; }
            public Dropdown Dropdown { get; private set; }
            public ButtonRef Button { get; private set; }

            public LDBTrio(Text label, Dropdown dropdown, ButtonRef button)
            {
                Label = label;
                Dropdown = dropdown;
                Button = button;
            }
        }

        /// <summary>
        /// Creates a label and two buttons next to each other horizontally
        /// </summary>
        /// <returns>
        /// A reference to the three components
        /// </returns>
        public LBBTrio CreateLBBTrio(string label, string name = null, Action onClick1 = null, string buttonText1 = "Apply", Action onClick2 = null, string buttonText2 = "Apply", int labelWidth = 256,
            int spacerWidth = 32, int button1Width = 256, int button2Width = 256, int height = 32)
        {
            if (name == null) name = label;

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var _label = UIFactory.CreateLabel(group, label + " Label", " " + label);
            UIFactory.SetLayoutElement(_label.gameObject, labelWidth, height);

            var spacer1 = UIFactory.CreateUIObject("spacer", group);
            UIFactory.SetLayoutElement(spacer1, spacerWidth);

            var button1 = UIFactory.CreateButton(group, buttonText1 + " Button", buttonText1, ButtonColor);
            button1.OnClick = onClick1;
            button1.Component.image = button1.Transform.GetComponent<Image>();
            button1.Component.image.sprite = RoundedRect;

            var buttonKeybinder = button1.GameObject.AddComponent<ButtonKeybinder>();
            buttonKeybinder.button = button1;

            UIFactory.SetLayoutElement(button1.GameObject, button1Width, height, 0, 0);

            var spacer2 = UIFactory.CreateUIObject("spacer", group);
            UIFactory.SetLayoutElement(spacer2, spacerWidth);

            var button2 = UIFactory.CreateButton(group, buttonText2 + " Button", buttonText2, ButtonColor);
            button2.OnClick = onClick2;
            button2.Component.image = button2.Transform.GetComponent<Image>();
            button2.Component.image.sprite = RoundedRect;

            var buttonKeybinder2 = button2.GameObject.AddComponent<ButtonKeybinder>();
            buttonKeybinder2.button = button2;

            UIFactory.SetLayoutElement(button2.GameObject, button2Width, height, 0, 0);

            return new(_label, button1, button2);
        }

        public class LBBTrio
        {
            public Text Label { get; private set; }
            public ButtonRef Button { get; private set; }
            public ButtonRef Button2 { get; private set; }

            public LBBTrio(Text label, ButtonRef button, ButtonRef button2)
            {
                Label = label;
                Button = button;
                Button2 = button2;
            }
        }

        /// <summary>
        /// Creates a label and a button next to eachother horizontally
        /// </summary>
        /// <returns>
        /// A reference to the two components
        /// </returns>
        public LBDuo CreateLBDuo(string label, string name = null, Action onClick = null, string buttonText = "Apply", int labelWidth = 256, int spacerWidth = 32, int buttonWidth = 256, int height = 32)
        {
            if (name == null) name = label;

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var _label = UIFactory.CreateLabel(group, label + " Label", " " + label);
            UIFactory.SetLayoutElement(_label.gameObject, labelWidth, height);

            var spacer = UIFactory.CreateUIObject("spacer", group);
            UIFactory.SetLayoutElement(spacer, spacerWidth);

            var button = UIFactory.CreateButton(group, buttonText + " Button", buttonText, ButtonColor);
            button.OnClick = onClick;
            button.Component.image = button.Transform.GetComponent<Image>();
            button.Component.image.sprite = RoundedRect;

            var buttonKeybinder = button.GameObject.AddComponent<ButtonKeybinder>();
            buttonKeybinder.button = button;

            UIFactory.SetLayoutElement(button.GameObject, buttonWidth, height, 0, 0);

            return new(_label, button);
        }

        public class LBDuo
        {
            public Text Label { get; private set; }
            public ButtonRef Button { get; private set; }

            public LBDuo(Text label, ButtonRef button)
            {
                Label = label;
                Button = button;
            }
        }

        /// <summary>
        /// Creates a label and input next to each other horizontally
        /// </summary>
        /// <returns>
        /// A reference to the two components
        /// </returns>
        public LIDuo CreateLIDuo(string label, string name = null, string inputName = "Input", string inputPlaceholder = "", InputField.CharacterValidation characterValidation = InputField.CharacterValidation.None, 
            int labelWidth = 256, int spacerWidth = 32, int inputWidth = 256, int height = 32)
        {
            name ??= label;

            var group = UIFactory.CreateHorizontalGroup(root, name, true, true, true, true);
            UIFactory.SetLayoutElement(group);

            var _label = UIFactory.CreateLabel(group, label + " Label", " " + label);
            UIFactory.SetLayoutElement(_label.gameObject, labelWidth, height);

            var spacer = UIFactory.CreateUIObject("spacer", group);
            UIFactory.SetLayoutElement(spacer, spacerWidth);

            var input = UIFactory.CreateInputField(group, inputName, " " + inputPlaceholder);
            input.Component.image.sprite = RoundedRect;

            UIFactory.SetLayoutElement(input.GameObject, inputWidth, height, 0, 0);

            var keybinder = input.GameObject.AddComponent<InputFieldKeybinder>();
            keybinder.input = input;

            return new(_label, input);
        }

        public class LIDuo
        {
            public Text Label { get; private set; }
            public InputFieldRef Input { get; private set; }

            public LIDuo(Text label, InputFieldRef input)
            {
                Label = label;
                Input = input;
            }
        }
    }
}
