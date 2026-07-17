using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife;

public static class AntiPiracy
{
    private static Texture2D antiPiracyScreenTexture;
    private static Sprite antiPiracyScreenSprite;

    private static Texture2D emotesTexture;

    public static void Initialize()
    {
        antiPiracyScreenTexture ??= LoadEmbeddedTexture("lstwoMODS_WobblyLife.Resources.piracy2.png");
        antiPiracyScreenSprite ??= Sprite.Create(antiPiracyScreenTexture, new Rect(0, 0, antiPiracyScreenTexture.width, antiPiracyScreenTexture.height), new Vector2(0.5f, 0.5f));
        
        emotesTexture ??= LoadEmbeddedTexture("lstwoMODS_WobblyLife.Resources.piracy_emotes.png");
        
        new Harmony("net.lstwo.lstwoMODS_WobblyLife.AntiPiracy").PatchAll(typeof(Patches));
    }
    
    public static Texture2D LoadEmbeddedTexture(string resourcePath)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourcePath);
        
        if (stream == null)
        {
            return null;
        }

        var data = new byte[stream.Length];
        stream.Read(data, 0, data.Length);

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(data);

        return texture;
    }

    public static class Patches
    {
        [HarmonyPatch(typeof(Image), "OnEnable")]
        [HarmonyPrefix]
        public static void Image_OnEnable(ref Image __instance)
        {
            if (__instance.GetComponentInParent<UIGameplayEmoteWheel>())
            {
                return;
            }
            
            __instance.sprite = antiPiracyScreenSprite;
        }
        
        [HarmonyPatch(typeof(Image), "sprite", MethodType.Setter)]
        [HarmonyPrefix]
        public static bool Image_set_sprite(ref Image __instance, ref Sprite value)
        {
            if (__instance.GetComponentInParent<UIGameplayEmoteWheel>())
            {
                return true;
            }
            
            if (value != antiPiracyScreenSprite)
            {
                __instance.sprite = antiPiracyScreenSprite;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(UIGameplayEmoteWheel), "Awake")]
        [HarmonyPrefix]
        public static void EmoteWheelAwake(ref UIGameplayEmoteWheel __instance)
        {
            foreach (var image in __instance.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite.texture.name == "Emotes")
                {
                    Graphics.CopyTexture(emotesTexture, image.sprite.texture);
                    break;
                }
            }
        }
    }
}