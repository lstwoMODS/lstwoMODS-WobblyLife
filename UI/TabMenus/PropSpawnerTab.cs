using BepInEx.Logging;
using Newtonsoft.Json.Linq;
using ShadowLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Technie.PhysicsCreator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace NotAzzamods.UI.TabMenus
{
    public class PropSpawnerTab : BaseTab
    {
        public string SelectedProp
        {
            set
            {
                selectedObject = value;
                spawnBtn.ButtonText.text = $"Spawn Prop ({value})";
            }
        }

        private ScrollPool<PropCell> scrollPool;
        private string selectedObject;
        private PropCellHandler cellHandler;
        private ButtonRef spawnBtn;

        public PropSpawnerTab()
        {
            Name = "Prop Spawner";
        }

        public override void ConstructUI(GameObject root)
        {
            base.ConstructUI(root);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", root), 0, 5, 9999, 0);

            var scrollGroup = UIFactory.CreateHorizontalGroup(root, "group", true, false, true, true, bgColor: new(0, 0, 0, 0));
            UIFactory.SetLayoutElement(scrollGroup, 0, 620, 9999, 0);

            UIFactory.SetLayoutElement(UIFactory.CreateUIObject("spacer", scrollGroup), 5, 0, 0, 9999);

            scrollPool = UIFactory.CreateScrollPool<PropCell>(scrollGroup, "scroll", out var scrollRoot, out var scrollContent, new Color(.114f, .129f, .161f));
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
                if (player == null || selectedObject == "") return;

                var character = player.GetPlayerCharacter();
                var pos = character.GetPlayerPosition() + character.GetPlayerForward();
                Plugin.LogSource.LogMessage(selectedObject);
                NetworkPrefab.SpawnNetworkPrefab($"Game/Prefabs/Props/{selectedObject}.prefab", position: pos);

                //Plugin._StartCoroutine(InstantiateAllProp());
            };
            UIFactory.SetLayoutElement(spawnBtn.GameObject, 0, 32, 9999, 0);
        }

        private void InstantiateSingleProp(string prop)
        {
            var player = PlayerUtils.GetMyPlayer();
            if (player == null || prop == "" || prop == null) return;

            var character = player.GetPlayerCharacter();
            var pos = character.GetPlayerPosition() + character.GetPlayerForward();

            TrySpawnPrefab(prop, pos);
        }

        private IEnumerator InstantiateAllProp()
        {
            foreach (var prop in cellHandler.propList)
            {
                try
                {
                    InstantiateSingleProp(prop);
                } 
                catch (Exception ex)
                {
                    Debug.LogError($"Error while instantiating prop \"{prop}\": {ex.Message}");
                }

                yield return new WaitForSeconds(.05f);
            }

            yield break;
        }

        private bool TrySpawnPrefab(string address, Vector3 position)
        {
            bool worked = false;

            try
            {
                string newAddress = $"Game/Prefabs/Props/{address.Trim()}.prefab";
                Plugin.LogSource.LogMessage($"Trying {newAddress}");
                NetworkPrefab.SpawnNetworkPrefab(newAddress, position: position);
                worked = true;
            }
            catch (Exception a)
            {
                string newAddress = $"Assets/Content/Game/Prefabs/Props/{address.Trim()}.prefab";
                Plugin.LogSource.LogMessage($"\"Game/Prefabs/Props/{address.Trim()}.prefab\" FAILED: Trying \"{newAddress}\"");
                NetworkPrefab.SpawnNetworkPrefab(newAddress, position: position);
                worked = true;
            }

            return worked;
        }

        public override void RefreshUI()
        {
            _ = RefreshAsync();
        }

        public override void UpdateUI()
        {

        }

        public async Task RefreshAsync()
        {
            await DownloadPrefabJSON();

            cellHandler.Refresh();
        }

        public static async Task DownloadPrefabJSON()
        {
            string githubUrl = "https://raw.githubusercontent.com/lstwo/NotAzzamods/main/Data/NotAzzamods_prefabs.json";
            string fileName = AppDomain.CurrentDomain.BaseDirectory + "/NotAzzamods_prefabs.json";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(githubUrl);
                    response.EnsureSuccessStatusCode();
                    string jsonContent = await response.Content.ReadAsStringAsync();

                    string exeFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                    string filePath = Path.Combine(exeFolderPath, fileName);

                    File.WriteAllText(filePath, jsonContent);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError($"An error occurred: {ex.Message}");
            }
        }
    }

    public class PropCell : ICell
    {
        private bool enabled;

        public bool Enabled => enabled;
        public RectTransform Rect { get; set; }
        public GameObject UIRoot { get; set; }
        public float DefaultHeight => 32;

        public string prop;
        public PropSpawnerTab parentTab;

        private Text text;
        private ButtonRef button;

        public GameObject CreateContent(GameObject parent)
        {
            UIRoot = UIFactory.CreateUIObject("PropCell", parent);
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

        public void ConfigureCell(string prop)
        {
            this.prop = prop;
            text.text = prop;
        }
    }

    public class PropCellHandler : ICellPoolDataSource<PropCell>
    {
        public int ItemCount => propList.Count;
        public PropSpawnerTab parentTab;

        public List<string> propList = new();

        public PropCellHandler(PropSpawnerTab parentTab)
        {
            this.parentTab = parentTab;
        }

        public void OnCellBorrowed(PropCell cell)
        {
        }

        public void SetCell(PropCell cell, int index)
        {
            if(index < propList.Count)
            {
                cell.parentTab = parentTab;
                cell.ConfigureCell(propList[index]);
                cell.Enable();
            }
            else
            {
                cell.Disable();
            }
        }

        public void Refresh()
        {
            Plugin._StartCoroutine(RefreshCoroutine());
        }

        public IEnumerator RefreshCoroutine()
        {
            propList.Clear();

            string jsonData = "";

            try
            {
                jsonData = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "/NotAzzamods_prefabs.json");
            }
            catch(Exception e)
            {
                Plugin.LogSource.LogError("Could not load prefab locations: " + e.Message);
            }

            if (jsonData == "") yield break;

            foreach (var array in LoadPrefabData(jsonData))
            {
                propList.AddRange(array);
            }
        }

        public static string[][] LoadPrefabData(string jsonData)
        {
            var jsonObject = JObject.Parse(jsonData);
            var prefabArrayList = new List<string[]>();

            foreach (var property in jsonObject.Properties())
            {
                var prefabArray = property.Value.ToObject<string[]>();
                prefabArrayList.Add(prefabArray);
            }

            return prefabArrayList.ToArray();
        }
    }
}
