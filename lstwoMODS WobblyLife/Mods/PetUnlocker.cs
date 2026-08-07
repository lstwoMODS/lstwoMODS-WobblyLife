using System.Linq;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class PetUnlocker : BaseMod
{
    public override string Name => "Pet Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.SaveModsWindow;

    public static PetAssetReference[] AllPets => PetManager.InstanceExists ? PetManager.Instance.GetAllPets() : [];

    private static Ref<string[]> petDropdownItems = new([]);
    private static Ref<int> petDropdownIndex = new();

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            SaveGuard.Notice("pet-guard-notice"),

            new Combo("Select Pet", []).WithItems(petDropdownItems).WithSelectedIndex(petDropdownIndex),

            SaveGuard.Guard(new HStack("actions",
                ActionMenu(new Button("Unlock Pet", () => UnlockPet(AllPets[petDropdownIndex.Value])), nameof(UnlockPet)),
                ActionMenu(new Button("Lock Pet", () => LockPet(AllPets[petDropdownIndex.Value])), nameof(LockPet))
            ).WithContentWidth()),

            new SeparatorText("Lock / Unlock All Pets", "Lock / Unlock All Pets"),

            SaveGuard.Guard(new HStack("actions",
                ActionMenu(new Button("Unlock All Pet", UnlockAllPets), nameof(UnlockAllPets)),
                ActionMenu(new Button("Lock All Pet", LockAllPets), nameof(LockAllPets))
            ).WithContentWidth())
        );
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockPet(PetAssetReference pet)
    {
        if (SaveGuard.On) return;

        var player = GameInstance.Instance.GetFirstLocalPlayerController();
        var controllerPet = player.GetPlayerControllerPet();
        controllerPet.UnlockPet(pet.prefab);
    }

    [ModAction(ShowInUI = false)]
    public static void LockPet(PetAssetReference pet)
    {
        if (SaveGuard.On) return;

        var player = GameInstance.Instance.GetFirstLocalPlayerController();
        var controllerPet = player.GetPlayerControllerPet();
        controllerPet.LockPet(pet.prefab);
    }

    [ModAction(ShowInUI = false)]
    public static void UnlockAllPets()
    {
        foreach (var pet in AllPets)
        {
            UnlockPet(pet);
        }
    }

    [ModAction(ShowInUI = false)]
    public static void LockAllPets()
    {
        foreach (var pet in AllPets)
        {
            LockPet(pet);
        }
    }

    public override void RefreshUI()
    {
        petDropdownItems.Value = AllPets.Select(x => x.petName).ToArray();
        base.RefreshUI();
    }
}