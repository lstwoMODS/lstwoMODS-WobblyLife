using System;
using System.Collections.Generic;
using lstwoMODS_Core.Hacks;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods;

[ModContext(typeof(PlayerRef), Description = "The player this mod operates on", Key = "Player")]
public abstract class PlayerBasedMod : BaseMod
{
    public PlayerRef Player { get; set; }

    public override void SetContext(ModExecutionContext context)
    {
        base.SetContext(context);
        if (context != null && context.TryGet<PlayerRef>("Player", out var player) && player != null)
            Player = player;
    }

    private static readonly Dictionary<Type, Dictionary<PlayerController, object>> _allSettings = new();

    public static T GetPlayerSettings<T>(Type modType, PlayerController controller) where T : new()
    {
        if (controller == null) return new T();
        if (!_allSettings.TryGetValue(modType, out var typeDict))
            _allSettings[modType] = typeDict = new Dictionary<PlayerController, object>();
        if (!typeDict.TryGetValue(controller, out var s))
            typeDict[controller] = s = new T();
        return (T)s;
    }

    public T GetPlayerSettings<T>(PlayerController controller) where T : new() =>
        GetPlayerSettings<T>(GetType(), controller);
}