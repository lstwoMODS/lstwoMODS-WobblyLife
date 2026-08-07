using System;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using UnityExplorer;

namespace lstwoMODS_WobblyLife.Mods;

public class NpcManager : PlayerBasedMod
{
    public override string Name => "NPC Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    //public static string NpcGuid = "14fb04f12bc2f5440b0b6052b6c46af7";

    public static AllClothingAssetReferences AllClothingAssetReferences => ClothingManager.Instance.allClothingAssetReferences;

    public static List<GameObject> SpawnedNpcs = [];

    private static Ref<int> _currentSelectedNpcIndex = new();
    private static Ref<string[]> _dropdownNpcItems = new([]);
    private static Ref<bool> _isNpcSelectionInvalid = new(true);

    private Ref<string> npcNameInput = new("New NPC");

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new SeparatorText("Spawn NPC", "Spawn NPC"),
            
            new InputText("NPC Name").WithValue(npcNameInput),
            new Button("Spawn New NPC", () =>
            {
                var playerBody = GameInstance.Instance.GetFirstLocalPlayerController();
            
                SpawnNewNpc(behaviour =>
                {
                    AddNpcToList(behaviour.gameObject, npcNameInput.Value);
                    npcNameInput.Value = "New NPC (" + _dropdownNpcItems.Value.Length + ")";
                    _currentSelectedNpcIndex.Value = _dropdownNpcItems.Value.Length - 1;
                    behaviour.transform.position = playerBody.transform.position;

                    RefreshUI();

                }, playerBody.transform.position, bSendTransform: true);
            }),
            
            new SeparatorText("Edit NPC", "Edit NPC"),
            
            new Combo("Select NPC", [], 0, _ => RefreshUI()).WithItems(_dropdownNpcItems).WithSelectedIndex(_currentSelectedNpcIndex),
            new Spacing("spacer"),
            
            new UIText("No NPC Selected", "No NPC Selected").WithVisible(_isNpcSelectionInvalid),
            new Group("NPC Settings",
                
                new Button("Transfer Clothing to NPC", () =>
                {
                    var playerCustomize = Player.CharacterCustomize;
                    var npcCustomize = SpawnedNpcs[_currentSelectedNpcIndex.Value].GetComponent<CharacterCustomize>();

                    if (playerCustomize.GetClothingHat())
                    {
                        var hatReference = AllClothingAssetReferences.GetClothing(playerCustomize.GetClothingHat().GetGuid());
                        npcCustomize.SetClothingPiece(hatReference, ClothingSelectionType.Hat, data: new ClothingPieceData { clothingPrimaryColor = playerCustomize.GetClothingHat().GetPrimaryColor() });
                    }
            
                    if (playerCustomize.GetClothingTop())
                    {
                        var hatReference = AllClothingAssetReferences.GetClothing(playerCustomize.GetClothingTop().GetGuid());
                        npcCustomize.SetClothingPiece(hatReference, ClothingSelectionType.Top, data: new ClothingPieceData { clothingPrimaryColor = playerCustomize.GetClothingTop().GetPrimaryColor() });
                    }
            
                    if (playerCustomize.GetClothingBottom())
                    {
                        var hatReference = AllClothingAssetReferences.GetClothing(playerCustomize.GetClothingBottom().GetGuid());
                        npcCustomize.SetClothingPiece(hatReference, ClothingSelectionType.Bottom, data: new ClothingPieceData { clothingPrimaryColor = playerCustomize.GetClothingBottom().GetPrimaryColor() });
                    }
                    
                }).WithContentWidth(),
                
                new Button("Teleport NPC to Player", () =>
                {
                    SpawnedNpcs[_currentSelectedNpcIndex.Value]?.transform.position = Player.Character.GetPlayerBody().transform.position;
                    
                }).WithContentWidth(),
                
                new Button("Inspect NPC Object", () =>
                {
                    InspectorManager.Inspect(SpawnedNpcs[_currentSelectedNpcIndex.Value]);
                    UnityExplorer.UI.UIManager.ShowMenu = true;
                    
                }).WithContentWidth()
                
            ).WithDisabled(_isNpcSelectionInvalid)
        );
    }

    public override void RefreshUI()
    {
        _isNpcSelectionInvalid.Value = SpawnedNpcs.Count == 0 || _currentSelectedNpcIndex.Value >= SpawnedNpcs.Count;
    }

    [ModAction(ShowInUI = false)]
    public static void SpawnNewNpc(Action<HawkNetworkBehaviour> callback = null, Vector3? position = null, 
        Quaternion? rotation = null, HawkConnection owner = null, bool bUseChunkSystem = true, 
        bool bSendTransform = true, bool bCheckChunk = false, bool bMarkAsReplaced = true)
    {
        NetworkPrefab.SpawnNetworkPrefab("NPC World", callback, position, rotation, owner, bUseChunkSystem, bSendTransform, bCheckChunk, bMarkAsReplaced);
    }

    public static void AddNpcToList(GameObject npc, string name)
    {
        SpawnedNpcs.Add(npc);

        var dropdownList = _dropdownNpcItems.Value.ToList();
        dropdownList.Add(name);
        _dropdownNpcItems.Value = dropdownList.ToArray();
    }
}