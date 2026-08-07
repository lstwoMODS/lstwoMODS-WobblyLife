using HarmonyLib;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityExplorer;
using UnityExplorer.UI;
using Button = lstwoMODS_Core.UI.Elements.Button;

namespace lstwoMODS_WobblyLife.Mods;

public class MovementManager : PlayerBasedMod
{
    public override string Name => "Movement Manager";
    public override string Description => "Allows you to change speed, jump height, toggle noclip and more.";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    [ModSetting(Order = 5)]
    public bool EnableNoclip
    {
        get => Player?.CharacterMovement?.IsNoClipEnabled() ?? false;
        set => Player?.CharacterMovement?.SetNoClipEnabled(value);
    }

    [ModSetting(Speed = 0.05f, Order = 10, ApplyButton = true)]
    public float MoveSpeed
    {
        get => Player?.CharacterMovement?.GetSpeedMultiplier() ?? 0f;
        set => Player?.CharacterMovement?.SetSpeedMultiplier(value);
    }

    [ModSetting(Speed = 0.05f, Order = 20, ApplyButton = true)]
    public float JumpHeight
    {
        get => Player?.CharacterMovement?.GetJumpMultiplier() ?? 0f;
        set => Player?.CharacterMovement?.SetJumpMultiplier(value);
    }
    
    [ModSetting(Label = "Enable Infinite Jump Hack (Host Only)", Order = 30)]
    public static bool EnableInfiniteJump;

    [ModSetting(Label = "Enable Multi Jump Hack (Host Only)", Order = 40)]
    public static bool EnableMultiJump;
    
    protected override void OnStaticInit()
    {
        new Harmony("lstwoMODS.Movement").PatchAll(typeof(MovementPatches));
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            base.BuildPanel(id),
            
            new Button("Inspect \"Player Character Movement\" Component", () =>
            {
                if (Player?.CharacterMovement)
                {
                    InspectorManager.Inspect(Player.CharacterMovement);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }

    public override void Update()
    {
        if (Player == null || Player?.Character == null || !Player?.Character?.GetRewiredPlayer()?.GetButtonDown("Jump") == true || !EnableMultiJump)
        {
            return;
        }
        
        Player.CharacterMovement.bCanJump = true;
        Player.CharacterMovement.bIsGrounded = true;
    }
}

public static class MovementPatches
{
    [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateJump")]
    [HarmonyPrefix]
    public static void InfiniteJumpHack(PlayerCharacterMovement __instance, bool bJump)
    {
        if (MovementManager.EnableInfiniteJump != true || !__instance.GetPlayerBody().GetPlayerCharacter().GetPlayerController().networkObject.IsOwner())
        {
            return;
        }
        
        __instance.bCanJump = true;
        __instance.bIsGrounded = true;
    }
}
