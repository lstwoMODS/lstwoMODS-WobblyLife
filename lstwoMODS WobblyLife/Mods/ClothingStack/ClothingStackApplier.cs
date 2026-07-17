using System;
using System.Collections.Generic;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.ClothingStack;

/// <summary>
/// Keeps one character's attached extra pieces in sync with a desired layer list. One instance
/// drives the local player; one per remote player drives everyone else. Reconcile is idempotent
/// and cheap to call every tick, it only does work when the desired set, the target character
/// (respawn), or an attached piece changed.
/// </summary>
internal class ClothingStackApplier
{
    private CharacterCustomize _customize;
    private readonly List<ClothingPiece> _applied = new();
    private string _signature;
    private bool _busy;

    private static readonly List<StackLayer> Empty = new();

    /// <summary>Detach everything and forget (used when a player leaves).</summary>
    public void Clear()
    {
        if (_customize)
            foreach (var piece in _applied)
                if (piece) ClothingStackClothing.Detach(_customize, piece);

        Forget();
    }

    public void Reconcile(CharacterCustomize target, List<StackLayer> desired)
    {
        // Character gone (destroyed together with its clothing pieces): forget without touching
        // the now-invalid pieces. A fresh character next tick triggers a re-apply below.
        if (!target)
        {
            if (_customize || _applied.Count > 0) Forget();
            return;
        }

        desired ??= Empty;
        var signature = ClothingStackCodec.Encode(desired);
        var characterChanged = target != _customize;
        var lostPieces = HasLostPieces();

        if (!characterChanged && !lostPieces && signature == _signature) return;
        if (_busy) return;

        if (!characterChanged && _customize)
            foreach (var piece in _applied)
                if (piece) ClothingStackClothing.Detach(_customize, piece);

        _applied.Clear();
        _customize = target;
        _signature = signature;

        if (desired.Count == 0) { _busy = false; return; }

        var capture = target;
        var remaining = desired.Count;
        _busy = true;

        foreach (var layer in desired)
        {
            if (layer == null || !Guid.TryParse(layer.Guid, out var guid) || guid == Guid.Empty)
            {
                if (--remaining <= 0) _busy = false;
                continue;
            }

            var applyColor = layer.A >= 0f;
            var color = new Color(layer.R, layer.G, layer.B, Mathf.Max(0f, layer.A));

            ClothingStackClothing.Spawn(guid, piece =>
            {
                // Guard against a respawn that happened while the spawn was in flight.
                if (piece && capture && _customize == capture)
                {
                    ClothingStackClothing.Attach(capture, piece);
                    // Pooled pieces keep the colour of their previous wearer, so always set one: the
                    // captured override, or the piece's own default when the layer has none.
                    if (applyColor) piece.SetPrimaryColor(color);
                    else piece.Default();
                    _applied.Add(piece);
                }
                else if (piece)
                {
                    ClothingStackClothing.Recycle(piece);
                }

                if (--remaining <= 0) _busy = false;
            });
        }
    }

    private void Forget()
    {
        _applied.Clear();
        _signature = null;
        _customize = null;
        _busy = false;
    }

    private bool HasLostPieces()
    {
        for (var i = 0; i < _applied.Count; i++)
            if (!_applied[i]) return true;
        return false;
    }
}
