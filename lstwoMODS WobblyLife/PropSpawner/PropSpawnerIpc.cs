using System.Collections;
using System.Diagnostics;
using System.Linq;
using lstwoMODS_Core;
using lstwoMODS_Core.UI;
using lstwoMODS.ImGui.Shared;
using lstwoMODS.WobblyLife.SharedObjects;
using Newtonsoft.Json;

namespace lstwoMODS_WobblyLife.PropSpawner;

/// <summary>
/// Carries the prop spawner across the process boundary: commands in from the overlay, a state
/// snapshot back out.
/// </summary>
public static class PropSpawnerIpc
{
    /// <summary>A session with more live props than this is pathological; the snapshot is cut off
    /// rather than allowed to grow without bound.</summary>
    private const int MaxSpawnedInSnapshot = 2000;

    private static bool _subscribed;
    private static bool _pumpRunning;
    private static bool _snapshotPending;
    private static int  _revision;

    /// <summary>Wired from <c>UIManager.OnInitialized</c>. May run off the main thread.</summary>
    public static void Initialize()
    {
        if (_subscribed) return;
        _subscribed = true;

        // Deliberately not IpcChannel.MessageReceived: an overlay restart replaces the channel
        // object, and a subscription against the old one is dropped without a trace, which would
        // leave every button in the panel dead until the game is restarted.
        UIManager.MessageReceived += Handle;

        MarkDirty();
    }

    /// <summary>Runs on the IPC reader thread.</summary>
    private static void Handle(IpcMessage msg)
    {
        if (msg.Type != "_plugin") return;

        try
        {
            var inner = JsonConvert.DeserializeObject<IpcMessage>(msg.Payload);
            if (inner == null) return;

            // Everything below touches the registry or Unity objects, so it has to be handed to
            // the main thread rather than run here.
            if (inner.Type == SpawnPropMessage.MessageType)
            {
                var spawn = SpawnPropMessage.Deserialize(inner);
                MainThread.Enqueue(() => PropSpawnManager.SpawnFromLibrary(spawn));
            }
            else if (inner.Type == SpawnCustomItemMessage.MessageType)
            {
                var spawn = SpawnCustomItemMessage.Deserialize(inner);
                MainThread.Enqueue(() => PropSpawnManager.SpawnCustomItem(spawn));
            }
            else if (inner.Type == SpawnedPropActionMessage.MessageType)
            {
                var action = SpawnedPropActionMessage.Deserialize(inner);
                MainThread.Enqueue(() => PropSpawnManager.HandleAction(action));
            }
            else if (inner.Type == PropGroupCommandMessage.MessageType)
            {
                // An unknown enum value from a mismatched overlay build throws here and is
                // swallowed below, which is the behaviour we want for version skew.
                var command = PropGroupCommandMessage.Deserialize(inner);
                MainThread.Enqueue(() => Execute(command));
            }
        }
        catch { }
    }

    /// <summary>Started from the main thread once the plugin is running.</summary>
    public static void StartPump()
    {
        if (_pumpRunning) return;
        _pumpRunning = true;

        PropSpawnManager.Changed  += MarkDirty;
        PropGroupManager.Changed  += MarkDirty;

        Plugin._StartCoroutine(PumpRoutine());
    }

    public static void MarkDirty() => _snapshotPending = true;


    // ── commands ─────────────────────────────────────────────────────────────

