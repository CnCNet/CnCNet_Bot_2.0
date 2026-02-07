using System;
using System.Collections.Generic;
using System.Linq;

namespace MedalBot.Commands
{
    public class CommandManager
    {
        private readonly List<ICommand> _commands = new();

        public CommandManager()
        {
            // Register commands here
            _commands.Add(new GambleCommand());
            _commands.Add(new MedalCommand());
        }

        public string TryProcess(string senderNick, string message, string fullLine)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            // 🔥 FIX: ignore anything before the first '!' (e.g., ♥10)
            int bangIndex = message.IndexOf('!');
            if (bangIndex >= 0)
                message = message.Substring(bangIndex);

            foreach (var cmd in _commands)
            {
                var (handled, response) = cmd.Process(senderNick, message, fullLine);
                if (handled && !string.IsNullOrEmpty(response))
                    return response;
            }

            return null;
        }
    }
}