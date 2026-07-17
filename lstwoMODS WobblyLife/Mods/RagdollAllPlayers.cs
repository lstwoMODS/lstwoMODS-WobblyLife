using lstwoMODS_WobblyLife.UI.TabMenus;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class RagdollAllPlayers : BaseMod
{
    public override string Name => "Ragdoll All Players";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ServerModsWindow;

    [ModAction(SeparatorText = "Ragdoll All Players", Order = 10)]
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
    
    [ModAction(Order = 20)]
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

    [ModAction(SeparatorText = "Kill All Players", Order = 30)]
    public static void QuickKillAllPlayers()
    {
        KillAllPlayers();
    }

    [ModAction(Order = 40)]
    public static void RespawnAllPlayers()
    {
        KillAllPlayers(0);
    }
    
    [ModAction(Order = 50, SeparatorText = "")]
    public static void KillAllPlayers(float timeBeforeRespawn = 1)
    {
        if (!GameInstance.InstanceExists) return;

        foreach (var controller in GameInstance.Instance.GetPlayerControllers())
        {
            var player = new PlayerRef();
            player.SetPlayerController(controller);
            player.Character?.Kill(timeBeforeRespawn);
        }
    }
}