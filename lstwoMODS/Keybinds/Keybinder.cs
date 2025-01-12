using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NotAzzamods.Keybinds
{
    public abstract class Keybinder : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// List of keybinds on this keybinder.
        /// </summary>
        public List<Keybind> keybinds = new();

        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClicked();
            }
        }

        /// <summary>
        /// Opens the Keybind Panel when right clicked.
        /// </summary>
        public virtual void OnRightClicked()
        {
            Plugin.KeybindPanel._SetActive(true);
            Plugin.KeybindPanel.Keybinder = this;
        }

        /// <summary>
        /// Use this to create a keybind
        /// </summary>
        /// <returns>The new empty keybind</returns>
        public abstract Keybind CreateKeybind();

        public abstract class Keybind
        {
            public KeyCode? primaryKey = null;
            public List<KeyCode> secondaryKeys = new();
            public Keybinder keybinder;

            public Keybind(Keybinder keybinder)
            {
                this.keybinder = keybinder;
                KeybindManager.AddKeybind(this);
            }

            /// <summary>
            /// Checks if this Keybind is being pressed.
            /// </summary>
            /// <returns></returns>
            public virtual bool IsPressed()
            {
                if(!this.primaryKey.HasValue)
                {
                    return false;
                }

                KeyCode primaryKey = this.primaryKey.Value;

                if (!Input.GetKeyDown(primaryKey))
                {
                    return false;
                }

                foreach(var key in secondaryKeys)
                {
                    if(!Input.GetKey(key))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Whether the keyboard input is being recorded to set the Keybind.
            /// </summary>
            public bool isCapturing = false;

            /// <summary>
            /// Just starts detecting the Keys to set the Keybind.
            /// </summary>
            public virtual void StartDetectKeybind()
            {
                primaryKey = null;
                secondaryKeys = new();
                isCapturing = true;

                KeybindManager.KeybinderToUpdate = keybinder;
            }

            /// <summary>
            /// Just stops detecting the Keys to set the Keybind.
            /// </summary>
            public virtual void StopDetectKeybind()
            {
                isCapturing = false;
            }

            /// <summary>
            /// Only override if absolutley necessary. Detects Keyboard Input to set the keys for the Keybind.
            /// </summary>
            public virtual void DetectKeybinds()
            {
                KeyCode? primaryKey = null;
                List<KeyCode> secondaryKeys = new();

                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKey(key))
                    {
                        if (!IsModifierKey(key))
                        {
                            primaryKey = key;
                            continue;
                        }

                        secondaryKeys.Add(key);
                    }
                }

                if (primaryKey.HasValue)
                {
                    this.primaryKey = primaryKey.Value;
                    this.secondaryKeys = secondaryKeys;
                    StopDetectKeybind();
                }
            }

            private KeyCode[] modifierKeys = { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };

            private bool IsModifierKey(KeyCode keyCode)
            {
                foreach (var modifier in modifierKeys)
                {
                    if (keyCode == modifier)
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Called when the keybind has been pressed. Use to complete your action.
            /// </summary>
            public abstract void OnPressed();

            /// <summary>
            /// Called when a new scroll item for the Keybind Panel needs to be created.
            /// </summary>
            /// <param name="root">The Scroll Root to child your keybind object to</param>
            public abstract void CreateScrollItem(GameObject root);

            /// <summary>
            /// Used to refresh the keybind item in the Keybind Panel's scroll view. Use this to set the keys in your item to match the keybind's.
            /// </summary>
            public abstract void RefreshScrollItem();
        }
    }
}
