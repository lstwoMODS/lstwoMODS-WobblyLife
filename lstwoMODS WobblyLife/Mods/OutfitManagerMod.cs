using System;
using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife.Mods.ClothingStack;

namespace lstwoMODS_WobblyLife.Mods;

public class OutfitManagerMod : BaseMod
{
    public override string Name => "Outfit Manager";
    public override string Description => "Save your current outfit under a name and re-apply, rename or delete saved outfits";
    public override ModsWindow ModsWindow => Plugin.ClientModsWindow;

    private const string SaveKey = "outfits";

    private readonly List<SavedOutfit> _outfits = new();

    private readonly Ref<string> _nameField = new("");
    private readonly Ref<string[]> _outfitNames = new([]);
    private readonly Ref<int> _selectedIndex = new(0);

    protected override void Awake()
    {
        var loaded = LoadData<List<SavedOutfit>>(SaveKey);
        if (loaded != null)
            _outfits.AddRange(loaded.Where(o => o != null && !string.IsNullOrEmpty(o.Name)));
        RefreshNames();
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,

            new SeparatorText("Save Current Outfit", "Save Current Outfit"),
            new InputText("Outfit Name", hint: "Name your outfit...").WithValue(_nameField),
            ActionMenu(
                new Button("Save Current Outfit", () => SaveCurrentOutfit(_nameField.Value)).WithContentWidth(),
                nameof(SaveCurrentOutfit)),

            new SeparatorText("Saved Outfits", "Saved Outfits"),
            new SearchableCombo("Select Outfit", []).WithItems(_outfitNames).WithSelectedIndex(_selectedIndex),

            new HStack("Outfit Actions",
                ActionMenu(new Button("Apply", ApplySelected), nameof(ApplyOutfit)),
                new Button("Rename to Name", RenameSelected),
                new Button("Delete", DeleteSelected)
            ).WithContentWidth()
        );
    }

    private SavedOutfit Selected =>
        _selectedIndex.Value >= 0 && _selectedIndex.Value < _outfits.Count
            ? _outfits[_selectedIndex.Value]
            : null;

    private static CharacterCustomize LocalCustomize =>
        GameInstance.Instance?.GetFirstLocalPlayerController()?.GetPlayerCharacter()?.GetPlayerCharacterCustomize();

    private void ApplySelected()
    {
        var outfit = Selected;
        if (outfit != null) ApplyOutfit(outfit.Name);
    }

    private void RenameSelected()
    {
        var outfit = Selected;
        if (outfit == null) return;

        var newName = _nameField.Value?.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            Plugin.LogSource.LogWarning("[OutfitManager] Enter a name in the box before renaming.");
            return;
        }

        if (_outfits.Any(o => o != outfit && o.Name == newName))
        {
            Plugin.LogSource.LogWarning($"[OutfitManager] An outfit named '{newName}' already exists.");
            return;
        }

        outfit.Name = newName;
        Persist();
        RefreshNames();
    }

    private void DeleteSelected()
    {
        var outfit = Selected;
        if (outfit == null) return;

        _outfits.Remove(outfit);
        Persist();
        RefreshNames();
    }

    [ModAction(ShowInUI = false)]
    public void SaveCurrentOutfit(string name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            Plugin.LogSource.LogWarning("[OutfitManager] Enter a name before saving an outfit.");
            return;
        }

        var customize = LocalCustomize;
        if (customize == null)
        {
            Plugin.LogSource.LogWarning("[OutfitManager] No local player character to read the outfit from.");
            return;
        }

        var snapshot = FromClothesData(name, customize.GetClothesData());
        snapshot.Layers = ClothingStackMod.GetWornLayers();

        var existing = _outfits.FirstOrDefault(o => o.Name == name);
        if (existing != null)
        {
            existing.Hat = snapshot.Hat;
            existing.Top = snapshot.Top;
            existing.Bottom = snapshot.Bottom;
            existing.Outfit = snapshot.Outfit;
            existing.Layers = snapshot.Layers;
        }
        else
        {
            _outfits.Add(snapshot);
        }

        Persist();
        RefreshNames();
    }

    [ModAction(ShowInUI = false)]
    public void ApplyOutfit(string name)
    {
        var outfit = _outfits.FirstOrDefault(o => o.Name == name);
        if (outfit == null)
        {
            Plugin.LogSource.LogWarning($"[OutfitManager] No saved outfit named '{name}'.");
            return;
        }

        var controller = GameInstance.Instance?.GetFirstLocalPlayerController();
        var customize = controller?.GetPlayerCharacter()?.GetPlayerCharacterCustomize();
        if (controller == null || customize == null)
        {
            Plugin.LogSource.LogWarning("[OutfitManager] No local player character to apply the outfit to.");
            return;
        }

        var data = ToClothesData(outfit);

        customize.SetClothesData(data);

        var persistent = controller.GetPlayerPersistentData();
        if (persistent != null) persistent.CurrentClothes = data;
        controller.ClientUpdateClothes(data);

        ClothingStackMod.ApplyLayers(outfit.Layers);
    }

    [ModAction(ShowInUI = false)]
    public void DeleteOutfit(string name)
    {
        if (_outfits.RemoveAll(o => o.Name == name) > 0)
        {
            Persist();
            RefreshNames();
        }
    }

    private void RefreshNames()
    {
        _outfitNames.Value = _outfits.Select((o, i) => $"{o.Name}##{i}").ToArray();
        if (_selectedIndex.Value >= _outfits.Count)
            _selectedIndex.Value = Math.Max(0, _outfits.Count - 1);
    }

    private void Persist() => SaveData(SaveKey, _outfits);


    private static SavedOutfit FromClothesData(string name, PlayerClothesData data) => new()
    {
        Name = name,
        Hat = OutfitPiece.From(data.ClothingHat),
        Top = OutfitPiece.From(data.ClothingTop),
        Bottom = OutfitPiece.From(data.ClothingBottom),
        Outfit = OutfitPiece.From(data.ClothingOutfit)
    };

    private static PlayerClothesData ToClothesData(SavedOutfit outfit) => new()
    {
        ClothingHat = OutfitPiece.To(outfit.Hat),
        ClothingTop = OutfitPiece.To(outfit.Top),
        ClothingBottom = OutfitPiece.To(outfit.Bottom),
        ClothingOutfit = OutfitPiece.To(outfit.Outfit)
    };

    private class SavedOutfit
    {
        public string Name { get; set; }
        public OutfitPiece Hat { get; set; }
        public OutfitPiece Top { get; set; }
        public OutfitPiece Bottom { get; set; }
        public OutfitPiece Outfit { get; set; }

        public List<StackLayer> Layers { get; set; }
    }

    private class OutfitPiece
    {
        public string Guid { get; set; }
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }
        public float A { get; set; }

        public static OutfitPiece From(ClothingPieceData data)
        {
            var color = data.clothingPrimaryColor;
            return new OutfitPiece
            {
                Guid = data.clothingPrefabGUID.ToString(),
                R = color.r,
                G = color.g,
                B = color.b,
                A = color.a
            };
        }

        public static ClothingPieceData To(OutfitPiece piece)
        {
            var data = new ClothingPieceData();
            if (piece != null && System.Guid.TryParse(piece.Guid, out var guid))
                data.clothingPrefabGUID = guid;
            data.clothingPrimaryColor = new SerializableColor
            {
                r = piece?.R ?? 1f,
                g = piece?.G ?? 1f,
                b = piece?.B ?? 1f,
                a = piece?.A ?? 1f
            };
            return data;
        }
    }
}
