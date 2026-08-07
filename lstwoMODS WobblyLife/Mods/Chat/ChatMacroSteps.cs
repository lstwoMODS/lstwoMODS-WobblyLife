using lstwoMODS_Core.Macros;
using lstwoMODS_WobblyLife.UI.TabMenus;

namespace lstwoMODS_WobblyLife.Mods.Chat;

/// <summary>
/// Registers the chat mod's actions as custom macro steps under the "Chat" category, so a macro can
/// drive the chat the same way a player does: send a global message, show a local-only notice, whisper
/// a specific player, or run any chat command.
///
/// These are plain <see cref="MacroMethodDescriptor"/> registrations rather than <c>[ModAction]</c>s:
/// it lets each step declare a typed, well-labelled parameter (notably a <see cref="PlayerRef"/> for the
/// whisper target, which reuses the Local/Host/By Name selection modes from <see cref="PlayerMacroType"/>),
/// matching how the job managers and player steps already expose their actions.
///
/// Steps run on the main thread inside the macro runner's coroutine, so they call straight into
/// <see cref="ChatMod"/> without marshaling.
/// </summary>
public static class ChatMacroSteps
{
    private const string Category = "Chat";

    /// <summary>Register the steps. Call once at startup (see Plugin.Start).</summary>
    public static void Register()
    {
        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id          = "chat.send",
            Label       = "Send Chat Message",
            Category    = Category,
            PickerLabel = "Send Chat Message",
            Parameters  = new[] { new MacroParam { Name = "Message", Type = typeof(string) } },
            Execute     = args =>
            {
                ChatMod.SendChat(AsString(args[0]));
                return null;
            },
        });

        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id          = "chat.local",
            Label       = "Show Local Message",
            Category    = Category,
            PickerLabel = "Show Local Message",
            Parameters  = new[]
            {
                new MacroParam { Name = "Message",        Type = typeof(string) },
                // Enum params auto-render as a combo of every ChatMessageKind, so the message can be
                // styled as any of the chat's own types (System, Error, Player Say, Private, ...).
                new MacroParam { Name = "Message Type",   Type = typeof(ChatMessageKind) },
                // Only meaningful for the kinds that render a name: Sender for Player Say / Private From,
                // Recipient for Private To. Ignored (blank) by System / Error / Command Reply, etc.
                new MacroParam { Name = "Sender Name",    Type = typeof(string) },
                new MacroParam { Name = "Recipient Name", Type = typeof(string) },
            },
            Execute = args =>
            {
                // Local-only: build any chat line, styled by its kind, and append it to this client's
                // log. Never networked, so sender/recipient are purely cosmetic for the local render.
                ChatMod.AppendLocal(new ChatMessage
                {
                    Text          = AsString(args[0]),
                    Kind          = AsKind(args[1]),
                    SenderName    = AsString(args[2]) ?? "",
                    RecipientName = AsString(args[3]) ?? "",
                });
                return null;
            },
        });

        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id          = "chat.whisper",
            Label       = "Whisper to Player",
            Category    = Category,
            PickerLabel = "Whisper to Player",
            ReturnType  = typeof(bool), // true when the recipient was found and the whisper was sent
            Parameters  = new[]
            {
                new MacroParam { Name = "Player",  Type = typeof(PlayerRef) },
                new MacroParam { Name = "Message", Type = typeof(string) },
            },
            Execute = args =>
            {
                var name = (args[0] as PlayerRef)?.Controller?.GetPlayerName();
                return !string.IsNullOrEmpty(name) && ChatMod.SendWhisper(name, AsString(args[1]));
            },
        });

        MacroRegistry.Register(new MacroMethodDescriptor
        {
            Id          = "chat.runCommand",
            Label       = "Run Chat Command",
            Category    = Category,
            PickerLabel = "Run Chat Command",
            Parameters  = new[]
            {
                new MacroParam { Name = "Command", Type = typeof(string) }, // e.g. "coords" or "msg Bob hi"
            },
            Execute = args =>
            {
                ChatMod.RunCommand(AsString(args[0]));
                return null;
            },
        });
    }

    private static string AsString(object arg) => (string)MacroValues.Coerce(arg, typeof(string));

    private static ChatMessageKind AsKind(object arg) => (ChatMessageKind)MacroValues.Coerce(arg, typeof(ChatMessageKind));
}
