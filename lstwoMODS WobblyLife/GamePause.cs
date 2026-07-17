using System.Collections.Generic;

namespace lstwoMODS_WobblyLife;

public static class GamePause
{
    private static readonly HashSet<object> _handles = new();

    private static bool _ownsPause;

    private static readonly object InputToken = new();

    /// <summary>True while at least one pause handle is held.</summary>
    public static bool HasHandles => _handles.Count > 0;

    public static void AddPauseHandle(object handle)
    {
        if (handle == null) return;
        if (!_handles.Add(handle)) return;
        if (_handles.Count != 1) return;

        if (IsGamePausedExternally())
        {
            _ownsPause = false;
            return;
        }

        SetPaused(true);
        _ownsPause = true;
    }

    public static void ReleasePauseHandle(object handle)
    {
        if (handle == null) return;
        if (!_handles.Remove(handle)) return;
        if (_handles.Count != 0) return;

        if (_ownsPause)
            SetPaused(false);

        _ownsPause = false;
    }

    private static bool IsGamePausedExternally()
    {
        var pauseMenu = UIPauseMenuCanvasInstance.Instance;
        return pauseMenu != null && pauseMenu.IsShowingPauseMenu();
    }

    /// <summary>
    /// Pause/unpause exactly the way the overlay UI toggle did: disable the local player's
    /// gameplay input and show the pause menu, or re-enable input and hide it.
    /// </summary>
    private static void SetPaused(bool paused)
    {
        if (!GameInstance.InstanceExists) return;

        var localPlayers = GameInstance.Instance.GetLocalPlayerControllers();

        foreach (var player in localPlayers)
        {
            var inputManager = player.GetPlayerControllerInputManager();

            if (paused)
            {
                inputManager.DisableGameplayCameraInput(InputToken);
                inputManager.DisableGameplayInput(InputToken);
                inputManager.DisableInteratorInput(InputToken);
                inputManager.DisablePlayerTransformInput(InputToken);
                inputManager.DisableUIInput(InputToken);

                UIPauseMenuCanvasInstance.Instance?.ShowPauseMenu(player);

                return;
            }

            inputManager.EnableGameplayCameraInput(InputToken);
            inputManager.EnableGameplayInput(InputToken);
            inputManager.EnableInteratorInput(InputToken);
            inputManager.EnablePlayerTransformInput(InputToken);
            inputManager.EnableUIInput(InputToken);

            UIPauseMenuCanvasInstance.Instance?.HidePauseMenu(player);
        }
    }
}
