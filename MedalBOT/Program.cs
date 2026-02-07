using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MedalBot.Commands;

namespace MedalBot
{
    class Program
    {
        private static TcpClient irc;
        private static StreamReader reader;
        private static StreamWriter writer;

        private static readonly string server = "irc.gamesurge.net";
        private static readonly int port = 6667;
        private static readonly string nick = "";
        private static readonly string user = "";
        private static readonly string pass = "";
        private static readonly string channel = "";
        private static readonly string channelPass = "";

        private static readonly string adminsFile = "admins.txt";
        private static readonly string voicedFile = "voiced.txt";
        private static readonly string messagesFile = "messages.txt";

        private static HashSet<string> admins = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, int> voicedUsers = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> currentHostmasks = new(StringComparer.OrdinalIgnoreCase);
        private static List<(string Message, int Interval)> scheduledMessages = new();

        private static CommandManager commandManager = new();

        static void Main()
        {
            LoadAdmins();
            LoadVoiced();
            LoadMessages();

            Console.WriteLine("Working dir: " + Environment.CurrentDirectory);
            Console.WriteLine("Loaded admins: " + string.Join(", ", admins));

            irc = new TcpClient(server, port);
            reader = new StreamReader(irc.GetStream());
            writer = new StreamWriter(irc.GetStream()) { AutoFlush = true };

            Console.WriteLine("Connected to IRC server.");

            writer.WriteLine($"NICK {nick}");
            writer.WriteLine($"USER {user} 8 * :{user}");

            bool authed = false;
            bool joined = false;

            Task.Run(MessageScheduler);

            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) continue;

                Console.WriteLine(line);

                if (line.StartsWith("PING"))
                {
                    writer.WriteLine($"PONG {line.Split(' ')[1]}");
                    continue;
                }

                if (line.Contains(" 001 "))
                {
                    Thread.Sleep(1000);
                    writer.WriteLine($"PRIVMSG AuthServ@Services.GameSurge.net :AUTH {user} {pass}");
                    Console.WriteLine("Sent AuthServ authentication...");
                    authed = true;
                    continue;
                }

                if (authed && !joined && line.Contains("is now your hidden host"))
                {
                    Thread.Sleep(1500);
                    writer.WriteLine($"JOIN {channel} {channelPass}");
                    Console.WriteLine($"Joined channel {channel}.");
                    joined = true;
                }

