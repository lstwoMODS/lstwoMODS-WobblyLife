using System.Collections;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

public class SpectateMod : PlayerBasedMod
{
    public override string Name => "Spectate";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new Checkbox("Enable Spectator Mode", false, b =>
            {
                if (Player?.ControllerSpectate == null) return;
                
                Player.ControllerSpectate.ServerSetSpectating(b);

                if (!b)
                {
                    StartCoroutine(Routine());
                }
            })
        );
    }

    private IEnumerator Routine()
    {
        Player.Controller.allowRespawningHandles.Clear();
        Player.Controller.allowRespawningHandlesLocal.Clear();
        
        Player.ControllerSpectate.ServerSetSpectating(false);
        yield return null;
        Player.ControllerSpectate.ServerSetSpectating(true);
        yield return null;
        Player.ControllerSpectate.ServerSetSpectating(false);
        yield return null;
        
        Player.Controller.allowRespawningHandles.Clear();
        Player.Controller.allowRespawningHandlesLocal.Clear();
        
        Player.Controller.ClientRequestRespawn();
    }
}