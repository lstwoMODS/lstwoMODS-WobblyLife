using NotAzzamods.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UniverseLib.UI;
using UniverseLib.UI.Panels;

namespace NotAzzamods.UI
{
    public class MainPanel : PanelBase
    {
        public MainPanel(UIBase owner) : base(owner) { }

        public override string Name => "<b>lstwoMODS</b>";

        public override int MinWidth => 1280;
        public override int MinHeight => 720;

        public override Vector2 DefaultAnchorMin => default;
        public override Vector2 DefaultAnchorMax => default;

        public override Vector2 DefaultPosition => new(-MinWidth / 2, MinHeight / 2);

        public BaseTab CurrentTab { get; set; }
        public BaseTab oldTab = null;

        protected override void ConstructPanelContent()
        {
            var ui = new HacksUIHelper(ContentRoot);

            var horizontalGroup = ui.CreateHorizontalGroup("horizontalGroup", true, true, true, true);

            var fullTabMenu = UIFactory.CreateHorizontalGroup(horizontalGroup, "full tab menu", false, false, true, true, bgColor: new Color(.102f, .157f, .216f));
            UIFactory.SetLayoutElement(fullTabMenu, 256, 1, 0, 9999);

            new HacksUIHelper(fullTabMenu).AddSpacer(0, 3);

            var tabMenu = UIFactory.CreateScrollView(fullTabMenu, "tabMenu", out var tabMenuRoot, out var tabMenuScrollbar, new Color(.102f, .157f, .216f));
            UIFactory.SetLayoutElement(tabMenu, 253, 1, 0, 9999);

            var tabUi = new HacksUIHelper(tabMenuRoot);

            var hacksMenu = UIFactory.CreateScrollView(horizontalGroup, "hacksMenu", out var hacksMenuRoot, out var hacksMenuScrollbar, new Color(.095f, .108f, .133f));
            UIFactory.SetLayoutElement(hacksMenu, 512, 1, 9999, 9999);

            foreach (BaseTab tab in Plugin.TabMenus)
            {
                tabUi.AddSpacer(3);

                tab.ConstructTabButton(tabUi);

                var newRoot = UIFactory.CreateVerticalGroup(hacksMenuRoot, tab.Name, false, false, true, true, bgColor: new Color(.095f, .108f, .133f));

                tab.ConstructUI(newRoot);
            }
        }

        public void Refresh()
        {
            foreach(BaseTab tab in Plugin.TabMenus)
            {
                tab.RefreshUI();
            }
        }

        protected override void OnClosePanelClicked()
        {
            Plugin.UiBase.Enabled = false;

            var inputManager = PlayerUtils.GetMyPlayer().GetPlayerControllerInputManager();
            
            inputManager.EnableGameplayCameraInput(Plugin.Instance);
            inputManager.EnableGameplayInput(Plugin.Instance);
            inputManager.EnableInteratorInput(Plugin.Instance);
            inputManager.EnablePlayerTransformInput(Plugin.Instance);
            inputManager.EnableUIInput(Plugin.Instance);
        }
    }
}