                UpdateHostmaskMap(line);
                HandleLine(line);
            }
        }

        // === AUTO MESSAGE SYSTEM ===
        private static async Task MessageScheduler()
        {
            while (true)
            {
                foreach (var msg in scheduledMessages)
                {
                    _ = Task.Run(async () =>
                    {
                        while (true)
                        {
                            await Task.Delay(msg.Interval * 60000);
                            writer?.WriteLine($"PRIVMSG {channel} :{msg.Message}");
                            Console.WriteLine($"[AutoMsg] Sent: {msg.Message}");
                        }
                    });
                }

                await Task.Delay(TimeSpan.FromHours(24));
                LoadMessages();
            }
        }

        private static void LoadMessages()
        {
            scheduledMessages.Clear();

            if (!File.Exists(messagesFile))
            {
                File.WriteAllText(messagesFile, "Welcome to the channel! 1\nStay active and have fun! 2");
                Console.WriteLine("messages.txt created.");
            }

            foreach (var line in File.ReadAllLines(messagesFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Trim().Split(' ');
                if (int.TryParse(parts.Last(), out int priority))
                {
                    string msg = string.Join(' ', parts.Take(parts.Length - 1));
                    int interval = priority switch
                    {
                        1 => 50,
                        2 => 100,
                        3 => 150,
                        _ => 200
                    };
                    scheduledMessages.Add((msg, interval));
                }
            }

            Console.WriteLine($"Loaded {scheduledMessages.Count} auto messages.");
        }

        // === IRC LINE HANDLING ===
        private static void HandleLine(string line)
        {
            if (line.Contains(" JOIN ") && line.Contains(channel))
            {
                string hostmask = ExtractHostmask(line);
                string joinedNick = GetNick(line);

                if (!string.IsNullOrEmpty(hostmask) && joinedNick != null)
                    currentHostmasks[joinedNick] = hostmask;

                string identKey = ExtractIdentKey(hostmask);
                if (!string.IsNullOrEmpty(identKey) && voicedUsers.TryGetValue(identKey, out int medal))
                {
                    VoiceUser(joinedNick);
                    Console.WriteLine($"Auto-voiced {joinedNick} (ident {identKey})");
                }
                return;
            }

            if (line.Contains("PRIVMSG"))
            {
                string senderNick = GetNick(line);
                string message = GetMessage(line);

                if (string.IsNullOrWhiteSpace(message)) return;

                // Pass through the CommandManager (handles GambleCommand, MedalCommand, etc.)
                string response = commandManager.TryProcess(senderNick, message, line);
                if (!string.IsNullOrEmpty(response))
                {
                    writer.WriteLine($"PRIVMSG {channel} :{senderNick}: {response}");
                    Console.WriteLine($"[Command] {senderNick}: {response}");
                }
            }
        }

        // === HELPERS ===
        private static string ExtractIdentKey(string hostmask)
        {
            if (string.IsNullOrWhiteSpace(hostmask)) return "";
            int at = hostmask.IndexOf('@');
            if (at == -1) return hostmask;
            string ident = hostmask.Substring(0, at);
            return ident.Length > 0 ? ident : hostmask;
        }

        private static void VoiceUser(string nickToVoice)
        {
            writer.WriteLine($"PRIVMSG ChanServ :voice {channel} {nickToVoice}");
            Console.WriteLine($"Gave voice to {nickToVoice}");
        }

        private static string GetNick(string line)
        {
            if (line.StartsWith(":"))
            {
                int end = line.IndexOf('!');
                if (end > 1) return line.Substring(1, end - 1);
            }
            return "Unknown";
        }

        private static string GetMessage(string line)
        {
            int idx = line.IndexOf("PRIVMSG");
            if (idx == -1) return "";
            int msgStart = line.IndexOf(':', idx);
            if (msgStart == -1) return "";

            string message = line[(msgStart + 1)..].Trim();

            // ✅ Ignore everything before '!' — fixes heart emoji or extra chars
            int exclamationIndex = message.IndexOf('!');
            if (exclamationIndex != -1)
                message = message.Substring(exclamationIndex);

            return message;
        }

        private static void LoadAdmins()
        {
            if (File.Exists(adminsFile))
                admins = new HashSet<string>(
                    File.ReadAllLines(adminsFile)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Trim()),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static void LoadVoiced()
        {
            if (!File.Exists(voicedFile)) return;

            foreach (string line in File.ReadAllLines(voicedFile))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[1], out int medal))
                    voicedUsers[parts[0]] = medal;
            }

            Console.WriteLine($"Loaded voiced entries: {voicedUsers.Count}");
        }

        public static void SaveVoiced()
        {
            File.WriteAllLines(voicedFile, voicedUsers.Select(kv => $"{kv.Key} {kv.Value}"));
            Console.WriteLine("Saved voiced.txt");
        }

        private static void UpdateHostmaskMap(string line)
        {
            if (!line.StartsWith(":")) return;
            int bang = line.IndexOf('!');
            if (bang == -1) return;
            int spaceAfterHostmask = line.IndexOf(' ', bang);
            if (spaceAfterHostmask == -1) return;

            string hostmask = line.Substring(bang + 1, spaceAfterHostmask - (bang + 1));
            string nickSeen = GetNick(line);

            if (!string.IsNullOrWhiteSpace(nickSeen) && !string.IsNullOrWhiteSpace(hostmask))
                currentHostmasks[nickSeen] = hostmask;
        }

        private static string ExtractHostmask(string line)
        {
            if (!line.StartsWith(":")) return string.Empty;
            int bang = line.IndexOf('!');
            if (bang == -1) return string.Empty;
            int spaceAfterHostmask = line.IndexOf(' ', bang);
            if (spaceAfterHostmask == -1) return string.Empty;
            return line.Substring(bang + 1, spaceAfterHostmask - (bang + 1));
        }

        // === ACCESS HELPERS FOR COMMANDS ===
        public static StreamWriter Writer => writer;
        public static string Channel => channel;
        public static Dictionary<string, int> VoicedUsers => voicedUsers;
        public static Dictionary<string, string> CurrentHostmasks => currentHostmasks;
        public static void SaveVoicedData() => SaveVoiced();
    }
}
