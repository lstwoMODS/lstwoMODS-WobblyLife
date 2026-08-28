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

namespace lstwoMODS_WobblyLife.Mods.Chat;

public class ChatMod : BaseMod
{
    public override string Name => "Chat";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.ExtraModsWindow;

    /// <summary>
    /// Master switch for the mod. Off means the chat takes no part in the session at all: nothing is
    /// appended or rendered locally (<see cref="AppendLocal"/> is the single gate for that), nothing is
    /// sent, and a host neither relays whispers nor runs server commands on a client's behalf. Chat
    /// already requires a modded host, so a host opting out simply switches the feature off for the lobby.
    /// </summary>
    [ModSetting(Order = 10)] public static Ref<bool> Enabled = new();
    [ModSetting(Order = 20)] public static Ref<int> MaxHistory = new(200);
    [ModSetting(Order = 30)] public static Ref<int> HistorySize = new(50);
    [ModSetting(Order = 40)] public static Ref<bool> ShowMessagesAsSpeechBubbles = new();

    public static readonly Ref<bool>  UseCustomNameColor = new(false);
    public static readonly Ref<Col> CustomNameColor    = new(Color.white);

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
    private static TextColored[] _logRowNameTexts;
    private static SameLine[] _logRowSameLines;
    private static PushStyleColorCommand[] _logRowBgCmds;
    private static float[] _logRowAlphas;

    protected override void OnStaticInit()
    {
        BindData(Enabled, nameof(Enabled), false);
        BindData(MaxHistory, nameof(MaxHistory), 200);
        BindData(HistorySize, nameof(HistorySize), 50);
        BindData(ShowMessagesAsSpeechBubbles, nameof(ShowMessagesAsSpeechBubbles), false);
        BindData(UseCustomNameColor, nameof(UseCustomNameColor), false);
        BindData(CustomNameColor, nameof(CustomNameColor), Color.white);

        ChatNetworking.Initialize();

        ChatNetworking.MessageReceived += OnMessageReceived;
        ChatNetworking.ServerCommandRequested += OnServerCommandRequested;
        ChatNetworking.SyncRequested += OnSyncRequested;
        ChatNetworking.SyncCommandsReceived += OnSyncCommandsReceived;
        ChatNetworking.WhisperRequested += OnWhisperRequested;

        ChatCommands.RegisterDefaults();
        CustomCommandStore.Initialize();

        CustomCommandStore.Changed += () =>
        {
            if (ChatNetworking.IsHost() && ChatNetworking.IsOnline())
                ChatNetworking.BroadcastServerCommands(CustomCommandStore.GetSyncPayload());
        };
    }

    private static void OnSyncRequested(HawkConnection sender)
    {
        if (!ChatNetworking.IsHost()) return;
        ChatNetworking.SendServerCommandsTo(sender, CustomCommandStore.GetSyncPayload());
    }

    private static void OnWhisperRequested(HawkConnection sender, string recipientName, string text)
    {
        if (!Enabled.Value) return;
        if (!ChatNetworking.IsHost()) return;
        DeliverWhisperFromHost(sender, recipientName, text);
    }

