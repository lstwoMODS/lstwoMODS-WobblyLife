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
    public class AccuratePhysicsMode : BaseHack
    {
        public override string Name => "\"stop whining about physics going wrong\" mode";

        public override string Description => "";

        public override HacksTab HacksTab => Plugin.ExtraHacksTab;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLBDuo("Enable (Cannot be disabled)", "name", () =>
            {
                foreach(var rb in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
