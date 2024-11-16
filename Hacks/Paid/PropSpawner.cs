using HawkNetworking;
using NotAzzamods.UI.TabMenus;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace NotAzzamods.Hacks.Paid
{
    public class PropSpawner : BaseHack
    {
        public override string Name => "Prop Spawner";

        public override string Description => "";

        public override HacksTab HacksTab => null;

        private List<GameObject> gameObjects = new List<GameObject>();
        private Dropdown dropdown;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var ldb = ui.CreateLDBTrio("Spawn Object", buttonText: "Spawn");
            dropdown = ldb.Dropdown;
            ldb.Button.OnClick = () =>
            {
                if (Player == null || dropdown.value < 0 || dropdown.value >= gameObjects.Count) return;

                var obj = gameObjects[dropdown.value];
                var pos = Player.Character.GetPlayerPosition() + Player.Character.GetPlayerForward();
                Plugin.LogSource.LogMessage(pos + "; " + Player.Character.GetPlayerPosition());
                HawkNetworkManager.DefaultInstance.InstantiateNetworkPrefab(obj, pos);
            };

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
            gameObjects.Clear();
            dropdown.ClearOptions();

            foreach (var behavior in Object.FindObjectsOfType<HawkNetworkBehaviour>())
            {
                var obj = behavior.gameObject;
                if(obj)
                {
                    gameObjects.Add(obj);
                    dropdown.options.Add(new(obj.name));
                }
            }

            dropdown.RefreshShownValue();
        }

        public override void Update()
        {
        }
    }
}
