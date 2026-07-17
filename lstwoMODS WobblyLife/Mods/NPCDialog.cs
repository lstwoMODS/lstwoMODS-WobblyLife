using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_Core.Hacks;

namespace lstwoMODS_WobblyLife.Mods;

public class NPCDialog : PlayerBasedMod
{
    public override string Name => "Fake Dialog";
    public override string Description => "Shows a speech bubble with a custom message";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    [ModAction]
    public void ShowSpeechBubble(string message)
    {
        var dialog = Player?.NPCDialog;

        var line = new NPCDialogLineData
        {
            text = message,
            lineType = NPCDialogLineType.Normal
        };

        dialog?.PlayDialog([line], NPCDialogType.Interrupt, bClearPrevious: true);
    }
}