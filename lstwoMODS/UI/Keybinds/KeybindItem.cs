using NotAzzamods.Keybinds;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NotAzzamods.UI.Keybinds
{
    public class KeybindItem : MonoBehaviour, IPointerClickHandler
    {
        public Keybinder.Keybind keybind;

        public bool Selected
        {
            set
            {
                if (value)
                {
                    GetComponent<Image>().color = HacksUIHelper.BGColor2;
                }
                else
                {
                    GetComponent<Image>().color = HacksUIHelper.BGColor1;
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Plugin.KeybindPanel.SelectedKeybind = this;
        }
    }
}
