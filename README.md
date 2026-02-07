
CnCNet_Bot_2.0 – IRC Utility Bot (C#)

Overview

CnCNet_Bot_2.0 is a lightweight IRC bot written in C# for GameSurge and CnCNet channels.
It supports modular commands, admin-only actions, and automatic user tracking.
All commands are handled through individual .cs files located in the Commands folder.

---

1. Features

Command System:
Each command is defined in a separate class implementing ICommand.cs.
Easily add, remove, or modify commands without touching the core logic.

Admin Commands:
Admins are defined in admins.txt.
Example:

N8Diaz
CO2

Auto Voice System:

When an admin uses !medal nick <type>, the bot saves the user’s hostmask in voiced.txt.

It auto-voices recognized players upon join.

Supported medal types:

Platinum = 1
Gold     = 2
Silver   = 3


Custom Messages:

The bot can send automated messages read from messages.txt.

Each message ends with a number that defines its repeat interval (in minutes).
Example:

Remember to join the official ladder! 50
Check out www.cncnet.org for updates! 100


Future Feature – Seen Tracking (planned):

Planned feature to track the last time a user was active.

Will store timestamps in seen.txt for future use.




---

2. Folder Structure

MedalBot/
│
├── Program.cs              → Main bot connection & event logic
├── CommandManager.cs        → Handles command registration and execution
├── ICommand.cs              → Base interface for all commands
│
├── Commands/
│   ├── GambleCommand.cs     → !gamble <options>
│   ├── MedalCommand.cs      → !medal nick <type>, !unmedal nick
│   └── (SeenCommand.cs)     → (future command)
│
├── admins.txt               → List of admin nicks (one per line)
├── voiced.txt               → Saved hostmasks for autovoice
├── messages.txt             → Timed broadcast messages
└── ReadMe.txt               → (this file)


---

3. Commands

Command	Description	Example	Notes

!gamble <word1> <word2> ...	Picks a random option	!gamble Coke Pepsi	Works for all users
!medal <nick> <type>	Awards a medal and adds user to auto-voice	!medal N8Diaz Gold	Admins only
!unmedal <nick>	Removes a medal and unvoices the user	!unmedal CO2	Admins only
(Planned) !seen <nick>	Shows when a user was last active	(Not yet implemented)	Future update



---

4. Configuration Files :

admins.txt

List of users allowed to run admin commands.
Each line should contain only the nickname.

voiced.txt

Automatically maintained by the bot.
Contains the hostmasks of users who should be voiced upon joining.

messages.txt

List of messages to broadcast automatically.
Each message ends with a number (minutes) indicating when it should repeat.

Example:

Welcome to CnCNet Red Alert 1 Ladder! 50
Remember to visit www.cncnet.org/community for news! 100


---

5. Adding New Commands :

1. Create a new .cs file inside the Commands folder (e.g., PingCommand.cs).


2. Implement the ICommand interface:

public class PingCommand : ICommand
{
    public string Name => "ping";
    public (bool handled, string response) Process(string senderNick, string message, string fullLine)
    {
        if (!message.StartsWith("!ping", StringComparison.OrdinalIgnoreCase))
            return (false, null);
        return (true, $"{senderNick}, pong!");
    }
}


3. Register it in CommandManager.cs:

_commands.Add(new PingCommand());


---

6. Compilation & Run

1. Open the project in Visual Studio 2022+ or any C# IDE.


2. Restore dependencies (standard .NET libraries only).


3. Build the project.


4. Run the generated .exe.


5. The bot will connect to the configured IRC server and start listening for messages.


---

6. Notes

The bot ignores anything before ! in IRC messages, so it can safely read messages like:

 PRIVMSG #cncnet-ra :♥10!gamble 1 2

and still recognize the command.

Always run the bot from the same directory so it can access its .txt files.

All times are stored in UTC.

7. Risks / Actions (N8Diaz review)

1. God class (Program.cs) — already collapsing

Program is doing:

Networking
Auth flow
State storage
Scheduling
Message parsing
Command dispatch
Persistence

This is not “simple”. It’s entangled.

Any change risks breaking unrelated behaviour
Impossible to unit test meaningfully
Cognitive load explodes beyond ~1k LOC

Action

Minimum viable separation:

/Core
  IrcClient.cs
  MessageParser.cs
/State
  UserState.cs
  VoiceRegistry.cs
/Services
  AutoMessageService.cs
/Commands
  CommandManager.cs
Program.cs (composition only)

Program.cs should be orchestration, not logic.

2. Global mutable state everywhere

private static HashSet<string> admins
private static Dictionary<string, int> voicedUsers
private static Dictionary<string, string> currentHostmasks
private static StreamWriter writer

Why this is dangerous
Race conditions (already use Task.Run)
Impossible to reason about lifecycle
Commands can mutate shared state silently
Cannot ever run two bots in the same process
Already violating thread safety:
MessageScheduler seems to spawn infinite tasks
scheduledMessages can be reloaded while being iterated
writer is used concurrently without locking

Action

Encapsulate state into a single object and pass it explicitly

class BotContext {
    public IrcClient Client { get; }
    public VoiceRegistry Voices { get; }
}

Static state is the tax we will keep paying forever.

3. Auto message scheduler appears to be broken:

foreach (var msg in scheduledMessages)
{
    _ = Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(msg.Interval * 60000);
            writer?.WriteLine(...)
        }
    });
}

Problems

Every reload spawns new infinite loops
Messages will duplicate over time
No cancellation
Memory leak
Impossible to stop cleanly
If this ran for weeks, it would seem to DOS our own channel.

Action

One scheduler loop
Track next send time
Support cancellation


---

8. Credits

Developed for CnCNet / Red Alert 1 community.
Initial design & command system by CO2.
Code examples adapted (Original Code) from N8Diaz Bot Framework. (N8sBOT)
Reviewed by N8Diaz

Original ReadMe :

# IRC-bot
A simple IRC bot in C# that connects, authenticates and posts an hourly message.

Functionality ideas to consider adding:

!gamble - returns a random word based upon the user's input - Done

!events - returns the current events list e.g. Fight Night 25 Lordy vs Yuzgen 9pm Tonight live on Twitch.tv/yuzgen

!points - tells you how many points a user has (points gained for activeness or something else)

!register - register for a tournament

!status - check your registration status

!bracket - display the tournament bracket and schedule

!report - report match results

Implement a system to generate and update the bracket as the tournament progresses.

Admin menu - kick, ban, add medal, ban name, announcement, mute

Connection to discord channel e.g. annoucements posted in both

Highlight text - annoucements, medalist rooms, links

!Medalists - lists players who have a medal

Gives different medal grades per user access level e.g. 10 = gold, 11 = platinum (edited)
