using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using UnityExplorer;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class RagdollManager : PlayerBasedMod
{
    public override string Name => "Ragdoll Manager";
    public override string Description => "Change Properties of your Ragdoll";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    private Ref<bool> _isRagdollLocked = new();

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new SeparatorText("Lock / Unlock Ragdoll", "Lock / Unlock Ragdoll"),
            new ToggleButton("Lock Unlock Toggle", "Lock Ragdoll", "Unlock Ragdoll", false, b =>
            {
                if(b) LockRagdoll();
                else UnlockRagdoll();
            }).WithState(_isRagdollLocked).WithContentWidth(),
            
            AutoUIBuilder.Build(this, id),
            
            new Button("Inspect \"Ragdoll Controller\" Component", () =>
            {
                if(Player != null && Player.RagdollController)
                {
                    InspectorManager.Inspect(Player.RagdollController);
                    UIManager.ShowMenu = true;
                }
            }).WithContentWidth()
        );
    }

    [ModAction(ShowInUI = false)]
    public void LockRagdoll()
    {
        Player?.RagdollController.LockRagdollState();
    }

    [ModAction(ShowInUI =  false)]
    public void UnlockRagdoll()
    {
        Player?.RagdollController.UnlockRagdollState();
    }
    
    [ModAction(SeparatorText = "Ragdoll / Knockout Player", Order = 30)]
    public void KnockoutPlayer()
    {
        Player.RagdollController.Knockout();
    }

    [ModAction(Order = 40)]
    public void RagdollPlayer()
    {
        Player.RagdollController.Ragdoll();
    }

    public override void RefreshUI()
    {
        _isRagdollLocked.Value = Player?.RagdollController?.IsRagdollStateLocked() ?? false;
        base.RefreshUI();
    }
}