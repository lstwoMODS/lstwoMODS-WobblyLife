using NotAzzamods.Hacks;
using ShadowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;

namespace NotAzzamods.UI.TabMenus
{
    public abstract class BaseTab
    {
        public BaseTab()
        {
            Plugin.TabMenus.Add(this);
        }

        /// <summary>
        /// The name showed on the tab button.
        /// </summary>
        public string Name;

        /// <summary>
        /// The ui helper for the content of the Tab.
        /// </summary>
        protected HacksUIHelper ui;

        /// <summary>
        /// The root of the tab content.
        /// </summary>
        protected GameObject root;

        /// <summary>
        /// Called when UI gets constructed. Use this to create your UI.
        /// </summary>
        /// <param name="root"></param>
        public virtual void ConstructUI(GameObject root)
        {
            this.root = root;
            ui = new(root);

            root.SetActive(false);
        }

        /// <summary>
        /// Override to change how to tabs button looks.
        /// </summary>
        /// <param name="ui">The UI Helper for the tab button layout group.</param>
        /// <returns>The button GameObject</returns>
        public virtual GameObject ConstructTabButton(HacksUIHelper ui)
        {
            var btn = ui.CreateButton( "<b>" + Name + "</b>", () =>
            {
                SetTabActive(true);
            }, "", null, 224, 32);

            return btn.GameObject;
        }

        /// <summary>
        /// Used to set the current Tab content active or not active.
        /// </summary>
        /// <param name="active">Wether the content should be shown or not.</param>
        public virtual void SetTabActive(bool active)
        {
            root.SetActive(active);

            if(active)
            {
                Plugin.MainPanel.CurrentTab = this;

                if (Plugin.MainPanel.oldTab != null && Plugin.MainPanel.oldTab != this)
                    Plugin.MainPanel.oldTab.SetTabActive(false);

                Plugin.MainPanel.oldTab = this;

                RefreshUI();
            }
        }

        /// <summary>
        /// Idk never gets called
        /// </summary>
        public abstract void UpdateUI();

        /// <summary>
        /// Called when the tab gets opened. Use this to refresh your tab content.
        /// </summary>
        public abstract void RefreshUI();
    }
}
