using System.Collections.Generic;
using lstwoMODS.WobblyLife.SharedObjects;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.UI.Elements;

/// <summary>
/// Single opaque element that hands all prop-spawner rendering to the
/// WobblyLife overlay extension running natively inside the overlay process.
/// </summary>
public class PropSpawnerElement : BaseUIElement<PropSpawnerElement>
{
    public PropSpawnerElement(string name) : base(name)
    {
        Data = new PropSpawnerData { Name = name };
    }

    public override IEnumerable<BaseUIElement> GetChildren() => [];
}
