using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;

namespace NotAzzamods.UI.TabMenus
{
    public class SettingsTab : BaseTab
    {
        public float UISize
        {
            get
            {
                return configUISize.Value;
            }
            set
            {
                configUISize.Value = value;

                var size = new Vector2(value, value);
                var transform = Plugin.MainPanel.ContentRoot.transform;
                var rect = transform.GetComponent<RectTransform>();

                Vector2 originalSize = rect.rect.size;
                var sizeDifference = value - transform.localScale.x;
                var newRectSize = new Vector2(originalSize.x / sizeDifference, originalSize.y / sizeDifference);

                transform.localScale = size;

                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newRectSize.x);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newRectSize.y);
            }
        }

        private ConfigEntry<float> configUISize;
        private Slider sizeSlider;

        public SettingsTab()
        {
            Name = "Settings";
        }

        public override void ConstructUI(GameObject root)
        {
            base.ConstructUI(root);

            configUISize = Plugin.ConfigFile.Bind("Settings", "UISize", 1f);

            var horizontalLayout = UIFactory.CreateUIObject("layout", root);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(horizontalLayout, false, false, true, true, 6);
            UIFactory.SetLayoutElement(horizontalLayout);

            new HacksUIHelper(horizontalLayout).AddSpacer(0, 6);

            // layout
            var layout = UIFactory.CreateUIObject("layout", horizontalLayout);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(layout, false, false, true, true, 6);
            UIFactory.SetLayoutElement(layout);

            new HacksUIHelper(horizontalLayout).AddSpacer(6, 0);

            // size

            // label
            var sizeLabel = UIFactory.CreateLabel(layout, "sizeLabel", "<b>UI Size</b>", fontSize: 18);
            UIFactory.SetLayoutElement(sizeLabel.gameObject, 256, 20, 0, 0);

            // slider
            var sizeSliderObj = UIFactory.CreateSlider(layout, "sizeSlider", out sizeSlider);
            UIFactory.SetLayoutElement(sizeSliderObj, 512, 32, 0, 0);
            sizeSlider.maxValue = 2.5f;
            sizeSlider.minValue = 0.1f;
            sizeSlider.value = configUISize.Value;
            sizeSlider.onValueChanged.AddListener((value) =>
            {
                UISize = (float)Math.Round(value, 2);
            });

            // reset
            var resetButton = UIFactory.CreateButton(layout, "resetButton", "Reset Size");
            UIFactory.SetLayoutElement(resetButton.GameObject, 256, 32, 0, 0);
            resetButton.OnClick = () =>
            {
                sizeSlider.value = 1;
            };
        }

        public override void RefreshUI()
        {
            sizeSlider.value = UISize;
        }

        public override void UpdateUI()
        {
        }
    }
}
