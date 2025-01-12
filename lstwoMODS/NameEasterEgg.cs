using Steamworks;
using System.Reflection;
using TMPro;
using UnityEngine;
using System.Linq;
using System;

namespace NotAzzamods
{
    internal class NameEasterEgg : MonoBehaviour
    {
        public static SteamId[] rainbowSteamIDs = new SteamId[] { 76561198824442161, 76561199083118091 };

        private static readonly FieldInfo textMeshProField = typeof(CharacterNameTag).GetField("textMeshPro", Plugin.Flags);

        public PlayerController playerParent;
        public float rainbowMultiplier = 1f;

        public void Update()
        {
            try
            {
                var steamConnection = (SteamConnection)playerParent.networkObject.GetOwner();

                if (rainbowSteamIDs.Contains(steamConnection.steamId))
                {
                    var textMeshPro = (TextMeshPro)textMeshProField.GetValue(GetComponent<CharacterNameTag>());
                    var textColor = textMeshPro.color;

                    Color.RGBToHSV(textColor, out var hue, out var _, out var _);

                    hue += Time.deltaTime * rainbowMultiplier % 1;
                    var saturation = 0.5f;
                    var brightness = 1f;

                    textMeshPro.color = Color.HSVToRGB(hue, saturation, brightness);
                }
            }
            catch
            {
            }
        }
    }
}
