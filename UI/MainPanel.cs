using NotAzzamods.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniverseLib.UI;
using UniverseLib.UI.Panels;

namespace NotAzzamods.UI
{
    public class MainPanel : PanelBase
    {
        public MainPanel(UIBase owner) : base(owner) { }

        public override string Name => "<b>lstwoMODS</b>";

        public override int MinWidth => 640;
        public override int MinHeight => 450;

        public override Vector2 DefaultAnchorMin => default;
        public override Vector2 DefaultAnchorMax => default;

        public BaseTab CurrentTab { get; set; }
        public BaseTab oldTab = null;

        protected override void ConstructPanelContent()
        {
            var ui = new HacksUIHelper(ContentRoot);

            var horizontalGroup = ui.CreateHorizontalGroup("horizontalGroup", true, true, true, true);

            var tabMenu = UIFactory.CreateScrollView(horizontalGroup, "tabMenu", out var tabMenuRoot, out var tabMenuScrollbar, new Color(.102f, .157f, .216f));
            UIFactory.SetLayoutElement(tabMenu, 256, 1, 0, 9999);

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
        }
    }
}
