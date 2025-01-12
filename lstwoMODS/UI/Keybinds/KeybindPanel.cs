using NotAzzamods.Keybinds;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Panels;

namespace NotAzzamods.UI.Keybinds
{
    public class KeybindPanel : PanelBase
    {
        public KeybindPanel(UIBase owner) : base(owner)
        {
        }

        public override string Name => "Edit Keybinds";
        public override int MinWidth => 612;
        public override int MinHeight => 418;

        public override Vector2 DefaultAnchorMin => new(.5f, .5f);
        public override Vector2 DefaultAnchorMax => new(.5f, .5f);

        public override Vector2 DefaultPosition => new(-MinWidth / 2, MinHeight / 2);

        public override bool CanDragAndResize => false;

        public Keybinder Keybinder
        {
            set
            {
                currentKeybinds = value.keybinds;
                currentKeybinder = value;

                for (int i = 0; i < keybindsContent.transform.childCount; i++)
                {
                    UnityEngine.Object.Destroy(keybindsContent.transform.GetChild(i).gameObject);
                }

                foreach (var keybind in currentKeybinds)
                {
                    CreateKeybindItem(keybind);
                }
            }
        }

        public KeybindItem SelectedKeybind
        {
            set
            {
                if(selectedKeybind != null)
                {
                    selectedKeybind.Selected = false;
                }

                selectedKeybind = value;
                selectedKeybind.Selected = true;
            }
        }

        private KeybindItem selectedKeybind;
        private Keybinder currentKeybinder;
        private List<Keybinder.Keybind> currentKeybinds;
        private GameObject keybindsContent;

        protected override void ConstructPanelContent()
        {
            var height = ContentRoot.GetComponent<RectTransform>().sizeDelta.y;

            // Root
            var root = UIFactory.CreateVerticalGroup(ContentRoot, "root", false, false, true, true, 0, default, new(.102f, .157f, .216f));
            UIFactory.SetLayoutElement(root, 0, 0, 9999, 9999);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 6, 9999, 0);

            // Keybinds Scroll View
            var keybindsRoot = UIFactory.CreateHorizontalGroup(root, "keybindsRoot", false, false, true, true, 0, default, new(.102f, .157f, .216f));
            UIFactory.SetLayoutElement(keybindsRoot, 0, 0, 9999, 9999);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", keybindsRoot), 6, 0, 0, 9999);

            var keybinds = UIFactory.CreateScrollView(keybindsRoot, "keybinds", out keybindsContent, out var scrollBar, HacksUIHelper.BGColor2);
            UIFactory.SetLayoutElement(keybindsContent, 0, 0, 9999, 9999);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", keybindsRoot), 6, 0, 0, 9999);

            // Buttons
            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 6, 9999, 0);

            var buttonGroup = UIFactory.CreateHorizontalGroup(root, "buttonGroup", false, false, true, true, 0, default, new(.102f, .157f, .216f));
            UIFactory.SetLayoutElement(buttonGroup, 0, 48, 9999, 0);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", buttonGroup), 6, 0, 0, 9999);

            var addButton = UIFactory.CreateButton(buttonGroup, "addButton", "<b>+</b>", HacksUIHelper.ButtonColor);
            addButton.OnClick = AddKeybind;
            UIFactory.SetLayoutElement(addButton.GameObject, 294, 48, 9999, 0);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", buttonGroup), 6, 0, 0, 9999);

            var removeButton = UIFactory.CreateButton(buttonGroup, "removeButton", "<b>-</b>", HacksUIHelper.ButtonColor);
            removeButton.OnClick = RemoveKeybind;
            UIFactory.SetLayoutElement(removeButton.GameObject, 294, 48, 9999, 0);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", buttonGroup), 6, 0, 0, 9999);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 6, 9999, 0);
        }

        public override void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape) && Enabled)
            {
                _SetActive(false);
            }
        }

        public void _SetActive(bool active)
        {
            SetDefaultSizeAndPosition();
            SetActive(active);
            Plugin.MainPanel.SetActive(!active);
        }

        protected override void OnClosePanelClicked()
        {
            _SetActive(false);
        }

        public void CreateKeybindItem(Keybinder.Keybind keybind)
        {
            var root = UIFactory.CreateVerticalGroup(keybindsContent, "Keybind", true, false, false, true, 0, default, HacksUIHelper.BGColor1);
            root.AddComponent<KeybindItem>().keybind = keybind;
            keybind.CreateScrollItem(root);
            keybind.RefreshScrollItem();

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", keybindsContent), 0, 1, 9999, 0);
        }

        public void RefreshKeybinds()
        {
            foreach(var keybind in currentKeybinds)
            {
                keybind.RefreshScrollItem();
            }
        }

        public void AddKeybind()
        {
            if(currentKeybinder != null)
            {
                var keybind = currentKeybinder.CreateKeybind();
                CreateKeybindItem(keybind);
                keybind.RefreshScrollItem();
            }
        }

        public void RemoveKeybind()
        {
            if(selectedKeybind)
            {
                var keybind = selectedKeybind.keybind;
                selectedKeybind.keybind.keybinder.keybinds.Remove(keybind);
                KeybindManager.RemoveKeybind(keybind);
                UnityEngine.Object.Destroy(selectedKeybind.gameObject);
                selectedKeybind = null;
            }
        }
    }
}
