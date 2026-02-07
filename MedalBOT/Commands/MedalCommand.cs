using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MedalBot.Commands
{
    public class MedalCommand : ICommand
    {
        private const string AdminsFile = "admins.txt";
        // Medal mapping: text -> code
        private static readonly Dictionary<string, int> MedalMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "platinum", 1 },
            { "gold",     2 },
            { "silver",   3 }
        };

        public string Name => "medal";

        public (bool handled, string response) Process(string senderNick, string message, string fullLine)
        {
            if (string.IsNullOrWhiteSpace(message))
                return (false, null);

            // Ignore anything before first '!' (fixes ♥10 etc.)
            int bang = message.IndexOf('!');
            if (bang >= 0)
                message = message.Substring(bang);

            var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return (false, null);

            string cmd = parts[0].ToLowerInvariant();

            if (cmd == "!medallist")
            {
                return (true, BuildMedalList());
            }

            // admin-only commands
            if (cmd != "!medal" && cmd != "!unmedal")
                return (false, null);

            if (!IsAdmin(senderNick))
                return (true, $"⚠️ {senderNick}: only admins can use this command.");

            if (cmd == "!unmedal")
                return HandleUnmedal(parts, senderNick);

            // cmd == !medal
            return HandleMedal(parts, senderNick);
        }

        private (bool, string) HandleMedal(string[] parts, string senderNick)
        {
            if (parts.Length < 3)
                return (true, "Usage: !medal <nick> <Platinum|Gold|Silver>");

            string targetNick = parts[1];
            string medalType = parts[2];

            if (!MedalMap.TryGetValue(medalType, out int medalValue))
                return (true, "Invalid medal type. Use: Platinum, Gold, or Silver.");

            // Get hostmask from in-memory map
            if (!Program.CurrentHostmasks.TryGetValue(targetNick, out string hostmask) || string.IsNullOrWhiteSpace(hostmask))
                return (true, $"⚠️ Could not find hostmask for {targetNick}. Ask them to send a message or rejoin.");

            string ident = ExtractIdent(hostmask);
            if (string.IsNullOrEmpty(ident))
                return (true, "⚠️ Could not extract ident from hostmask.");

            // Save ident -> medalValue using Program's shared dictionary
            Program.VoicedUsers[ident] = medalValue;
            Program.SaveVoicedData();

            // Voice via ChanServ (GameSurge)
            Program.Writer?.WriteLine($"PRIVMSG ChanServ :voice {Program.Channel} {targetNick}");
            Console.WriteLine($"[Medal] Saved {ident} => {medalValue} and voiced {targetNick}");

            string emoji = medalValue switch
            {
                1 => "🏆",
                2 => "🥇",
                3 => "🥈",
                _ => "🎖️"
            };

            return (true, $"✅ {senderNick} awarded {medalType.ToUpper()} {emoji} to {targetNick}.");
        }

        private (bool, string) HandleUnmedal(string[] parts, string senderNick)
        {
            if (parts.Length < 2)
                return (true, "Usage: !unmedal <nick>");

            string targetNick = parts[1];

            if (!Program.CurrentHostmasks.TryGetValue(targetNick, out string hostmask) || string.IsNullOrWhiteSpace(hostmask))
                return (true, $"⚠️ Could not find hostmask for {targetNick}.");

            string ident = ExtractIdent(hostmask);
            if (string.IsNullOrEmpty(ident))
                return (true, "⚠️ Could not extract ident from hostmask.");

            if (!Program.VoicedUsers.Remove(ident))
                return (true, $"⚠️ {targetNick} had no recorded medal.");

            Program.SaveVoicedData();
            Program.Writer?.WriteLine($"PRIVMSG ChanServ :devoice {Program.Channel} {targetNick}");
            Console.WriteLine($"[Medal] Removed {ident} and de-voiced {targetNick}");

            return (true, $"❌ {senderNick} removed the medal from {targetNick}.");
        }

        private string BuildMedalList()
        {
            if (Program.VoicedUsers == null || Program.VoicedUsers.Count == 0)
                return "No medalled players yet.";

            // Map numeric back to name
            var items = Program.VoicedUsers.Select(kv =>
            {
                string name = kv.Value switch
                {
                    1 => "Platinum",
                    2 => "Gold",
                    3 => "Silver",
                    _ => "Unknown"
                };
                return $"{kv.Key}({name})";
            });

            return $"🏅 Medallist: {string.Join(", ", items)}";
        }

        private static string ExtractIdent(string hostmask)
        {
            if (string.IsNullOrWhiteSpace(hostmask)) return null;
            int at = hostmask.IndexOf('@');
            if (at <= 0) return hostmask;
            return hostmask.Substring(0, at);
        }

        private static bool IsAdmin(string nick)
        {
            if (!File.Exists(AdminsFile)) return false;
            var admins = File.ReadAllLines(AdminsFile)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => l.ToLowerInvariant())
                .ToHashSet();
            return admins.Contains(nick.ToLowerInvariant());
        }
    }
}