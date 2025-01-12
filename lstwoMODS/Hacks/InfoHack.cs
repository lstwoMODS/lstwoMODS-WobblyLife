using NotAzzamods.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks
{
    public class InfoHack : BaseHack
    {
        public override string Name => "Some Hacks are Disabled!";

        public override string Description => "";

        public override HacksTab HacksTab => null;

        public override void ConstructUI(GameObject root)
        {
            var ui = new HacksUIHelper(root);

            ui.AddSpacer(6);

            ui.CreateLabel("Some hacks were disabled and don't work on other players! This is to prevent people from tampering with other people's save files.\n" +
                "This is because some hacks would allow users to reset players money, lock all their clothes, etc.");

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
