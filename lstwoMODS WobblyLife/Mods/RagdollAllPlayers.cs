using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using UnityEngine;
using UnityEngine.UI;
using lstwoMODS_Core;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.Hacks.ModActions;

namespace lstwoMODS_WobblyLife.Hacks;

public class RagdollAllPlayers : BaseMod
{
    public override string Name => "Ragdoll All Players";
    public override string Description => "";
    public override ModsTab ModsTab => Plugin.ServerModsTab;

    private float advancedKillTime;

    public override void RenderUI()
    {
        ImGui.Text("Ragdoll All Players");
        ImGui.SameLine();

        if (ImGui.Button("Ragdoll"))
        {
            RagdollPlayers();
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Knockout"))
        {
            KnockoutPlayers();
        }
        
        
        ImGui.Text("Kill All Players");
        ImGui.SameLine();

        if (ImGui.Button("Quick Kill"))
        {
            KillPlayers();
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Respawn"))
        {
            KillPlayers(0);
        }
        
        
        ImGui.Text("Advanced Kill All Players");
        ImGui.SameLine();

        ImGui.DragFloat("", ref advancedKillTime, 0.01f);
        ImGui.SameLine();

        if (ImGui.Button("Kill"))
        {
            KillPlayers(advancedKillTime);
        }
    }

    [ModAction(Name = "Kill All Players")]
    public static void KillPlayers(float time = 1)
    {
        if (!GameInstance.InstanceExists) return;

        foreach (var controller in GameInstance.Instance.GetPlayerControllers())
        {
            var player = new PlayerRef();
            player.SetPlayerController(controller);
            player.Character?.Kill(time);
        }
    }

    [ModAction(Name = "Knockout All Players")]
    public static void KnockoutPlayers()
    {
        if (!GameInstance.InstanceExists) return;

        foreach (var controller in GameInstance.Instance.GetPlayerControllers())
        {
            var player = new PlayerRef();
            player.SetPlayerController(controller);
            player.RagdollController?.Knockout();
        }
    }

    [ModAction(Name = "Ragdoll All Players")]
    public static void RagdollPlayers()
    {
        if (!GameInstance.InstanceExists) return;

        foreach (var controller in GameInstance.Instance.GetPlayerControllers())
        {
            var player = new PlayerRef();
            player.SetPlayerController(controller);
            player.RagdollController?.Ragdoll();
        }
    }

    public override void RefreshUI()
    {
    }

    public override void Update()
    {
    }
}