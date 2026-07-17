using System.Collections;
using System.Linq;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lstwoMODS_WobblyLife.Mods;

public class ClothingManagerMod : BaseMod
{
    public override string Name => "Clothing Manager";
    public override string Description => "Unlock or Lock all Clothes";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    private static ClothingAssetReference[] _allClothingCached;
    public static ClothingAssetReference[] AllClothing => _allClothingCached;

    private static Ref<string[]> allClothingDropdownItems = new([]);
    /// <summary>Display names for <see cref="AllClothing"/> (same order/length), populated after MainMenu load.</summary>
    public static Ref<string[]> AllClothingNames => allClothingDropdownItems;
    private static Ref<int> selectingClothingIndex = new();

    private static bool _initialized;

    protected override void OnStaticInit()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (_initialized || mode != LoadSceneMode.Single || scene.name != "MainMenu" || !ClothingManager.Instance) return;
            StartCoroutine(InitializeCoroutine());
        };
    }

    private IEnumerator InitializeCoroutine()
    {
        _initialized = true;
        _allClothingCached = ClothingManager.Instance.GetAllClothingReferences();
        
        var array = new string[_allClothingCached.Length];

        for (var i = 0; i < _allClothingCached.Length; i++)
        {
            var clothingAssetReference = _allClothingCached[i];
            
            if (clothingAssetReference.clothingPrefab.IsRawObjectValid())
            {
                // Prefab names aren't guaranteed unique across variants; the hidden ##index
                // keeps every dropdown entry uniquely selectable (ImGui keys on the string).
                array[i] = $"{clothingAssetReference.clothingPrefab.rawObject.name}##{i}";
                continue;
            }

            var handle = clothingAssetReference.clothingPrefab.reference.LoadAssetAsync<GameObject>();
            yield return handle;

            array[i] = $"{handle.Result.name}##{i}";
            handle.Release();
        }

        allClothingDropdownItems.Value = array;
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new SeparatorText("Unlock / Lock Specific Clothing", "Unlock / Lock Specific Clothing"),
            new SearchableCombo("Select Clothing", []).WithItems(allClothingDropdownItems).WithSelectedIndex(selectingClothingIndex),

            new HStack("Selected Clothing Actions",
                ActionMenu(new Button("Unlock Selected Clothing", () => UnlockClothing(AllClothing[selectingClothingIndex.Value])), nameof(UnlockClothing)),
                ActionMenu(new Button("Lock Selected Clothing", () => LockClothing(AllClothing[selectingClothingIndex.Value])), nameof(LockClothing))
            ).WithContentWidth(),

            new SeparatorText("Unlock / Lock All Clothing", "Unlock / Lock All Clothing"),

            new HStack("All Clothing Actions",
                ActionMenu(new Button("Unlock All Clothing", UnlockAllClothing), nameof(UnlockAllClothing)),
                ActionMenu(new Button("Lock All Clothing", LockAllClothing), nameof(LockAllClothing))
            ).WithContentWidth()
        );
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockAllClothing()
    {
        foreach (var clothing in AllClothing)
        {
            UnlockClothing(clothing);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void LockAllClothing()
    {
        foreach (var clothing in AllClothing)
        {
            LockClothing(clothing);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockClothing(ClothingAssetReference clothing)
    {
        GameInstance.Instance?.GetFirstLocalPlayerController()?.GetPlayerControllerUnlocker()?.UnlockClothing(Plugin.Instance, clothing);
    }
    
    [ModAction(ShowInUI = false)]
    public static void LockClothing(ClothingAssetReference clothing)
    {
        GameInstance.Instance?.GetFirstLocalPlayerController()?.GetPlayerControllerUnlocker()?.LockClothing(clothing);
    }
}