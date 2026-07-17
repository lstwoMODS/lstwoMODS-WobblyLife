using System;
using System.Collections.Generic;
using System.Linq;
using HawkNetworking;
using lstwoMODS.ImGui.Shared;
using lstwoMODS.ImGui.Shared.UI;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public class ChatMod : BaseMod
{
    public override string Name => "Chat";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;

    [ModSetting] public static Ref<bool> Enabled = new();
    [ModSetting] public static Ref<int> MaxHistory = new(200);
    [ModSetting] public static Ref<int> HistorySize = new(50);

    private const int LogCapacity = 100;
    private const float BackgroundAlpha = 0.55f;
    private const float RowBackgroundAlpha = 0.6f;
    private const float MessageVisibleSec = 7f;
    private const float MessageFadeSec = 1.5f;


    private static readonly List<ChatMessage> _log = new();
    private static readonly List<string> _commandHistory = new();
    private static int _historyCursor = -1;
    private static string _historyDraft = "";

    private static bool _chatOpen;
    private static int _openedFrame = -1;


    private static readonly Ref<string> _input = new("");
    private static readonly Ref<bool> _chatRender = new();

    private static InputText _inputBox;
    private static ChildWindow _logChild;
    private static GuiWindow _chatWindow;
    private static PushStyleColorAlphaCommand _chatBgAlphaCmd;
    private static PushStyleColorAlphaCommand _chatBorderAlphaCmd;

    private static ChildWindow[] _logRows;
    private static TextColored[] _logRowTexts;
    private static PushStyleColorCommand[] _logRowBgCmds;
    private static float[] _logRowAlphas;

    protected override void OnStaticInit()
    {
        // Persist the chat settings: load the saved value on startup and auto-save on change.
        // Defaults mirror the field initializers above so a fresh install behaves unchanged.
        BindData(Enabled,     nameof(Enabled),     false);
        BindData(MaxHistory,  nameof(MaxHistory),  200);
        BindData(HistorySize, nameof(HistorySize), 50);

        ChatNetworking.Initialize();

        ChatNetworking.MessageReceived += OnMessageReceived;
        ChatNetworking.ServerCommandRequested += OnServerCommandRequested;
        ChatNetworking.SyncRequested += OnSyncRequested;
        ChatNetworking.SyncCommandsReceived += OnSyncCommandsReceived;

        ChatCommands.RegisterDefaults();
        CustomCommandStore.Initialize();

        // When the host edits its command set, push the new definitions to all clients.
        CustomCommandStore.Changed += () =>
        {
            if (ChatNetworking.IsHost() && ChatNetworking.IsOnline())
                ChatNetworking.BroadcastServerCommands(CustomCommandStore.GetSyncPayload());
        };
    }

    // Host: a client asked for the current server-command definitions, reply to just that client.
    private static void OnSyncRequested(HawkConnection sender)
    {
        if (!ChatNetworking.IsHost()) return;
        ChatNetworking.SendServerCommandsTo(sender, CustomCommandStore.GetSyncPayload());
    }

    // Client: the host pushed its server-command definitions, register them locally.
    private static void OnSyncCommandsReceived(string payload)
    {
        if (ChatNetworking.IsHost()) return; // host owns the authoritative set
        CustomCommandStore.ApplyRemoteServerCommands(payload);
    }


    public static void BuildChatUI()
    {
        var window = lstwoMODS_Core.Plugin.Window;

        _logRows      = new ChildWindow[LogCapacity];
        _logRowTexts  = new TextColored[LogCapacity];
        _logRowBgCmds = new PushStyleColorCommand[LogCapacity];
        _logRowAlphas = new float[LogCapacity];
        for (var i = 0; i < LogCapacity; i++)
        {
            var text = new TextColored($"chat-row-text-{i}", "", 1f, 1f, 1f, 1f)
                .WithRequireInput(false);

            var bgCmd = new PushStyleColorCommand { Col = ImGuiCol.ChildBg, R = 0.04f, G = 0.04f, B = 0.05f, A = 0f };

            var row = new ChildWindow($"chat-row-{i}", 0f, 0f, text)
                .WithFlags(ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding)
                .WithWindowFlags(ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings)
                .WithStyleVar(ImGuiStyleVar.ChildRounding, 5f)
                .WithStyleVar(ImGuiStyleVar.WindowPadding, 7f, 3f)
                .WithRequireInput(false);
            row.Data.PushCommands.Add(bgCmd);
            row.Data.Enabled = false;

            _logRows[i]      = row;
            _logRowTexts[i]  = text;
            _logRowBgCmds[i] = bgCmd;
        }

        _logChild = new ChildWindow("chat-log", 0f, 200f, _logRows.Cast<BaseUIElement>().ToArray())
            .WithWindowFlags(ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar)
            .WithRequireInput(false);

        _inputBox = new InputText("##chat-input", "", "Enter a message, / for commands", 512, flags: ImGuiInputTextFlags.EnterReturnsTrue)
            .WithValue(_input)
            .WatchKeys(ImGuiKey.Enter, ImGuiKey.KeypadEnter, ImGuiKey.UpArrow, ImGuiKey.DownArrow, ImGuiKey.Escape)
            .OnKey(OnSpecialKey)
            .WithItemWidth(-1f)
            .WithRequireInput(true);
        _inputBox.Data.Enabled = false;

        _chatBgAlphaCmd = new PushStyleColorAlphaCommand { Col = ImGuiCol.WindowBg, A = 0f };
        _chatBorderAlphaCmd = new PushStyleColorAlphaCommand { Col = ImGuiCol.Border, A = 0f };

        _chatWindow = new GuiWindow("chat-window", "Chat", _logChild, _inputBox)
            .WithFlags(
                ImGuiWindowFlags.NoTitleBar       |
                ImGuiWindowFlags.NoResize         |
                ImGuiWindowFlags.NoMove           |
                ImGuiWindowFlags.NoCollapse       |
                ImGuiWindowFlags.NoDocking        |
                ImGuiWindowFlags.NoSavedSettings  |
                ImGuiWindowFlags.NoBringToFrontOnFocus)
            .WithSize(520f, 250f, ImGuiCond.Always)
            .WithOpen(_chatRender)
            .WithNoClose()
            .PinToMainViewport()
            .WithRequireInput(RequireInputMode.Inherit);
        _chatWindow.Data.PushCommands.Add(_chatBgAlphaCmd);
        _chatWindow.Data.PushCommands.Add(_chatBorderAlphaCmd);

        window.AddElement(_chatWindow);

        window.HotkeyManager.Register("chat.open", "Open Chat", KeyCode.T, HotkeyModifiers.Ctrl, ToggleChat);
    }


    public static void AppendLocal(ChatMessage msg)
    {
        if (msg == null) return;
        Plugin.LogSource.LogDebug($"[Chat] Message Received: {msg.Kind.ToString()} from '{msg.SenderName}' (Steam ID: {msg.SenderSteamId})");
        Plugin.LogSource.LogInfo($"[Chat] {msg.ReceivedAt.Hour}:{msg.ReceivedAt.Minute}:{msg.ReceivedAt.Second}: {msg.Render()}");
        
        _log.Add(msg);

        if (_log.Count > MaxHistory.Value)
        {
            _log.RemoveAt(0);
        }
        
        RefreshLogRows();
        _logChild?.ScrollToBottom();
    }

    public static void AppendSystem(string text) => AppendLocal(new ChatMessage { Kind = ChatMessageKind.System, Text = text });
    public static void AppendError(string text)  => AppendLocal(new ChatMessage { Kind = ChatMessageKind.Error,  Text = text });

    public static void ClearLog()
    {
        _log.Clear();
        RefreshLogRows();
    }


    private static void ToggleChat()
    {
        if (!Enabled.Value) return;
        if (_chatOpen) CloseChat();
        else OpenChat();
    }
    
    private static object _pauseHandle = new();

    private static void OpenChat()
    {
        if (!Enabled.Value) return;
        if (_chatOpen) return;
        if (!GameInstance.Instance.GetGamemode()) return;

        _chatOpen = true;
        _openedFrame = Time.frameCount;

        if (_inputBox != null)
        {
            _inputBox.Data.Enabled = true;
            _inputBox.MarkChanged();
            _inputBox.FocusNextFrame();
        }

        _logChild?.ScrollToBottom();

        GamePause.AddPauseHandle(_pauseHandle);
        lstwoMODS_Core.Plugin.Window?.FocusOverlayWindow();
    }

    private static void OpenChatWithSlash()
    {
        if (!Enabled.Value) return;
        if (string.IsNullOrEmpty(_input.Value))
            _input.Value = "/";
        OpenChat();
    }

    private static void CloseChat()
    {
        _chatOpen = false;
        _input.Value = "";
        _historyCursor = -1;
        _historyDraft = "";
        
        if (_inputBox != null)
        {
            _inputBox.Data.Enabled = false;
            _inputBox.MarkChanged();
        }
        
        GamePause.ReleasePauseHandle(_pauseHandle);
        lstwoMODS_Core.Plugin.Window?.FocusGameWindow();
    }


    private static void OnSpecialKey(ImGuiKey key)
    {
        switch (key)
        {
            case ImGuiKey.UpArrow:
                NavigateHistory(direction: -1);
                break;

            case ImGuiKey.DownArrow:
                NavigateHistory(direction: +1);
                break;

            case ImGuiKey.Escape:
                CloseChat();
                break;

            case ImGuiKey.Enter:
            case ImGuiKey.KeypadEnter:
                Submit(_input.Value);
                break;
        }
    }

    private static bool _syncRequested;

    public override void Update()
    {
        var anyRowVisible = UpdateLogRowAlphas();
        UpdateChatWindowVisuals(anyRowVisible);
        UpdateCommandSync();
    }

    // Client-side: request the host's server commands once we're connected; drop them when we leave.
    private static void UpdateCommandSync()
    {
        if (!ChatNetworking.IsOnline())
        {
            if (_syncRequested)
            {
                _syncRequested = false;
                CustomCommandStore.ClearRemoteServerCommands();
            }
            return;
        }

        // The host authors the set locally; it never requests a sync.
        if (ChatNetworking.IsHost()) return;

        if (!_syncRequested && ChatNetworking.IsReady())
        {
            ChatNetworking.RequestServerCommandSync();
            _syncRequested = true;
        }
    }

    private static void UpdateChatWindowVisuals(bool anyRowVisible)
    {
        if (_chatWindow == null) return;

        var shouldRender = _chatOpen || anyRowVisible;

        if (_chatRender.Value != shouldRender)
            _chatRender.Value = shouldRender;

        if (!shouldRender) return;

        var d = (WindowData)_chatWindow.Data;
        d.NextPosX = 20f;
        d.NextPosY = -20f;
        d.PivotX = 0f;
        d.PivotY = 1f;
        d.PosCond = ImGuiCond.Always;

        if (_chatBgAlphaCmd != null)
        {
            var desiredBgAlpha = _chatOpen ? BackgroundAlpha : 0f;
            if (Math.Abs(_chatBgAlphaCmd.A - desiredBgAlpha) > 0.001f)
                _chatBgAlphaCmd.A = desiredBgAlpha;
        }

        if (_chatBorderAlphaCmd != null)
        {
            var desiredBorderAlpha = _chatOpen ? 1f : 0f;
            if (Math.Abs(_chatBorderAlphaCmd.A - desiredBorderAlpha) > 0.001f)
                _chatBorderAlphaCmd.A = desiredBorderAlpha;
        }

        _chatWindow.MarkChanged();
    }

    private static bool UpdateLogRowAlphas()
    {
        if (_logRows == null) return false;

        var start = Math.Max(0, _log.Count - LogCapacity);
        var visibleCount = Math.Min(_log.Count, LogCapacity);
        var now = DateTime.UtcNow;
        var anyVisible = false;

        for (var i = 0; i < LogCapacity; i++)
        {
            var row  = _logRows[i];
            var text = _logRowTexts[i];
            float targetAlpha;
            bool targetEnabled;

            if (i < visibleCount)
            {
                var msg = _log[start + i];
                targetAlpha = ComputeRowAlpha(msg.ReceivedAt, now);
                targetEnabled = targetAlpha > 0f;
            }
            else
            {
                targetAlpha = 0f;
                targetEnabled = false;
            }

            if (targetEnabled) anyVisible = true;

            var textData = (TextData)text.Data;
            var targetBgAlpha = _chatOpen ? 0f : targetAlpha * RowBackgroundAlpha;

            var alphaChanged   = Math.Abs(_logRowAlphas[i] - targetAlpha) > 0.005f;
            var enabledChanged = row.Data.Enabled != targetEnabled;
            var bgChanged      = Math.Abs(_logRowBgCmds[i].A - targetBgAlpha) > 0.005f;

            if (alphaChanged || enabledChanged || bgChanged)
            {
                _logRowAlphas[i]   = targetAlpha;
                textData.A         = targetAlpha;
                _logRowBgCmds[i].A = targetBgAlpha;
                row.Data.Enabled   = targetEnabled;
                text.MarkChanged();
                row.MarkChanged();
            }
        }

        return anyVisible;
    }

    private static float ComputeRowAlpha(DateTime receivedAt, DateTime now)
    {
        if (_chatOpen) return 1f;
        var age = (float)(now - receivedAt).TotalSeconds;
        if (age <= MessageVisibleSec) return 1f;
        var fade = (age - MessageVisibleSec) / MessageFadeSec;
        if (fade >= 1f) return 0f;
        return 1f - fade;
    }

    private static void Submit(string raw)
    {
        if (Time.frameCount - _openedFrame <= 1) return;

        if (!string.IsNullOrEmpty(raw))
        {
            if (_commandHistory.LastOrDefault() != raw)
            {
                _commandHistory.Add(raw);
                while (_commandHistory.Count > HistorySize.Value)
                    _commandHistory.RemoveAt(0);
            }
        }

        if (string.IsNullOrEmpty(raw))
        {
            CloseChat();
            return;
        }

        if (raw.StartsWith("/"))
            ExecuteCommand(raw);
        else
            SendChat(raw);

        CloseChat();
    }

    private static void SendChat(string text)
    {
        var me = ChatNetworking.LocalConnection();
        var name = me?.Name ?? "me";
        var steamId = (me is SteamConnection sc) ? sc.steamId.Value : 0UL;

        AppendLocal(new ChatMessage
        {
            Kind = ChatMessageKind.PlayerSay,
            SenderName = name,
            SenderSteamId = steamId,
            Text = text,
        });

        if (ChatNetworking.IsOnline())
        {
            ChatNetworking.Broadcast(new ChatMessage
            {
                Kind = ChatMessageKind.PlayerSay,
                SenderName = name,
                SenderSteamId = steamId,
                Text = text,
            });
        }
    }

    private static void ExecuteCommand(string line)
    {
        AppendLocal(new ChatMessage { Kind = ChatMessageKind.CommandEcho, Text = line });

        var body = line.Substring(1);
        var tokens = CommandTokenizer.Tokenize(body);
        if (tokens.Length == 0)
        {
            AppendError("Empty command.");
            return;
        }

        if (!CommandRegistry.TryGet(tokens[0], out var cmd))
        {
            AppendError($"Unknown command: /{tokens[0]}");
            return;
        }

        var ctx = new CommandContext
        {
            Args = CommandTokenizer.BuildExecArgs(body, cmd),
            Sender = ChatNetworking.LocalConnection(),
            IsRemote = false,
            IsHost = ChatNetworking.IsHost(),
            Reply = msg => AppendLocal(msg),
        };

        switch (cmd.Scope)
        {
            case CommandScope.Client:
                SafeInvoke(cmd, ctx);
                break;

            case CommandScope.Server:
                if (ctx.IsHost || !ChatNetworking.IsOnline())
                    SafeInvoke(cmd, ctx);
                else
                    ChatNetworking.SendServerCommand(line);
                break;
        }
    }

    private static void SafeInvoke(Command cmd, CommandContext ctx)
    {
        try
        {
            cmd.Execute?.Invoke(ctx);
            // Let macro triggers react to the command having run (see ChatMacroTrigger).
            CommandRegistry.NotifyInvoked(cmd, ctx);
        }
        catch (Exception ex)
        {
            AppendError($"/{cmd.Name}: {ex.Message}");
            Debug.LogError($"[ChatMod] Command /{cmd.Name} failed: {ex}");
        }
    }


    private static void OnServerCommandRequested(HawkConnection sender, string line)
    {
        if (!ChatNetworking.IsHost()) return;
        if (string.IsNullOrEmpty(line) || !line.StartsWith("/")) return;

        var body = line.Substring(1);
        var tokens = CommandTokenizer.Tokenize(body);
        if (tokens.Length == 0) return;

        if (!CommandRegistry.TryGet(tokens[0], out var cmd)) return;
        if (cmd.Scope == CommandScope.Client) return;
        
        Plugin.LogSource.LogInfo($"[Chat] Command requested from '{sender.Name}': {line}");

        // Default (null ServerAllow) = host/owner only; commands opt in to public use with a predicate.
        var allowed = cmd.ServerAllow != null ? cmd.ServerAllow(sender) : sender.IsHost;
        if (!allowed)
        {
            ChatNetworking.Whisper(sender, new ChatMessage
            {
                Kind = ChatMessageKind.Error,
                Text = $"You are not permitted to run /{cmd.Name}.",
            });
            return;
        }

        var ctx = new CommandContext
        {
            Args = CommandTokenizer.BuildExecArgs(body, cmd),
            Sender = sender,
            IsRemote = true,
            IsHost = true,
            Reply = msg =>
            {
                ChatNetworking.Whisper(sender, msg);
                AppendLocal(msg);
            },
        };

        SafeInvoke(cmd, ctx);
    }

    private static void OnMessageReceived(ChatMessage msg)
    {
        if (!Enabled.Value) return;
        AppendLocal(msg);
    }


    private static void NavigateHistory(int direction)
    {
        if (_commandHistory.Count == 0) return;

        if (_historyCursor == -1)
        {
            _historyDraft = _input.Value ?? "";
            _historyCursor = _commandHistory.Count;
        }

        _historyCursor = Math.Max(0, Math.Min(_commandHistory.Count, _historyCursor + direction));
        _input.Value = _historyCursor >= _commandHistory.Count ? _historyDraft : _commandHistory[_historyCursor];
    }


    private static void RefreshLogRows()
    {
        if (_logRows == null) return;

        var start = Math.Max(0, _log.Count - LogCapacity);
        var visibleCount = Math.Min(_log.Count, LogCapacity);

        for (var i = 0; i < LogCapacity; i++)
        {
            var text = _logRowTexts[i];
            var data = (TextData)text.Data;
            
            if (i < visibleCount)
            {
                var message = _log[start + i];
                var messageColor = message.Color;
                
                data.Text = message.Render();
                data.R = messageColor.r; data.G = messageColor.g; data.B = messageColor.b;
            }
            else
            {
                data.Text = "";
            }
            
            text.MarkChanged();
        }
    }


    // ── Custom command manager (mods-window panel) ──────────────────────────────

    private static readonly Ref<string> _newName  = new("");
    private static readonly Ref<string> _newDesc  = new("");
    private static readonly Ref<int>    _newScope = new();          // 0 = Client, 1 = Server
    private static readonly Ref<string> _createStatus = new("");

    private static readonly Ref<string[]> _cmdItems       = new(Array.Empty<string>());
    private static readonly Ref<int>      _selectedCmd    = new();
    private static readonly Ref<int>      _selectedScope  = new();
    private static readonly Ref<string>   _editDesc       = new("");
    private static readonly Ref<bool>     _whitelistEnabled = new(true);
    // Enforce-whitelist checkbox is only meaningful for server commands.
    private static readonly Ref<bool>     _notServer      = new(true);
    private static readonly Ref<string>   _detailText     = new("");

    // Parameters of the selected command.
    private static readonly Ref<string[]> _paramItems     = new(Array.Empty<string>());
    private static readonly Ref<int>      _paramIndex     = new();
    private static readonly Ref<bool>     _noSelection    = new(true);
    private static readonly Ref<string>   _newParamHint   = new("");
    private static readonly Ref<int>      _newParamType   = new();
    private static readonly Ref<bool>     _newParamOptional = new();
    private static readonly Ref<string>   _newParamEnum   = new("");

    private static readonly Ref<string[]> _lobbyItems     = new(Array.Empty<string>());
    private static readonly Ref<int>      _lobbyIndex     = new();
    private static readonly Ref<string[]> _whitelistItems = new(Array.Empty<string>());
    private static readonly Ref<int>      _whitelistIndex = new();
    // Whitelist player controls apply only to server commands that enforce their whitelist.
    private static readonly Ref<bool>     _whitelistDisabled = new(true);

    // Index-parallel to _lobbyItems: the profile each lobby entry maps to.
    private static readonly List<SteamProfile> _lobbyProfiles = new();

    private static readonly string[] ScopeNames = { "Client", "Server" };
    private static readonly string[] ParamTypeNames = { "String", "Int", "Float", "Player", "Enum", "Greedy" };

    public override Container BuildPanel(string id)
    {
        RefreshCommandLists();

        return new Container(id,

            base.BuildPanel(id),

            new SeparatorText("cc-create-sep", "Create Command"),

            new InputText("Command Name", "", "name (no leading slash, no spaces)", 64).WithValue(_newName),
            new InputText("Description", "", "optional description", 256).WithValue(_newDesc),
            new Combo("Scope", ScopeNames).WithSelectedIndex(_newScope),
            new Button("Create Command", CreateCommand).WithContentWidth(),
            new TextDisabled("cc-create-status", "").WithText(_createStatus),

            new SeparatorText("cc-manage-sep", "Manage Commands"),

            new Combo("Command", Array.Empty<string>(), onChanged: _ => RefreshCommandLists())
                .WithItems(_cmdItems).WithSelectedIndex(_selectedCmd),

            new Combo("Selected Scope", ScopeNames, onChanged: OnSelectedScopeChanged)
                .WithSelectedIndex(_selectedScope).WithDisabled(_noSelection),

            new HStack("cc-edit-desc",
                new InputText("###cc-edit-desc-input", "", "description", 256).WithValue(_editDesc),
                new Button("Save Description", ApplySelectedDescription)
            ).WithContentWidth().WithDisabled(_noSelection),

            new HStack("cc-manage-actions",
                new Button("Create Macro", CreateMacroForSelectedCommand),
                new Button("Delete Command", DeleteSelectedCommand)
            ).WithContentWidth().WithDisabled(_noSelection),

            new TextWrapped("cc-detail", "").WithText(_detailText),

            new SeparatorText("cc-params-sep", "Parameters"),

            new Combo("Parameters", Array.Empty<string>()).WithItems(_paramItems).WithSelectedIndex(_paramIndex).WithDisabled(_noSelection),
            new Button("Remove Parameter", RemoveSelectedParam).WithContentWidth().WithDisabled(_noSelection),

            new Spacing("cc-params-spacer"),

            new InputText("Param Hint", "", "e.g. <player> or [amount]", 64).WithValue(_newParamHint),
            new Combo("Param Type", ParamTypeNames).WithSelectedIndex(_newParamType),
            new Checkbox("Optional Param", false).WithValue(_newParamOptional),
            new InputText("Enum Values", "", "comma-separated (Enum type only)", 256).WithValue(_newParamEnum),
            new Button("Add Parameter", AddParamToSelected).WithContentWidth().WithDisabled(_noSelection),

            new SeparatorText("cc-wl-sep", "Server Whitelist"),

            new Checkbox("Enforce Whitelist", true, onChanged: OnWhitelistEnabledChanged).WithValue(_whitelistEnabled).WithDisabled(_notServer),

            new Combo("Add Player", Array.Empty<string>()).WithItems(_lobbyItems).WithSelectedIndex(_lobbyIndex).WithDisabled(_whitelistDisabled),
            new Button("Add to Whitelist", AddSelectedPlayerToWhitelist).WithContentWidth().WithDisabled(_whitelistDisabled),

            new Spacing("cc-wl-spacer"),

            new Combo("Whitelisted", Array.Empty<string>()).WithItems(_whitelistItems).WithSelectedIndex(_whitelistIndex).WithDisabled(_whitelistDisabled),
            new Button("Remove from Whitelist", RemoveSelectedPlayerFromWhitelist).WithContentWidth().WithDisabled(_whitelistDisabled)
        );
    }

    public override void RefreshUI()
    {
        base.RefreshUI();
        RefreshCommandLists();
    }

    private static CustomChatCommand SelectedCommand()
    {
        var cmds = CustomCommandStore.Commands;
        var i = _selectedCmd.Value;
        return i >= 0 && i < cmds.Count ? cmds[i] : null;
    }

    private static void CreateCommand()
    {
        var scope = _newScope.Value == 1 ? CommandScope.Server : CommandScope.Client;

        if (CustomCommandStore.Add(_newName.Value, _newDesc.Value, scope))
        {
            _createStatus.Value = $"Created /{_newName.Value.Trim().TrimStart('/')}";
            _newName.Value = "";
            _newDesc.Value = "";
            _selectedCmd.Value = CustomCommandStore.Commands.Count - 1;
            RefreshCommandLists();
        }
        else
        {
            _createStatus.Value = "Invalid or already-used command name.";
        }
    }

    private static void DeleteSelectedCommand()
    {
        var sel = SelectedCommand();
        if (sel == null) return;
        CustomCommandStore.Remove(sel);
        RefreshCommandLists();
    }

    // Auto-create a macro triggered by the selected command and jump to the Macros window.
    private static void CreateMacroForSelectedCommand()
    {
        var sel = SelectedCommand();
        if (sel == null) return;
        ChatMacroTrigger.CreateMacroForCommand(sel.Name);
    }

    private static void OnSelectedScopeChanged(int index)
    {
        var sel = SelectedCommand();
        if (sel == null) return;
        CustomCommandStore.SetScope(sel, index == 1 ? CommandScope.Server : CommandScope.Client);
        RefreshCommandLists();
    }

    private static void ApplySelectedDescription()
    {
        var sel = SelectedCommand();
        if (sel == null) return;
        CustomCommandStore.SetDescription(sel, _editDesc.Value);
        RefreshCommandLists();
    }

    private static void OnWhitelistEnabledChanged(bool enabled)
    {
        var sel = SelectedCommand();
        if (sel == null) return;
        CustomCommandStore.SetWhitelistEnabled(sel, enabled);
        RefreshCommandLists();
    }

    private static void AddParamToSelected()
    {
        var sel = SelectedCommand();
        if (sel == null) return;

        var type = (CustomParamType)_newParamType.Value;
        var hint = string.IsNullOrWhiteSpace(_newParamHint.Value) ? DefaultHint(type) : _newParamHint.Value.Trim();

        CustomCommandStore.AddParam(sel, new CustomCommandParam
        {
            Hint = hint,
            Type = type,
            Optional = _newParamOptional.Value,
            EnumValues = type == CustomParamType.Enum
                ? _newParamEnum.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
                : new List<string>(),
        });

        _newParamHint.Value = "";
        _newParamEnum.Value = "";
        RefreshCommandLists();
    }

    private static void RemoveSelectedParam()
    {
        var sel = SelectedCommand();
        if (sel == null) return;
        CustomCommandStore.RemoveParamAt(sel, _paramIndex.Value);
        RefreshCommandLists();
    }

    private static string DefaultHint(CustomParamType type) => type switch
    {
        CustomParamType.Player => "<player>",
        CustomParamType.Int    => "<number>",
        CustomParamType.Float  => "<number>",
        CustomParamType.Greedy => "<text...>",
        _                      => "<arg>",
    };

    private static void AddSelectedPlayerToWhitelist()
    {
        var sel = SelectedCommand();
        var i = _lobbyIndex.Value;
        if (sel == null || i < 0 || i >= _lobbyProfiles.Count) return;
        CustomCommandStore.AddToWhitelist(sel, _lobbyProfiles[i]);
        RefreshCommandLists();
    }

    private static void RemoveSelectedPlayerFromWhitelist()
    {
        var sel = SelectedCommand();
        var i = _whitelistIndex.Value;
        if (sel == null || i < 0 || i >= sel.Whitelist.Count) return;
        CustomCommandStore.RemoveFromWhitelist(sel, sel.Whitelist[i].SteamId);
        RefreshCommandLists();
    }

    /// <summary>Repopulate every command/whitelist dropdown from the store's current state.</summary>
    private static void RefreshCommandLists()
    {
        var cmds = CustomCommandStore.Commands;

        _cmdItems.Value = cmds.Select(c => $"/{c.Name}  [{c.Scope}]").ToArray();
        if (_selectedCmd.Value >= cmds.Count)
            _selectedCmd.Value = Math.Max(0, cmds.Count - 1);

        var sel = SelectedCommand();
        var isServer = sel != null && sel.Scope == CommandScope.Server;

        _noSelection.Value = sel == null;
        _selectedScope.Value = isServer ? 1 : 0;
        _editDesc.Value = sel?.Description ?? "";
        _whitelistEnabled.Value = sel?.WhitelistEnabled ?? true;
        _notServer.Value = !isServer;
        // Player add/remove only matters for a server command that actually enforces its whitelist.
        _whitelistDisabled.Value = !isServer || !(sel?.WhitelistEnabled ?? true);

        // Parameters of the selected command.
        _paramItems.Value = sel != null ? sel.Params.Select(FormatParam).ToArray() : Array.Empty<string>();
        if (sel != null && _paramIndex.Value >= sel.Params.Count)
            _paramIndex.Value = Math.Max(0, sel.Params.Count - 1);

        // Lobby players eligible to add (SteamConnections not already on the whitelist).
        _lobbyProfiles.Clear();
        if (sel != null)
        {
            foreach (var conn in ChatNetworking.AllConnections().OfType<SteamConnection>())
            {
                var profile = SteamProfileHelper.FromConnection(conn);
                if (profile == null || profile.SteamId == 0) continue;
                if (sel.Whitelist.Any(p => p.SteamId == profile.SteamId)) continue;
                if (_lobbyProfiles.Any(p => p.SteamId == profile.SteamId)) continue;
                _lobbyProfiles.Add(profile);
            }
        }

        _lobbyItems.Value = _lobbyProfiles.Select(p => p.SteamName).ToArray();
        if (_lobbyIndex.Value >= _lobbyProfiles.Count)
            _lobbyIndex.Value = Math.Max(0, _lobbyProfiles.Count - 1);

        _whitelistItems.Value = sel != null ? sel.Whitelist.Select(p => p.SteamName).ToArray() : Array.Empty<string>();
        if (sel != null && _whitelistIndex.Value >= sel.Whitelist.Count)
            _whitelistIndex.Value = Math.Max(0, sel.Whitelist.Count - 1);

        _detailText.Value = BuildDetailText(sel);
    }

    private static string FormatParam(CustomCommandParam p)
    {
        var suffix = p.Type == CustomParamType.Enum && p.EnumValues.Count > 0
            ? $"{p.Type}: {string.Join("/", p.EnumValues)}"
            : p.Type.ToString();
        return $"{p.Hint}  [{suffix}{(p.Optional ? ", optional" : "")}]";
    }

    private static string BuildDetailText(CustomChatCommand cmd)
    {
        if (cmd == null)
            return "No custom commands yet. Create one above.";

        var sig = "/" + cmd.Name + (cmd.Params.Count > 0 ? " " + string.Join(" ", cmd.Params.Select(p => p.Hint)) : "");

        if (cmd.Scope == CommandScope.Client)
            return $"{sig} runs locally on the client. Server whitelist options below apply to server commands only.";

        if (!cmd.WhitelistEnabled)
            return $"{sig} runs on the host. Whitelist disabled, everyone may run it.";

        return cmd.Whitelist.Count == 0
            ? $"{sig} runs on the host. Whitelist empty, host only may run it."
            : $"{sig} runs on the host. Allowed: host + {cmd.Whitelist.Count} whitelisted player(s).";
    }
}
