using Newtonsoft.Json.Linq;
using NotAzzamods.CustomItems;
using ShadowLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Technie.PhysicsCreator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;
using CustomItems;
using HawkNetworking;

namespace NotAzzamods.UI.TabMenus
{
    public class OldCustomItemsTab : BaseTab
    {
        public CustomItem SelectedProp
        {
            set
            {
                selectedObject = value;
                spawnBtn.ButtonText.text = $"Spawn Item ({value.itemName})";
            }
        }

        private ScrollPool<CustomItemCell> scrollPool;
        private CustomItem selectedObject;
        private CustomItemCellHandler cellHandler;
        private ButtonRef spawnBtn;
        private ButtonRef refreshBtn;

        public OldCustomItemsTab()
        {
            Name = "Custom Items";
        }

        public override void ConstructUI(GameObject root)
        {
            base.ConstructUI(root);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 9999, 0);

            var scrollGroup = UIFactory.CreateHorizontalGroup(root, "group", true, false, true, true, bgColor: new(0, 0, 0, 0));
            UIFactory.SetLayoutElement(scrollGroup, 0, 620, 9999, 0);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", scrollGroup), 5, 0, 0, 9999);

            scrollPool = UIFactory.CreateScrollPool<CustomItemCell>(scrollGroup, "scroll", out var scrollRoot, out var scrollContent, new Color(.114f, .129f, .161f));
            UIFactory.SetLayoutElement(scrollRoot, 0, 0, 9999, 9999);
            UIFactory.SetLayoutElement(scrollContent, 0, 0, 9999, 9999);

            cellHandler = new(this);
            scrollPool.Initialize(cellHandler);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", scrollGroup), 5, 0, 0, 9999);
            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 9999, 0);

            spawnBtn = UIFactory.CreateButton(root, "SpawnBtn", "Spawn Prop");
            spawnBtn.OnClick = () =>
            {
                var player = PlayerUtils.GetMyPlayer();
                if (player == null || selectedObject == null) return;

                var character = player.GetPlayerCharacter();
                var pos = character.GetPlayerPosition() + character.GetPlayerForward();
                Plugin.LogSource.LogMessage(selectedObject);

                NetworkPrefab.SpawnNetworkPrefab(selectedObject.gameObject, pos);
            };
            UIFactory.SetLayoutElement(spawnBtn.GameObject, 0, 32, 9999, 0);
        }

        public override void RefreshUI()
        {
            cellHandler.Refresh();
        }

        public override void UpdateUI()
        {

        }
    }

    public class CustomItemCell : ICell
    {
        private bool enabled;

        public bool Enabled => enabled;
        public RectTransform Rect { get; set; }
        public GameObject UIRoot { get; set; }
        public float DefaultHeight => 32;

        public CustomItem prop;
        public OldCustomItemsTab parentTab;

        private Text text;
        private ButtonRef button;

        public GameObject CreateContent(GameObject parent)
        {
            UIRoot = UIFactory.CreateUIObject("ItemCell", parent);
            UIFactory.SetLayoutElement(UIRoot, 0, 32, 9999, 0);
            Rect = UIRoot.GetComponent<RectTransform>();

            button = UIFactory.CreateButton(UIRoot, "button", "");
            button.OnClick = OnCellButtonClicked;

            RectTransform buttonRect = button.GameObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 0);
            buttonRect.anchorMax = new Vector2(1, 1);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            text = button.ButtonText;

            return UIRoot;
        }

        public void OnCellButtonClicked()
        {
            parentTab.SelectedProp = prop;
        }

        public void Disable()
        {
            enabled = false;
            UIRoot.SetActive(false);
        }

        public void Enable()
        {
            enabled = true;
            UIRoot.SetActive(true);
        }

        public void ConfigureCell(CustomItem prop)
        {
            this.prop = prop;
            text.text = prop.itemName;
        }
    }

    public class CustomItemCellHandler : ICellPoolDataSource<CustomItemCell>
    {
        public int ItemCount => itemList.Count;
        public OldCustomItemsTab parentTab;

        public List<CustomItem> itemList = new();

        public CustomItemCellHandler(OldCustomItemsTab parentTab)
        {
            this.parentTab = parentTab;
        }

        public void OnCellBorrowed(CustomItemCell cell)
        {
        }

        public void SetCell(CustomItemCell cell, int index)
        {
            if(index < itemList.Count)
            {
                cell.parentTab = parentTab;
                cell.ConfigureCell(itemList[index]);
                cell.Enable();
            }
            else
            {
                cell.Disable();
            }
        }

        public void Refresh()
        {
            itemList.Clear();
            foreach(var pack in Plugin.CustomItemPacks)
            {
                Debug.Log(pack);
                itemList.AddRange(pack.items);
            }
            Debug.Log(itemList);
        }
    }
}
