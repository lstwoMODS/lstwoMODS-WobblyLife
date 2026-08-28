using System.Reflection;
using TMPro;
using UnityEngine;
using System.Linq;

namespace lstwoMODS_WobblyLife;

internal class NameEasterEgg : MonoBehaviour
{
    public static ulong[] rainbowSteamIDs = [76561199083118091, 76561198824442161];
    public static ulong[] goldSteamIDs = [76561199109862806, 76561199628305098];

    private static readonly PlayerKey[] RainbowKeys = rainbowSteamIDs.Select(PlayerKey.Steam).ToArray();
    private static readonly PlayerKey[] GoldKeys = goldSteamIDs.Select(PlayerKey.Steam).ToArray();

    private static readonly FieldInfo textMeshProField = typeof(CharacterNameTag).GetField("textMeshPro", Plugin.Flags);

    public PlayerController playerParent;
    public float rainbowMultiplier = 1f;
        
    public float goldHue = 0.1389f;
    public float goldSaturation = 1f;
    public float goldShimmerAmount = 0.15f;
    public float goldShimmerOffset = 0.05f;
    public float goldSaturationShimmerMultiplier = 2f;
    public float goldSaturationShimmerOffset = 1.5f;

    public void Update()
    {
        try
        {
            // These are Steam accounts, so nothing matches on a LAN transport or on the crossplay
            // build (where peers are EOS accounts). Short-circuit instead of throwing: this runs
            // per frame, per nametag.
            var key = PlayerIdentity.Of(playerParent.networkObject.GetOwner());
            if (!key.IsValid)
                return;
            var textMeshPro = (TextMeshPro)textMeshProField.GetValue(GetComponent<CharacterNameTag>());

            if (RainbowKeys.Contains(key))
            {
                var textColor = textMeshPro.color;

                Color.RGBToHSV(textColor, out var hue, out var _, out var _);

                hue += Time.deltaTime * rainbowMultiplier % 1;
                const float saturation = 0.5f;
                const float brightness = 1f;

                textMeshPro.color = Color.HSVToRGB(hue, saturation, brightness);
            }
            else if (GoldKeys.Contains(key))
            {
                var shimmer = 1f - (goldShimmerAmount + goldShimmerOffset) + Mathf.Sin(Time.time * 2f) * goldShimmerAmount;

                textMeshPro.color = Color.HSVToRGB(goldHue, goldSaturation - (shimmer * goldSaturationShimmerMultiplier - goldSaturationShimmerOffset), shimmer);
            }
        }
        catch
        {
            // ignored
        }
    }
}
