using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NotAzzamods.Keybinds
{
    public class KeybindManager : MonoBehaviour
    {
        public static Keybinder KeybinderToUpdate
        {
            get
            {
                return _keybinderToUpdate;
            }
            set
            {
                if(_keybinderToUpdate != null && _keybinderToUpdate != value && _keybinderToUpdate.keybinds.Any(keybind => keybind.isCapturing))
                {
                    foreach(var keybind in _keybinderToUpdate.keybinds.Where(keybind => keybind.isCapturing))
                    {
                        keybind.StopDetectKeybind();
                    }
                }

                _keybinderToUpdate = value;
            }
        }

        private static List<Keybinder.Keybind> Keybinds = new List<Keybinder.Keybind>();
        private static Keybinder _keybinderToUpdate;

        void Update()
        {
            if (_keybinderToUpdate != null)
            {
                foreach (var keybind in _keybinderToUpdate.keybinds.Where(keybind => keybind.isCapturing))
                {
                    keybind.DetectKeybinds();
                }
            }

            if (Plugin.UiBase == null || Plugin.UiBase.Enabled)
            {
                return;
            }

            foreach (var keybind in Keybinds)
            {
                if(keybind.IsPressed())
                {
                    keybind.OnPressed();
                }
            }
        }

        public static Keybinder.Keybind AddKeybind(Keybinder.Keybind keybind)
        {
            Keybinds.Add(keybind);
            return keybind;
        }

        public static void RemoveKeybind(Keybinder.Keybind keybind)
        {
            Keybinds.Remove(keybind);
        }
    }
}
