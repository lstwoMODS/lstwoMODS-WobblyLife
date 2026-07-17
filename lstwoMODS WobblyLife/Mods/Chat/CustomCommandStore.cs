using System;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using Newtonsoft.Json;
using lstwoMODS_Core;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>Trimmed, whitelist-free view of a server command sent from the host to clients over the wire.</summary>
public class SyncedServerCommand
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<CustomCommandParam> Params { get; set; } = new();
}

/// <summary>
/// Owns the set of user-defined chat commands: persists them to disk, (re)registers them into the
/// <see cref="CommandRegistry"/>, and enforces per-command whitelists for server-scoped commands.
/// Also handles host to client sync of server commands (definitions only, never whitelists) so
/// clients can see, tab-complete and forward them.
/// </summary>
public static class CustomCommandStore
{
    private const string StorageId = "lstwoMODS_WobblyLife.Mods.Chat.CustomCommandStore";
    private const string StorageKey = "CustomCommands";

    // Local, user-authored commands (persisted).
    private static readonly List<CustomChatCommand> _commands = new();
    private static readonly HashSet<string> _registered = new(StringComparer.OrdinalIgnoreCase);

    // Server commands received from the host (not persisted; live for the session only).
    private static readonly HashSet<string> _remoteRegistered = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CustomChatCommand> Commands => _commands;

    /// <summary>Fires after the local command set (or a command's config) changes and has been re-synced.</summary>
    public static event Action Changed;

    /// <summary>Load persisted commands and register them. Safe to call once during mod init.</summary>
    public static void Initialize()
    {
        _commands.Clear();

        var saved = DataStorage.Load<List<CustomChatCommand>>(StorageId, StorageKey);
        if (saved != null)
        {
            foreach (var cmd in saved)
            {
                if (cmd == null || string.IsNullOrEmpty(cmd.Name)) continue;
                cmd.Whitelist ??= new List<SteamProfile>();
                cmd.Params ??= new List<CustomCommandParam>();
                _commands.Add(cmd);
            }
        }

        SyncRegistry();
    }

    public static bool Exists(string name) =>
        _commands.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Add a new command. Returns false when the name is empty, contains whitespace, or is already
    /// taken by another custom command or a built-in (we never shadow a built-in).
    /// </summary>
    public static bool Add(string name, string description, CommandScope scope)
    {
        name = (name ?? "").Trim().TrimStart('/');
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Any(char.IsWhiteSpace)) return false;
        if (Exists(name)) return false;
        if (CommandRegistry.TryGet(name, out _)) return false;

        _commands.Add(new CustomChatCommand
        {
            Name = name,
            Description = (description ?? "").Trim(),
            Scope = scope,
        });

