using lstwoMODS_Core.Hacks;
using lstwoMODS_WobblyLife.UI.TabMenus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks;

public abstract class PlayerBasedHack : BaseHack
{
    /// <summary>
    /// The currently selected Player.
    /// </summary>
    public PlayerRef Player { get; set; }
}