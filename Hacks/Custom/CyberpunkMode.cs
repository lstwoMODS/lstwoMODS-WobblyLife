using HawkNetworking;
using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks.Custom
{
    public class CyberpunkMode : BaseHack
    {
        public override string Name => "Cyberpunk Mode";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBDuo("Enable Cyberpunk Mode (Cannot be disabled)", "name", () =>
            {
                Time.fixedDeltaTime = 0.05f;
                foreach(var rb in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
            }, "Apply");

            ui.AddSpacer(6);
        }

        public override void RefreshUI()
        {
        }

        public override void Update()
        {
        }
    }
}