    public static bool DeliverWhisperFromHost(HawkConnection sender, string recipientName, string text)
    {
        var target = ChatNetworking.FindByName(recipientName);
        if (target == null) return false;

        var msg = ChatNetworking.BuildInbound(ChatMessageKind.PrivateFrom, sender, recipientName: "", text: text);

        if (target.Me) AppendLocal (msg);            // the host is the recipient
        else ChatNetworking.Whisper(target, msg);   // relay to the recipient's client
        return true;
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

        _logRows         = new ChildWindow[LogCapacity];
        _logRowTexts     = new TextColored[LogCapacity];
        _logRowNameTexts = new TextColored[LogCapacity];
        _logRowSameLines = new SameLine[LogCapacity];
        _logRowBgCmds    = new PushStyleColorCommand[LogCapacity];
        _logRowAlphas    = new float[LogCapacity];
        for (var i = 0; i < LogCapacity; i++)
        {
            var nameText = new TextColored($"chat-row-name-{i}", "", 1f, 1f, 1f, 1f)
                .WithRequireInput(false);
            nameText.Data.Enabled = false;

            var sameLine = new SameLine($"chat-row-sameline-{i}", 0f, 0f)
                .WithRequireInput(false);
            sameLine.Data.Enabled = false;

            var text = new TextColored($"chat-row-text-{i}", "", 1f, 1f, 1f, 1f)
                .WithRequireInput(false);

            var bgCmd = new PushStyleColorCommand { Col = ImGuiCol.ChildBg, R = 0.04f, G = 0.04f, B = 0.05f, A = 0f };

            var row = new ChildWindow($"chat-row-{i}", 0f, 0f, nameText, sameLine, text)
                .WithFlags(ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.AlwaysUseWindowPadding)
                .WithWindowFlags(ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings)
                .WithStyleVar(ImGuiStyleVar.ChildRounding, 5f)
                .WithStyleVar(ImGuiStyleVar.WindowPadding, 7f, 3f)
                .WithRequireInput(false);
            row.Data.PushCommands.Add(bgCmd);
            row.Data.Enabled = false;

            _logRows[i]         = row;
            _logRowTexts[i]     = text;
            _logRowNameTexts[i] = nameText;
            _logRowSameLines[i] = sameLine;
            _logRowBgCmds[i]    = bgCmd;
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


    /// <summary>Put a message in the log. Every line the user ever sees passes through here, whether it
    /// came off the wire, from a command, from a macro, or is our own echo, so this is where
    /// <see cref="Enabled"/> gates display.</summary>
    public static void AppendLocal(ChatMessage msg)
    {
        if (msg == null) return;

        Plugin.LogSource.LogDebug($"[Chat] Message Received: {msg.Kind.ToString()} from '{msg.SenderName}' ({msg.SenderIdentity()})");

        if (!Enabled.Value) return;

        Plugin.LogSource.LogInfo($"[Chat] {msg.ReceivedAt.ToLocalTime():HH:mm:ss}: {msg.Render()}");

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
        _netStatus?.Tick();
    }

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

        // Switching the mod off mid-session closes anything still on screen; without this the window
        // lingers showing whatever was in the log, since only appends are gated.
        if (!Enabled.Value && _chatOpen) CloseChat();

        var shouldRender = Enabled.Value && (_chatOpen || anyRowVisible);

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
            var nameData = (TextData)_logRowNameTexts[i].Data;
            var targetBgAlpha = _chatOpen ? 0f : targetAlpha * RowBackgroundAlpha;

            var alphaChanged   = Math.Abs(_logRowAlphas[i] - targetAlpha) > 0.005f;
            var enabledChanged = row.Data.Enabled != targetEnabled;
            var bgChanged      = Math.Abs(_logRowBgCmds[i].A - targetBgAlpha) > 0.005f;

            if (alphaChanged || enabledChanged || bgChanged)
            {
                _logRowAlphas[i]   = targetAlpha;
                textData.A         = targetAlpha;
                nameData.A         = targetAlpha;
                _logRowBgCmds[i].A = targetBgAlpha;
                row.Data.Enabled   = targetEnabled;
                text.MarkChanged();
                _logRowNameTexts[i].MarkChanged();
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

    /// <summary>
    /// Send a normal (global) chat message to everyone: echo it locally and broadcast it when online.
    /// This is what typing a plain line into the chat box does; also the "Send Chat Message" macro step.
    /// </summary>
    public static void SendChat(string text)
    {
        if (!Enabled.Value) return;

        var me = ChatNetworking.LocalConnection();
        var name = me?.Name ?? "me";
        var senderKey = ChatNetworking.KeyOf(me);
        var address = ChatNetworking.AddressOf(me) ?? "";
        var networkId = ChatNetworking.LocalNetworkId();
        var nameColor = EffectiveLocalNameColor(senderKey, name);

        text = ChatNetworking.SanitizeText(text);

        var msg = new ChatMessage
        {
            Kind = ChatMessageKind.PlayerSay,
            SenderName = name,
            SenderKey = senderKey,
            SenderNetworkId = networkId,
            SenderAddress = address,
            Text = text,
            NameColor = nameColor,
        };

        AppendLocal(msg);
        ShowMessageAsSpeechBubble(msg);
        
        if (!ChatNetworking.IsOnline()) return;
        if (!WarnIfNotConnected()) return;

        ChatNetworking.Broadcast(new ChatMessage
        {
            Kind = ChatMessageKind.PlayerSay,
            SenderName = name,
            SenderKey = senderKey,
            SenderNetworkId = networkId,
            Text = text,
            NameColor = nameColor,
        });
    }

    /// <summary>
    /// True when the chat's network object exists and messages can actually leave this machine. The
    /// send helpers are all null-conditional on <c>ChatNetworkManager.Instance</c>, so without this a
    /// message sent while the object is missing (host not running the mod, or it has not spawned yet)
    /// silently goes nowhere while still echoing locally, which reads as "my chat is broken".
    /// </summary>
    private static bool WarnIfNotConnected()
    {
        if (ChatNetworking.IsReady()) return true;

        AppendError("Not sent: chat is not connected. The host needs the mod loaded and the session joined.");
        Plugin.LogSource.LogWarning("[Chat] Send skipped: ChatNetworkManager.Instance is null on this machine.");
        return false;
    }

    /// <summary>Our own name color for an outgoing message: the saved custom color when enabled,
    /// otherwise the stable auto color derived from our identity (same one everyone else derives).</summary>
    private static Color EffectiveLocalNameColor(PlayerKey key, string name)
        => UseCustomNameColor.Value ? CustomNameColor.Value : NameColorUtil.AutoColor(key, name);

    /// <summary>
    /// Host: send an unattributed line, the way command output reads, to one player or (when
    /// <paramref name="target"/> is null) to everyone including ourselves. Unlike <see cref="SendChat"/>
    /// this carries no sender name and renders with the kind's own styling.
    ///
    /// Host only by design: a receiving client only trusts these kinds from the host connection and
    /// coerces a peer's copy to normal chat, so system output cannot be forged (see
    /// <c>ChatNetworkManager.SanitizeKind</c>). Returns false when we are not the host, when the text
    /// is empty, or when nothing could be sent.
    /// </summary>
    public static bool SendHostNotice(HawkConnection target, string text,
                                      ChatMessageKind kind = ChatMessageKind.CommandReply)
    {
        if (!Enabled.Value) return false;
        if (!ChatNetworking.IsHost()) return false;

        text = ChatNetworking.SanitizeText(text);
        if (string.IsNullOrEmpty(text)) return false;

        var msg = new ChatMessage { Kind = HostNoticeKind(kind), Text = text };

        // To everyone: we are part of "everyone", and SendBroadcast only reaches the others.
        if (target == null)
        {
            AppendLocal(msg);
            if (!ChatNetworking.IsOnline()) return true;
            if (!WarnIfNotConnected()) return false;
            ChatNetworking.Broadcast(msg);
            return true;
        }

        if (target.Me)
        {
            AppendLocal(msg);
            return true;
        }

        if (!WarnIfNotConnected()) return false;
        ChatNetworking.Whisper(target, msg);
        return true;
    }

    /// <summary>The kinds a host notice may claim: those a client accepts from the host and that render
    /// without a sender name. Anything else falls back to plain command output rather than arriving as
    /// normal chat, which is what the receiving side would otherwise coerce it to.</summary>
    private static ChatMessageKind HostNoticeKind(ChatMessageKind kind) => kind switch
    {
        ChatMessageKind.System or ChatMessageKind.Error or ChatMessageKind.CommandReply => kind,
        _ => ChatMessageKind.CommandReply,
    };

    /// <summary>
    /// Send a private message to the player named <paramref name="recipientName"/>: echo it locally and
    /// route it to the recipient (delivered directly when we are the host, otherwise relayed through the
    /// host, since a client can't reach another client). Returns false when no such player is in the game.
    /// Shared body of the <c>/msg</c> command and the "Whisper to Player" macro step.
    /// </summary>
    public static bool SendWhisper(string recipientName, string text)
    {
        if (!Enabled.Value) return false;

        // Resolve against the replicated roster (present on every client), so it works for non-hosts too.
        var targetName = ChatNetworking.AllPlayerNames()
            .FirstOrDefault(n => string.Equals(n, recipientName, StringComparison.OrdinalIgnoreCase));
        if (targetName == null) return false;

        var me = ChatNetworking.LocalConnection();
        var senderName = me?.Name ?? "me";
        var senderKey  = ChatNetworking.KeyOf(me);

        // Same reason as SendChat: the echo has to match what the recipient will see.
        text = ChatNetworking.SanitizeText(text);

        // Local echo of our outgoing message (correct kind/name; no network round-trip).
        AppendLocal(new ChatMessage
        {
            Kind = ChatMessageKind.PrivateTo,
            SenderName = senderName,
            SenderKey = senderKey,
            SenderNetworkId = ChatNetworking.LocalNetworkId(),
            SenderAddress = ChatNetworking.AddressOf(me) ?? "",
            RecipientName = targetName,
            Text = text,
        });

        // Only the host can reach the recipient's connection, so deliver there: directly when we are the
        // host, otherwise relay the request through the host. Whispering yourself offline is fine and
        // needs no network object, so only the paths that actually leave the machine are checked.
        if (ChatNetworking.IsHost())
        {
            var target = ChatNetworking.FindByName(targetName);
            if (target != null && !target.Me && !WarnIfNotConnected()) return false;
            DeliverWhisperFromHost(me, targetName, text);
        }
        else
        {
            if (!WarnIfNotConnected()) return false;
            ChatNetworking.SendWhisperRelay(targetName, text);
        }
        return true;
    }

    /// <summary>
    /// Run a chat command line through the normal client/server dispatch. The leading slash is optional
    /// ("coords" and "/coords" behave identically). Shared entry point for the command box and the
    /// "Run Chat Command" macro step, so both route through <see cref="ExecuteCommand"/>.
    /// </summary>
    public static void RunCommand(string line)
    {
        if (!Enabled.Value) return;
        if (string.IsNullOrWhiteSpace(line)) return;
        line = line.Trim();
        if (!line.StartsWith("/")) line = "/" + line;
        ExecuteCommand(line);
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
            Plugin.LogSource.LogError($"[ChatMod] Command /{cmd.Name} failed: {ex}");
        }
    }


    private static void OnServerCommandRequested(HawkConnection sender, string line)
    {
        if (!Enabled.Value) return;
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
        ShowMessageAsSpeechBubble(msg);
    }

    private static void ShowMessageAsSpeechBubble(ChatMessage msg)
    {
        if (!ShowMessagesAsSpeechBubbles.Value || msg.Kind != ChatMessageKind.PlayerSay) return;
        
        var speaker = ChatNetworking.FindSender(msg);

        if (speaker == null) return;
        
        var dialogMod = new NPCDialog();
        dialogMod.Player = speaker;
        dialogMod.ShowSpeechBubble(msg.Text);
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
            var text     = _logRowTexts[i];
            var nameText = _logRowNameTexts[i];
            var sameLine = _logRowSameLines[i];
            var data     = (TextData)text.Data;
            var nameData = (TextData)nameText.Data;

            if (i < visibleCount)
            {
                var message = _log[start + i];

                if (message.HasColoredName)
                {
                    // Name in the sender's color, then ": message" in the kind color, on one line.
                    var nameColor = message.NameColor.Value;
                    nameData.Text = message.RenderName();
                    nameData.R = nameColor.r; nameData.G = nameColor.g; nameData.B = nameColor.b;

                    var bodyColor = message.Color;
                    data.Text = message.RenderBody();
                    data.R = bodyColor.r; data.G = bodyColor.g; data.B = bodyColor.b;

                    nameText.Data.Enabled = true;
                    sameLine.Data.Enabled = true;
                }
                else
                {
                    var messageColor = message.Color;
                    data.Text = message.Render();
                    data.R = messageColor.r; data.G = messageColor.g; data.B = messageColor.b;

                    nameData.Text = "";
                    nameText.Data.Enabled = false;
                    sameLine.Data.Enabled = false;
                }
            }
            else
            {
                data.Text = "";
                nameData.Text = "";
                nameText.Data.Enabled = false;
                sameLine.Data.Enabled = false;
            }

            text.MarkChanged();
            nameText.MarkChanged();
            sameLine.MarkChanged();
        }
    }


    // ── Custom command manager (mods-window panel) ──────────────────────────────

    // Custom name-color picker is only interactive when the custom-color toggle is on.
    private static readonly Ref<bool> _nameColorDisabled = new(true);

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
    private static readonly List<PlayerProfile> _lobbyProfiles = new();

    private static readonly string[] ScopeNames = { "Client", "Server" };
    private static readonly string[] ParamTypeNames = { "String", "Int", "Float", "Player", "Enum", "Greedy" };

    // Chat rides its own Hawk channel, so it only reaches other players when the host is modded too.
    private static ModNetworkIndicator _netStatus;

    public override Container BuildPanel(string id)
    {
        RefreshCommandLists();
        _nameColorDisabled.Value = !UseCustomNameColor.Value;

        _netStatus = new ModNetworkIndicator("chat-net-status", ChatNetworking.IsReady);

        return new Container(id,

            _netStatus,

            new UIText("info", "When enabled, press Ctrl + T in game to open / close."),

            base.BuildPanel(id),

            new SeparatorText("cc-namecolor-sep", "Name Color"),

            new TextWrapped("cc-namecolor-info",
                "Everyone gets a random name color by default. Turn on a custom color to pick your own; " +
                "it saves and shows on your name for everyone in chat."),
            new Checkbox("Use Custom Name Color", false, onChanged: e => _nameColorDisabled.Value = !e)
                .WithValue(UseCustomNameColor),
            new ColorEdit3("Name Color").WithValue(CustomNameColor).WithDisabled(_nameColorDisabled),

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
        CustomCommandStore.RemoveFromWhitelist(sel, sel.Whitelist[i].Key);
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

        // Lobby players eligible to add: anyone with an account identity who is not on the list yet.
        // On a LAN transport nobody has one, so the list stays empty rather than offering entries a
        // whitelist check could never match.
        _lobbyProfiles.Clear();
        if (sel != null)
        {
            foreach (var conn in ChatNetworking.AllConnections())
            {
                var profile = PlayerProfileHelper.FromConnection(conn);
                if (profile == null || !profile.Key.IsValid) continue;
                if (sel.Whitelist.Any(p => p.Key == profile.Key)) continue;
                if (_lobbyProfiles.Any(p => p.Key == profile.Key)) continue;
                _lobbyProfiles.Add(profile);
            }
        }

        _lobbyItems.Value = _lobbyProfiles.Select(p => p.Name).ToArray();
        if (_lobbyIndex.Value >= _lobbyProfiles.Count)
            _lobbyIndex.Value = Math.Max(0, _lobbyProfiles.Count - 1);

        _whitelistItems.Value = sel != null ? sel.Whitelist.Select(p => p.Name).ToArray() : Array.Empty<string>();
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
