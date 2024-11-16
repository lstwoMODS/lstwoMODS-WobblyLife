using NotAzzamods.UI.TabMenus;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NotAzzamods.Hacks
{
    public abstract class BaseHack
    {
        /// <summary>
        /// The name showed on the Hack button.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Unused for now.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// The tab your hack should get added to.
        /// </summary>
        public abstract HacksTab HacksTab { get; }

        /// <summary>
        /// The currently selected Player.
        /// </summary>
        public PlayerRef Player { get; set; }

        public BaseHack()
        {
            Plugin.Hacks.Add(this);
        }

        /// <summary>
        /// Called when UI gets constructed. Use this to create your UI.
        /// </summary>
        /// <param name="root">The root layout group. Place your objects as children of this object.</param>
        public abstract void ConstructUI(GameObject root);

        /// <summary>
        /// Called every frame. Use for special mods that need this.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Called when the UI, Tab or Mod is opened / closed. Use this to refresh your UI values.
        /// </summary>
        public abstract void RefreshUI();
    }
}
