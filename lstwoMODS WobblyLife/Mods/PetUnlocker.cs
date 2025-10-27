using lstwoMODS_Core;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public class PetUnlocker : BaseHack
{
    public override void ConstructUI(GameObject root)
    {
        var ui = new HacksUIHelper(root);
        
        ui.AddSpacer(6);

        ui.CreateLBBTrio("Unlock / Lock All Pets", "UnlockLockAllPetsLBB", () =>
        {
            var player = GameInstance.Instance.GetFirstLocalPlayerController();
            var controllerPet = player.GetPlayerControllerPet();
            var pets = PetManager.Instance.GetAllPets();

            foreach (var pet in pets)
            {
                controllerPet.UnlockPet(pet.prefab);
            }
        }, "Unlock", "UnlockAllPetsButton", () =>
        {
            var player = GameInstance.Instance.GetFirstLocalPlayerController();
            var controllerPet = player.GetPlayerControllerPet();
            var pets = PetManager.Instance.GetAllPets();

            foreach (var pet in pets)
            {
                controllerPet.LockPet(pet.prefab);
            }
        }, "Lock", "LockAllPetsButton");
        
        ui.AddSpacer(6);
    }

    public override void Update()
    {
    }

    public override void RefreshUI()
    {
    }

    public override string Name => "Pet Manager";
    public override string Description => "";
    public override HacksTab HacksTab => Plugin.SaveHacksTab;
}