    private static void Execute(PropGroupCommandMessage command)
    {
        if (command == null) return;

        switch (command.Command)
        {
            case PropGroupCommand.Refresh:
                // Also re-scans the folder, so a shared file dropped in shows up.
                PropGroupManager.Refresh();
                break;

            case PropGroupCommand.CreateGroup:
                PropGroupManager.CreateGroup(command.Name, command.SpawnIds, command.MakeActive);
                break;

            case PropGroupCommand.RenameGroup:
                PropGroupManager.RenameGroup(command.GroupId, command.Name);
                break;

            case PropGroupCommand.DeleteGroup:
                PropGroupManager.DeleteGroup(command.GroupId, command.DeleteFile);
                break;

            case PropGroupCommand.SetActiveGroup:
                PropGroupManager.ActiveGroupId = command.GroupId;
                break;

            case PropGroupCommand.SaveGroup:
            {
                var group = PropGroupManager.Find(command.GroupId);
                if (group == null) break;
                var count = PropLayoutCapture.SaveGroupFromLive(command.GroupId);
                SendStatus(command.GroupId, count, 0, null,
                    $"Saved {count} prop(s) to \"{group.Name}\".");
                break;
            }

            case PropGroupCommand.LoadGroup:
                PropLayoutRestore.SpawnGroup(command.GroupId, new PropSpawnOptions
                {
                    Rebase          = command.LoadMode == PropGroupLoadMode.RebaseToPlayer,
                    ReplaceExisting = command.ReplaceExisting,
                });
                break;

            case PropGroupCommand.DestroyGroup:
            {
                var group = PropGroupManager.Find(command.GroupId);
                var destroyed = PropSpawnManager.DestroyGroup(command.GroupId, forceLocal: false, out var refusals);
                SendStatus(command.GroupId, destroyed, refusals.Count, refusals.ToArray(),
                    $"Destroyed {destroyed} prop(s) in \"{group?.Name ?? command.GroupId}\".");
                break;
            }

            case PropGroupCommand.DestroyAll:
            {
                var destroyed = PropSpawnManager.DestroyAll(forceLocal: false, out var refusals);
                SendStatus(null, destroyed, refusals.Count, refusals.ToArray(),
                    $"Destroyed {destroyed} spawned prop(s).");
                break;
            }

            case PropGroupCommand.AssignProps:
                PropSpawnManager.Reassign(command.SpawnIds, command.GroupId);
                break;

            case PropGroupCommand.RevealGroupFile:
                RevealFile(command.GroupId);
                break;
        }
    }

    private static void RevealFile(string groupId)
    {
        var path = PropGroupManager.GroupFilePath(groupId);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch
        {
            SendStatus(groupId, 0, 0, null, $"Group file: {path}");
        }
    }


    // ── outbound ─────────────────────────────────────────────────────────────

    public static void SendStatus(string groupId, int spawned, int failed, string[] errors, string summary = null)
    {
        if (UIManager.IpcChannel == null) return;

        UIManager.IpcChannel.SendMessage(new PropSpawnStatusMessage
        {
            GroupId = groupId,
            Spawned = spawned,
            Failed  = failed,
            Errors  = errors,
            Summary = summary,
        }.Serialize());
    }

    private static IEnumerator PumpRoutine()
    {
        while (true)
        {
            // One snapshot per frame at most. A group action changes many props at once and would
            // otherwise push a message per prop.
            if (_snapshotPending && UIManager.IpcChannel != null)
            {
                _snapshotPending = false;
                try
                {
                    SendSnapshot();
                }
                catch (System.Exception ex)
                {
                    // Losing the pump would leave the panel frozen on a stale picture forever.
                    Plugin.LogSource.LogWarning($"[PropSpawner] Snapshot failed: {ex.Message}");
                }
            }

            yield return null;
        }
        // ReSharper disable once IteratorNeverReturns
    }

    private static void SendSnapshot()
    {
        var message = new PropGroupsStateMessage
        {
            Revision      = ++_revision,
            ActiveGroupId = PropGroupManager.ActiveGroupId,
        };

        var byGroup = PropSpawnManager.LiveByGroup();

        foreach (var group in PropGroupManager.Groups)
        {
            message.Groups.Add(new PropGroupData
            {
                Id                = group.Id,
                Name              = group.Name,
                LiveCount         = byGroup.TryGetValue(group.Id, out var members) ? members.Count : 0,
                SavedCount        = group.PropCount,
                HasUnsavedChanges = PropGroupManager.IsDirty(group.Id),
                HasFile           = PropGroupManager.HasFile(group.Id),
                SavedAtUnix       = group.ModifiedUtc,
                FilePath          = PropGroupManager.DisplayFilePath(group.Id),
            });
        }

        var live = byGroup.Values.SelectMany(v => v).OrderBy(p => p.SpawnId).ToList();
        foreach (var prop in live.Take(MaxSpawnedInSnapshot))
        {
            message.Spawned.Add(new SpawnedPropData
            {
                SpawnId   = prop.SpawnId,
                Address   = prop.Source?.Describe(),
                Name      = prop.DisplayName,
                Networked = prop.Networked,
                GroupId   = prop.GroupId,
            });
        }

        message.Truncated = System.Math.Max(0, live.Count - MaxSpawnedInSnapshot);

        UIManager.IpcChannel.SendMessage(message.Serialize());
    }
}
