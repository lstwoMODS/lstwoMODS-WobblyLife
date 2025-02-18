using HarmonyLib;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Hacks
{
    public class MuseumManager : BaseHack
    {
        private static bool forceFinishMuseum = false;

        public override string Name => "Museum Manager";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.SaveHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateToggle("lstwo.MuseumManager.forceFinishMuseumToggle", "Force Finish all Museum Collections", (b) => forceFinishMuseum = b);

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }

        public class WorldMissionMuseumPatch
        {
            [HarmonyPatch(typeof(WorldMissionMuseum), "HasCompletedAllCollections")]
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result)
            {
                if (forceFinishMuseum)
                {
                    __result = true;
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
