using System.Globalization;
using System.Linq;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Mods.Chat;

public static class ChatCommands
{
    public static void RegisterDefaults()
    {
        CommandRegistry.Register(new Command
        {
            Name = "help",
            Description = "List all available commands.",
            Scope = CommandScope.Client,
            Args =
            {
                new StringArg("[command]", optional: true),
            },
            Execute = ctx =>
            {
                if (ctx.Args.Length == 0)
                {
                    var names = string.Join(", ", CommandRegistry.All.Select(c => "/" + c.Name).OrderBy(s => s));
                    ctx.ReplyText("Commands: " + names);
                    return;
                }

                if (!CommandRegistry.TryGet(ctx.Args[0], out var c))
                {
                    ctx.Error($"Unknown command: /{ctx.Args[0]}");
                    return;
                }

                ctx.ReplyText($"{c.SignatureString} - {c.Description}");
            },
        });

        CommandRegistry.Register(new Command
        {
            Name = "clear",
            Description = "Clear the chat log.",
            Scope = CommandScope.Client,
            Execute = ctx =>
            {
                ChatMod.ClearLog();
                ChatMod.AppendSystem("(log cleared)");
            },
        });

        CommandRegistry.Register(new Command
        {
            Name = "coords",
            Description = "Print your current world position.",
            Scope = CommandScope.Client,
            Execute = ctx =>
            {
                var pc = GameInstance.InstanceExists ? GameInstance.Instance.GetFirstLocalPlayerController() : null;
                var body = pc?.GetPlayerCharacter()?.GetPlayerBody();
                if (body == null) { ctx.Error("No player character."); return; }
                var p = body.transform.position;
                ctx.ReplyText($"X={p.x:F1}  Y={p.y:F1}  Z={p.z:F1}");
            },
        });

        CommandRegistry.Register(new Command
        {
            Name = "msg",
            Description = "Send a private message to a specific player.",
            Aliases = new[] { "w", "whisper", "tell" },
            Scope = CommandScope.Client,
            Args =
            {
                new PlayerArg(),
                new GreedyStringArg("<message>"),
            },
            Execute = ctx =>
            {
                if (ctx.Args.Length < 2) { ctx.Error("Usage: /msg <player> <message>"); return; }
                var target = ChatNetworking.FindByName(ctx.Args[0]);
                if (target == null) { ctx.Error($"Player '{ctx.Args[0]}' not found."); return; }

                var text = ctx.GetRest(1);
                var me = ChatNetworking.LocalConnection();
                var senderName = me?.Name ?? "me";
                var senderId   = (me is SteamConnection sc) ? sc.steamId.Value : 0UL;

                ChatMod.AppendLocal(new ChatMessage
                {
                    Kind = ChatMessageKind.PrivateTo,
                    SenderName = senderName,
                    SenderSteamId = senderId,
                    RecipientName = target.Name,
                    Text = text,
                });

                ChatNetworking.Whisper(target, new ChatMessage
                {
                    Kind = ChatMessageKind.PrivateFrom,
                    SenderName = senderName,
                    SenderSteamId = senderId,
                    Text = text,
                });
            },
        });

        CommandRegistry.Register(new Command
        {
            Name = "players",
            Description = "List all players currently in the lobby.",
            Scope = CommandScope.Client,
            Execute = ctx =>
            {
                var names = ChatNetworking.AllConnections().Select(c => c.Name).Where(n => !string.IsNullOrEmpty(n));
                var joined = string.Join(", ", names);
                ctx.ReplyText(string.IsNullOrEmpty(joined) ? "No players." : $"Online: {joined}");
            },
        });
    }
}