        Persist();
        SyncRegistry();
        return true;
    }

    public static void Remove(CustomChatCommand cmd)
    {
        if (cmd == null || !_commands.Remove(cmd)) return;
        Persist();
        SyncRegistry();
    }

    public static void SetScope(CustomChatCommand cmd, CommandScope scope)
    {
        if (cmd == null || cmd.Scope == scope) return;
        cmd.Scope = scope;
        Persist();
        SyncRegistry();
    }

    public static void SetDescription(CustomChatCommand cmd, string description)
    {
        description = (description ?? "").Trim();
        if (cmd == null || cmd.Description == description) return;
        cmd.Description = description;
        Persist();
        SyncRegistry();
    }

    public static void SetWhitelistEnabled(CustomChatCommand cmd, bool enabled)
    {
        if (cmd == null || cmd.WhitelistEnabled == enabled) return;
        cmd.WhitelistEnabled = enabled;
        Persist();
        SyncRegistry();
    }

    public static void AddParam(CustomChatCommand cmd, CustomCommandParam param)
    {
        if (cmd == null || param == null) return;
        cmd.Params.Add(param);
        Persist();
        SyncRegistry();
    }

    public static void RemoveParamAt(CustomChatCommand cmd, int index)
    {
        if (cmd == null || index < 0 || index >= cmd.Params.Count) return;
        cmd.Params.RemoveAt(index);
        Persist();
        SyncRegistry();
    }

    public static void AddToWhitelist(CustomChatCommand cmd, SteamProfile profile)
    {
        if (cmd == null || profile == null || profile.SteamId == 0) return;
        if (cmd.Whitelist.Any(p => p.SteamId == profile.SteamId)) return;

        cmd.Whitelist.Add(profile);
        Persist();
        SyncRegistry();
    }

    public static void RemoveFromWhitelist(CustomChatCommand cmd, ulong steamId)
    {
        if (cmd == null) return;
        if (cmd.Whitelist.RemoveAll(p => p.SteamId == steamId) == 0) return;

        Persist();
        SyncRegistry();
    }

    private static void Persist() => DataStorage.Save(StorageId, StorageKey, _commands);

    /// <summary>Register every local command and drop registrations that no longer exist.</summary>
    private static void SyncRegistry()
    {
        var live = new HashSet<string>(_commands.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

        // Unregister names that were renamed away or deleted since the last sync.
        foreach (var name in _registered.Where(n => !live.Contains(n)).ToList())
        {
            CommandRegistry.Unregister(name);
            _registered.Remove(name);
        }

        foreach (var cmd in _commands)
        {
            CommandRegistry.Register(BuildCommand(cmd));
            _registered.Add(cmd.Name);
        }

        Changed?.Invoke();
    }

    private static Command BuildCommand(CustomChatCommand cmd)
    {
        return new Command
        {
            Name = cmd.Name,
            Description = string.IsNullOrEmpty(cmd.Description) ? "(custom command)" : cmd.Description,
            Scope = cmd.Scope,
            Args = cmd.Params.Select(p => p.ToArg()).ToList(),
            // Server commands consult the per-command whitelist; client commands need no gate.
            ServerAllow = cmd.Scope == CommandScope.Server ? conn => IsAllowed(cmd, conn) : null,
            // No direct body: custom commands are a trigger surface. Running one fires any macro
            // bound to it via the "Chat Command" trigger (CommandRegistry.Invoked), so the command
            // stays silent on its own instead of replying with a placeholder.
            Execute = null,
        };
    }

    /// <summary>
    /// Host is always allowed. When the whitelist is disabled, everyone is allowed; otherwise the
    /// sender's Steam ID must be on the whitelist (an empty whitelist means host only).
    /// </summary>
    private static bool IsAllowed(CustomChatCommand cmd, HawkConnection conn)
    {
        if (conn == null) return false;
        if (conn.IsHost) return true;
        if (!cmd.WhitelistEnabled) return true;
        if (cmd.Whitelist.Count == 0) return false;

        var steamId = (conn is SteamConnection sc) ? sc.steamId.Value : 0UL;
        return steamId != 0 && cmd.Whitelist.Any(p => p.SteamId == steamId);
    }

    // ── Host to client sync ─────────────────────────────────────────────────────

    /// <summary>Serialize the local server commands (definitions only, no whitelists) for the wire.</summary>
    public static string GetSyncPayload()
    {
        var dtos = _commands
            .Where(c => c.Scope == CommandScope.Server)
            .Select(c => new SyncedServerCommand
            {
                Name = c.Name,
                Description = c.Description,
                Params = c.Params,
            })
            .ToList();

        try { return JsonConvert.SerializeObject(dtos); }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"[CustomCommandStore] Failed to serialize sync payload: {ex.Message}");
            return "[]";
        }
    }

    /// <summary>Register server commands received from the host, replacing any previous remote set.</summary>
    public static void ApplyRemoteServerCommands(string payload)
    {
        List<SyncedServerCommand> dtos;
        try { dtos = JsonConvert.DeserializeObject<List<SyncedServerCommand>>(payload ?? "[]") ?? new(); }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"[CustomCommandStore] Failed to parse synced commands: {ex.Message}");
            return;
        }

        ClearRemoteServerCommands();

        foreach (var dto in dtos)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Name)) continue;
            CommandRegistry.Register(BuildRemoteCommand(dto));
            _remoteRegistered.Add(dto.Name);
        }
    }

    /// <summary>Drop every host-synced command and restore any local command it shadowed.</summary>
    public static void ClearRemoteServerCommands()
    {
        if (_remoteRegistered.Count == 0) return;

        foreach (var name in _remoteRegistered)
            CommandRegistry.Unregister(name);
        _remoteRegistered.Clear();

        // Re-register local commands in case a synced one shadowed a same-named local command.
        SyncRegistry();
    }

    private static Command BuildRemoteCommand(SyncedServerCommand dto)
    {
        return new Command
        {
            Name = dto.Name,
            Description = string.IsNullOrEmpty(dto.Description) ? "(host command)" : dto.Description,
            Scope = CommandScope.Server,
            Args = (dto.Params ?? new List<CustomCommandParam>()).Select(p => p.ToArg()).ToList(),
            // Client-side: forwarded to the host (which runs it and fires its own macros). No body.
            Execute = null,
        };
    }
}
