using System;
using System.Collections.Generic;
using HawkNetworking;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.FakePlayer;

public static class FakePlayerManager
{
    public const string PlayerCharacterPrefabAddress = "Game/Prefabs/Player/Characters/PlayerCharacter.prefab";

    public static List<FakePlayerInstance> ActiveFakePlayers { get; } = new();

    public static event Action<FakePlayerInstance> Spawned;
    public static event Action<FakePlayerInstance> Despawned;

    public static bool CanSpawn => HawkNetworkManager.DefaultInstance != null && HawkNetworkManager.DefaultInstance.IsServer();

    public static void Spawn(bool withController, PlayerCharacter clothingSource, Vector3? position, string name, Action<FakePlayerInstance> onSpawned = null)
    {
        if (!CanSpawn)
        {
            Plugin.LogSource.LogWarning("[FakePlayer] Spawn called but not server, only the host can spawn fake players.");
            onSpawned?.Invoke(null);
            return;
        }

        if (withController) SpawnWithController(clothingSource, position, name, onSpawned);
        else SpawnCharacterOnly(clothingSource, position, name, onSpawned);
    }

    private static void SpawnWithController(PlayerCharacter clothingSource, Vector3? position, string name, Action<FakePlayerInstance> onSpawned)
    {
        var prefab = UnitySingleton<PersistentContentManager>.Instance.GetDefaultPlayerControllerPrefab();
        if (prefab == null)
        {
            Plugin.LogSource.LogError("[FakePlayer] No PlayerController prefab available.");
            onSpawned?.Invoke(null);
            return;
        }

        var localConnection = HawkNetworkManager.DefaultInstance.GetMe();

        HawkNetworkManager.DefaultInstance.InstantiateNetworkPrefab(prefab.gameObject, b =>
        {
            var controller = b as PlayerController;
            if (controller == null) { onSpawned?.Invoke(null); return; }

            controller.gameObject.AddComponent<FakePlayerMarker>();

            controller.onGameplayCameraCreated += (_, cam) => DisableCameraPair(cam);

            controller.SetLocalPlayerid(0);
            if (!string.IsNullOrEmpty(name)) controller.SetServerPlayerName(name);

            controller.ServerSpawnPlayerCharacter(null, character =>
            {
                if (character == null) { onSpawned?.Invoke(null); return; }

                character.gameObject.AddComponent<FakePlayerMarker>();

                if (position.HasValue) character.SetPlayerPosition(position.Value);

                var fake = new FakePlayerInstance(controller, character, name);
                controller.GetComponent<FakePlayerMarker>().Instance = fake;
                character.GetComponent<FakePlayerMarker>().Instance = fake;

                DisableCameraPair(controller.GetGameplayCamera());

                if (clothingSource != null) fake.CopyClothingFrom(clothingSource);

                ActiveFakePlayers.Add(fake);
                Spawned?.Invoke(fake);
                onSpawned?.Invoke(fake);

            }, position, null, false);

        }, position, null, localConnection);
    }

    private static void SpawnCharacterOnly(PlayerCharacter clothingSource, Vector3? position, string name, Action<FakePlayerInstance> onSpawned)
    {
        var localConnection = HawkNetworkManager.DefaultInstance.GetMe();

        NetworkPrefab.SpawnNetworkPrefab(PlayerCharacterPrefabAddress, b =>
        {
            var character = b as PlayerCharacter;
            if (character == null) { onSpawned?.Invoke(null); return; }

            character.gameObject.AddComponent<FakePlayerMarker>();

            if (position.HasValue) character.SetPlayerPosition(position.Value);

            var fake = new FakePlayerInstance(null, character, name);
            character.GetComponent<FakePlayerMarker>().Instance = fake;

            if (clothingSource != null) fake.CopyClothingFrom(clothingSource);

            ActiveFakePlayers.Add(fake);
            Spawned?.Invoke(fake);
            onSpawned?.Invoke(fake);
        }, position, null, localConnection, true, true);
    }

    public static void Despawn(FakePlayerInstance fake)
    {
        if (fake == null || !ActiveFakePlayers.Contains(fake)) return;

        fake.DespawnInternal();
        ActiveFakePlayers.Remove(fake);
        Despawned?.Invoke(fake);
    }

    public static void DespawnAll()
    {
        for (var i = ActiveFakePlayers.Count - 1; i >= 0; i--) Despawn(ActiveFakePlayers[i]);
    }

    public static FakePlayerInstance GetFakePlayer(PlayerController controller)
    {
        if (controller == null) return null;
        var marker = controller.GetComponent<FakePlayerMarker>();
        return marker != null ? marker.Instance : null;
    }

    public static FakePlayerInstance GetFakePlayer(PlayerCharacter character)
    {
        if (character == null) return null;
        var marker = character.GetComponent<FakePlayerMarker>();
        return marker != null ? marker.Instance : null;
    }

    public static bool IsFake(PlayerController controller) => controller != null && controller.GetComponent<FakePlayerMarker>() != null;
    public static bool IsFake(PlayerCharacter character) => character != null && character.GetComponent<FakePlayerMarker>() != null;

    internal static void SetCameraPairActive(GameplayCamera cam, bool active)
    {
        if (cam == null) return;
        cam.gameObject.SetActive(active);
        var uiCam = cam.GetUICamera();
        if (uiCam != null) uiCam.gameObject.SetActive(active);
    }

    internal static void DisableCameraPair(GameplayCamera cam) => SetCameraPairActive(cam, false);
}
