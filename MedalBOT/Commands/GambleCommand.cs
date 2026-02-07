using System;

namespace MedalBot.Commands
{
    public class GambleCommand : ICommand
    {
        public string Name => "gamble";

        public (bool handled, string response) Process(string senderNick, string message, string fullLine)
        {
            // 🧠 Fix: ignore any text before "!" (e.g. heart emoji, number, etc.)
            int bangIndex = message.IndexOf('!');
            if (bangIndex > 0)
                message = message.Substring(bangIndex);

            if (!message.StartsWith("!gamble", StringComparison.OrdinalIgnoreCase))
                return (false, null);

            string[] args = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (args.Length < 2)
                return (true, $"{senderNick}, usage: !gamble <option1> <option2> ...");

            // 🎲 Pick a random result (skip index 0 = !gamble)
            Random random = new Random();
            int index = random.Next(1, args.Length);
            string chosen = args[index];

            return (true, $"{senderNick} gambled and won: {chosen} 🎲");
        }
    }
}