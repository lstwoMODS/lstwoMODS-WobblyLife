using System;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

/// <summary>
/// Thin helper over <see cref="CharacterCustomize"/>'s attach/detach so we can add extra
/// clothing pieces to the <c>attachedClothing</c> list WITHOUT touching the four slot fields.
/// Pieces added this way render and bone-follow like normal clothing, but are invisible to
/// <c>GetClothesData()</c>/vanilla sync, so they never fight the game's own clothing state and
/// survive base-clothes changes. They're purely local visuals; networking is done separately.
/// </summary>
internal static class ClothingStackClothing
{
    public static void Attach(CharacterCustomize customize, ClothingPiece piece)
    {
        if (customize && piece) customize.AttachClothing(piece);
    }

    public static void Detach(CharacterCustomize customize, ClothingPiece piece)
    {
        if (customize && piece) customize.UnattachClothing(piece);
    }

    /// <summary>Return a piece to the pool without having attached it (e.g. spawn raced a respawn).</summary>
    public static void Recycle(ClothingPiece piece)
    {
        if (piece) UnitySingleton<ClothingPoolingManager>.Instance.MakeAvaliable(piece);
    }

    /// <summary>Async: pool-spawn a clothing piece by GUID. <paramref name="onComplete"/> gets null on failure.</summary>
    public static void Spawn(Guid guid, Action<ClothingPiece> onComplete)
    {
        var manager = ClothingManager.Instance;
        if (!manager) { onComplete(null); return; }

        var reference = manager.GetClothingReference(guid);
        if (reference == null) { onComplete(null); return; }

        UnitySingleton<ClothingPoolingManager>.Instance.PopOrAllocClothing(reference, onComplete);
    }
}
