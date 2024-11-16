using BepInEx.Configuration;
using CustomItems;
using NotAzzamods.CustomItems;
using ShadowLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace NotAzzamods.UI.TabMenus
{
    public class CustomItemsTab : BaseTab
    {
        private GridLayoutGroup gridView;
        private GameObject gridViewObj;
        private ButtonRef spawnBtn;
        private ButtonRef refreshBtn;

        private List<CustomItem> items = new();
        private List<GameObject> itemCells = new();

        public CustomItemsTab()
        {
            Name = "Custom Items";
        }

        public override void ConstructUI(GameObject root)
        {
            base.ConstructUI(root);

            gridViewObj = UIFactory.CreateGridGroup(root, "gridView", new(268, 436), new(6, 6), new Color(.095f, .108f, .133f));
            gridView = gridViewObj.GetComponent<GridLayoutGroup>();
        }

        public override void RefreshUI()
        {
            items.Clear();

            foreach(var cell in itemCells)
            {
                UnityEngine.Object.Destroy(cell);
            }

            foreach (var pack in Plugin.CustomItemPacks)
            {
                items.AddRange(pack.items);

                foreach(var item in pack.items)
                {
                    var cell = new CustomItemCell();
                    itemCells.Add(cell.ConstructUI(gridViewObj));

                    cell.Item = item;
                    cell.ItemPack = pack;
                }
            }
        }

        public static IEnumerator InitCustomItems()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + "/CustomItems/";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                yield break;
            }

            var itemDirectories = Directory.GetDirectories(path);

            foreach (var itemDirectory in itemDirectories)
            {
                Debug.Log(itemDirectory);
                CustomItemPacks.Add(new(itemDirectory));
            }
        }

        public override void UpdateUI()
        {
        }

        public class CustomItemCell
        {
            public CustomItemPack ItemPack
            {
                set
                {
                    itemPack = value;
                    itemPackLabel.text = $"<b>{value.packName}</b>";
                }
            }

            public CustomItem Item
            {
                set
                {
                    item = value;
                    nameLabel.text = $"<b>{value.itemName}</b>";
                    descriptionText.text = value.itemDescription;
                    image.sprite = value.itemSprite;
                }
            }

            private CustomItem item;
            private CustomItemPack itemPack;
            private Text nameLabel;
            private Text itemPackLabel;
            private Text descriptionText;
            private Image image;
            private ButtonRef spawnBtn;

            public GameObject ConstructUI(GameObject root)
            {
                var layoutRoot = UIFactory.CreateVerticalGroup(root, "layout", false, false, true, true, 6, new(6, 6, 6, 6), HacksUIHelper.BGColor2, TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(layoutRoot, 0, 0, 9999, 9999);

                itemPackLabel = UIFactory.CreateLabel(layoutRoot, "itemPackLabel", "Item Pack", TextAnchor.UpperCenter, fontSize: 20);
                UIFactory.SetLayoutElement(itemPackLabel.gameObject, 0, 22, 9999, 0);

                image = UIFactory.CreateUIObject("image", layoutRoot).AddComponent<Image>();
                UIFactory.SetLayoutElement(image.gameObject, 256, 256, 0, 0);

                nameLabel = UIFactory.CreateLabel(layoutRoot, "nameLabel", "Item Name", TextAnchor.UpperCenter, fontSize: 20);
                UIFactory.SetLayoutElement(nameLabel.gameObject, 0, 22, 9999, 0);

                descriptionText = UIFactory.CreateLabel(layoutRoot, "descriptionText", "Item Description", TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(descriptionText.gameObject, 0, 64, 9999, 0);

                spawnBtn = UIFactory.CreateButton(layoutRoot, "spawnBtn", "<b>Spawn Item</b>", HacksUIHelper.ButtonColor);
                spawnBtn.OnClick = () =>
                {
                    var player = PlayerUtils.GetMyPlayer();

                    if (player != null && item != null)
                    {
                        var character = player.GetPlayerCharacter();
                        var pos = character.GetPlayerPosition() + character.GetPlayerForward();

                        NetworkPrefab.SpawnNetworkPrefab(item.gameObject, position: pos);
                    }
                };
                spawnBtn.ButtonText.fontSize = 20;
                UIFactory.SetLayoutElement(spawnBtn.GameObject, 0, 32, 9999, 0);

                return layoutRoot;
            }
        }
    }
}
