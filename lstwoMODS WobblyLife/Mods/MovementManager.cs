using System.Reflection;
using HarmonyLib;
using ImGuiNET;
using lstwoMODS_Core.Hacks.ModActions;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Hacks;
using lstwoMODS_WobblyLife.UI.TabMenus;
using UnityEngine;
using UnityEngine.UI;
using UnityExplorer;
using UnityExplorer.UI;

namespace lstwoMODS_WobblyLife.Mods;

public class MovementManager : PlayerBasedMod
{
    public static bool infiniteJump = false;
    public static bool multiJump = false;

    public override string Name => "Movement Manager";
    public override string Description => "Allows you to change speed, jump height, toggle noclip and more.";
    public override ModsTab ModsTab => Plugin.PlayerHacksTab;

    private float moveSpeed;
    private float jumpHeight;
    private bool enableNoclip;

    public MovementManager()
    {
        new Harmony("NotAzza.Movement").PatchAll(typeof(MovementPatches));
    }
    
    public override void RenderUI()
    {
        if (ImGui.DragFloat("Move Speed", ref moveSpeed, .01f, 0, float.MaxValue))
        {
            SetMoveSpeed(Player, moveSpeed);
        }

        if (ImGui.DragFloat("Jump Height", ref jumpHeight, .01f, 0, float.MaxValue))
        {
            SetJumpHeight(Player, jumpHeight);
        }

        if (ImGui.Checkbox("Enable Infinite Jump Hack (Host Only)", ref infiniteJump))
        {
            if(infiniteJump) multiJump = false;
        }

        if (ImGui.Checkbox("Enable Multi Jump Hack (Host Only)", ref multiJump))
        {
            if (multiJump) infiniteJump = false;
        }

        if (ImGui.Checkbox("Enable Noclip", ref enableNoclip))
        {
            SetNoclipEnabled(Player, enableNoclip);
        }

        if (ImGui.Button("Inspect \"Player Character Movement\" Component", new Vector2(624, 32)) && Player != null && Player.CharacterMovement)
        {
            InspectorManager.Inspect(Player.CharacterMovement);
            UIManager.ShowMenu = true;
        }
    }

    public override void Update()
    {
        if (Player == null || Player.Character == null || !Player.Character.GetRewiredPlayer().GetButtonDown("Jump") || !multiJump)
        {
            return;
        }
        
        var __instance = Player.CharacterMovement;
        var canJumpField = typeof(PlayerCharacterMovement).GetField("bCanJump", Plugin.Flags);
        canJumpField.SetValue(__instance, true);

        var groundedField = typeof(PlayerCharacterMovement).GetField("bIsGrounded", Plugin.Flags);
        groundedField.SetValue(__instance, true);
    }

    public override void RefreshUI()
    {
        if (Player == null)
        {
            return;
        }
        
        moveSpeed = Player.CharacterMovement.GetSpeedMultiplier();
        jumpHeight = Player.CharacterMovement.GetJumpMultiplier();
        enableNoclip = Player.CharacterMovement.IsNoClipEnabled();
    }


    [ModAction(Name = "Set Move Speed")]
    public static void SetMoveSpeed(PlayerRef player, float speed)
    {
        player?.CharacterMovement.SetSpeedMultiplier(speed);
    }

    [ModAction(Name = "Set Jump Height")]
    public static void SetJumpHeight(PlayerRef player, float height)
    {
        player?.CharacterMovement.SetJumpMultiplier(height);
    }
    
    [ModAction(Name = "Set Noclip Enabled")]
    public static void SetNoclipEnabled(PlayerRef player, bool b)
    {
        player?.CharacterMovement.SetNoClipEnabled(b);
    }

    [ModAction(Name = "Set Infinite Jump")]
    public static void SetInfiniteJump(bool b)
    {
        infiniteJump = b;
        if (b) multiJump = false;
    }

    [ModAction(Name = "Set Multi Jump")]
    public static void SetMultiJump(bool b)
    {
        multiJump = b;
        if (b) infiniteJump = false;
    }
}

public static class MovementPatches
{
    [HarmonyPatch(typeof(PlayerCharacterMovement), "SimulateJump")]
    [HarmonyPrefix]
    public static void InfiniteJumpHack(PlayerCharacterMovement __instance, bool bJump)
    {
        if (MovementManager.infiniteJump != true || !__instance.GetPlayerBody().GetPlayerCharacter().GetPlayerController().networkObject.IsOwner())
        {
            return;
        }
        
        var canJumpField = typeof(PlayerCharacterMovement).GetField("bCanJump", Plugin.Flags);
        canJumpField.SetValue(__instance, true);

        var groundedField = typeof(PlayerCharacterMovement).GetField("bIsGrounded", Plugin.Flags);
        groundedField.SetValue(__instance, true);
    }
}
