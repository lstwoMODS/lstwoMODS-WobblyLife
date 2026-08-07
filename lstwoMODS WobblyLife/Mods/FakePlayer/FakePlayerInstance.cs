using System;
using Rewired;

namespace lstwoMODS_WobblyLife.Mods.FakePlayer;

public class FakePlayerInstance
{
    public PlayerController Controller { get; }
    public PlayerCharacter Character { get; }
    public bool HasController => Controller != null;
    public string Name { get; set; }

    public bool IsBeingControlled => ControllingSource != null;
    public PlayerController ControllingSource { get; private set; }

    public Func<FakePlayerInstance, PlayerInput?> InputProvider { get; set; }
    public bool IsScripted => InputProvider != null && ControllingSource == null;

    private Player _savedSourceRewiredPlayer;
    private CameraFocus _savedSourceCameraFocus;

    internal FakePlayerInstance(PlayerController controller, PlayerCharacter character, string name)
    {
        Controller = controller;
        Character = character;
        Name = name;
    }

    public void TakeControl(PlayerController source)
    {
        if (source == null || Character == null || IsBeingControlled) return;

        var sourceCamera = source.GetGameplayCamera();
        if (sourceCamera == null) return;

        var sourceInput = source.GetPlayerControllerInputManager();
        var rewiredPlayer = sourceInput?.player;
        var sourceCharacter = source.GetPlayerCharacter();

        ControllingSource = source;
        _savedSourceRewiredPlayer = rewiredPlayer;
        _savedSourceCameraFocus = sourceCamera.cameraFocus;

        if (sourceCharacter != null) sourceCharacter.player = null;

        if (HasController)
        {
            var fakeInput = Controller.GetPlayerControllerInputManager();
            var fakeCamera = Controller.GetGameplayCamera();

            if (fakeInput != null) fakeInput.player = rewiredPlayer;
            Character.player = rewiredPlayer;
            if (fakeCamera != null) fakeCamera.player = rewiredPlayer;

            if (sourceInput != null) sourceInput.player = null;

            FakePlayerManager.SetCameraPairActive(sourceCamera, false);
            FakePlayerManager.SetCameraPairActive(fakeCamera, true);
        }
        else
        {
            var fakeFocus = Character.cameraFocus ?? Character.GetComponent<CameraFocus>();
            if (Character.cameraFocus == null && fakeFocus != null) Character.cameraFocus = fakeFocus;

            Character.player = rewiredPlayer;
            Character.gameplayCamera = sourceCamera;

            if (fakeFocus != null) sourceCamera.SetCameraFocus(fakeFocus);
        }
    }

    public void ReleaseControl()
    {
        if (!IsBeingControlled) return;

        var source = ControllingSource;
        var sourceInput = source != null ? source.GetPlayerControllerInputManager() : null;
        var sourceCamera = source != null ? source.GetGameplayCamera() : null;
        var sourceCharacter = source != null ? source.GetPlayerCharacter() : null;

        if (HasController)
        {
            var fakeInput = Controller.GetPlayerControllerInputManager();
            var fakeCamera = Controller.GetGameplayCamera();

            if (fakeInput != null) fakeInput.player = null;
            Character.player = null;
            if (fakeCamera != null) fakeCamera.player = null;

            if (sourceInput != null) sourceInput.player = _savedSourceRewiredPlayer;

            FakePlayerManager.SetCameraPairActive(fakeCamera, false);
            FakePlayerManager.SetCameraPairActive(sourceCamera, true);
        }
        else
        {
            Character.player = null;
            Character.gameplayCamera = null;

            if (sourceCamera != null) sourceCamera.SetCameraFocus(_savedSourceCameraFocus);
        }

        if (sourceCharacter != null && _savedSourceRewiredPlayer != null)
        {
            sourceCharacter.player = _savedSourceRewiredPlayer;
        }

        ControllingSource = null;
        _savedSourceRewiredPlayer = null;
        _savedSourceCameraFocus = null;
    }

    public void StartScriptedControl(Func<FakePlayerInstance, PlayerInput?> provider)
    {
        if (provider == null) return;
        if (IsBeingControlled) ReleaseControl();
        InputProvider = provider;
    }

    public void StopScriptedControl()
    {
        InputProvider = null;
    }

    public void CopyClothingFrom(PlayerCharacter source)
    {
        if (source == null || Character == null) return;

        var sourceCustomize = source.GetPlayerCharacterCustomize();
        var fakeCustomize = Character.GetPlayerCharacterCustomize();
        if (sourceCustomize == null || fakeCustomize == null) return;

        var refs = ClothingManager.Instance.allClothingAssetReferences;

        if (sourceCustomize.GetClothingHat())
        {
            fakeCustomize.SetClothingPiece(
                refs.GetClothing(sourceCustomize.GetClothingHat().GetGuid()),
                ClothingSelectionType.Hat,
                data: new ClothingPieceData { clothingPrimaryColor = sourceCustomize.GetClothingHat().GetPrimaryColor() });
        }
        if (sourceCustomize.GetClothingTop())
        {
            fakeCustomize.SetClothingPiece(
                refs.GetClothing(sourceCustomize.GetClothingTop().GetGuid()),
                ClothingSelectionType.Top,
                data: new ClothingPieceData { clothingPrimaryColor = sourceCustomize.GetClothingTop().GetPrimaryColor() });
        }
        if (sourceCustomize.GetClothingBottom())
        {
            fakeCustomize.SetClothingPiece(
                refs.GetClothing(sourceCustomize.GetClothingBottom().GetGuid()),
                ClothingSelectionType.Bottom,
                data: new ClothingPieceData { clothingPrimaryColor = sourceCustomize.GetClothingBottom().GetPrimaryColor() });
        }

        fakeCustomize.SetCharacterColor(sourceCustomize.GetCharacterColor(), false, 0f);
    }

    internal void DespawnInternal()
    {
        if (IsBeingControlled) ReleaseControl();

        if (Controller != null && Controller.networkObject != null)
        {
            Controller.networkObject.Destroy();
        }
        else if (Character != null && Character.networkObject != null)
        {
            Character.networkObject.Destroy();
        }
    }
}
