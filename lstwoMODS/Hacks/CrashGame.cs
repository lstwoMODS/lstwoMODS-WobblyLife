using MonoMod.Utils;
using NotAzzamods.UI.TabMenus;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks
{
    public class CrashGame : BaseHack
    {
        public override string Name => "Spawn Random Props";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.PlayerHacksTab;

        private int iterations = 100;
        private InputFieldRef iterationsInput;

        private float delayBetweenProps;
        private uint amountOfPropsForDelay;

        private Coroutine coroutine;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            var lib = ui.CreateLIBTrio("Spawn Prop Shop Props", "", "Amount of Props", Crash, "Spawn");
            iterationsInput = lib.Input;

            ui.AddSpacer(6);

            var waitLib = ui.CreateLIBTrio("Delay between props in seconds", "", "Seconds of delay");
            waitLib.Button.OnClick = () => delayBetweenProps = float.Parse(waitLib.Input.Text);

            ui.AddSpacer(6);

            var waitLib2 = ui.CreateLIBTrio("Amount of props between delay", "", "Prop amount");
            waitLib2.Button.OnClick = () => amountOfPropsForDelay = uint.Parse(waitLib2.Input.Text);

            ui.AddSpacer(6);

            var stopLB = ui.CreateLBDuo("Stop", "", () => Plugin.Instance.StopCoroutine(coroutine), "Stop");

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public void Crash()
        {
            if (Player == null || !Player.Controller.networkObject.IsOwner()) return;
            if (coroutine != null) Plugin.Instance.StopCoroutine(coroutine);

            iterations = int.Parse(iterationsInput.Text);
            coroutine = Plugin._StartCoroutine(Buy(PropShopUtils.FetchAllItems(), 0));
        }

        public IEnumerator Buy(Dictionary<PropShop, AssetReferenceT<ShopPropItem>[]> allItems, int currentIteration)
        {
            foreach (var shop in allItems.Keys)
            {
                foreach (var item in allItems[shop])
                {
                    if (currentIteration >= iterations)
                    {
                        yield break;
                    }

                    shop.Purchase(Player.Controller, item);
                    currentIteration++;

                    if (amountOfPropsForDelay > 0 && currentIteration % amountOfPropsForDelay == 0)
                    {
                        yield return new WaitForSeconds(delayBetweenProps);
                    }
                }
            }

            Plugin._StartCoroutine(Buy(allItems, currentIteration));
        }
    }

    public static class PropShopUtils
    {
        public static Dictionary<PropShop, AssetReferenceT<ShopPropItem>[]> FetchAllItems()
        {
            var propShops = Object.FindObjectsOfType<PropShop>();
            var allItems = new Dictionary<PropShop, AssetReferenceT<ShopPropItem>[]>();

            foreach (var shop in propShops)
            {
                var catalogue = shop.GetPropCatalog();

                if (catalogue && catalogue.GetPropItems() != null && catalogue.GetPropItems().Length > 0)
                {
                    allItems.Add(shop, catalogue.GetPropItems());
                }
            }

            return allItems;
        }
    }
}
