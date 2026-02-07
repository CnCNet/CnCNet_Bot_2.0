namespace MedalBot.Commands
{
    public interface ICommand
    {
        string Name { get; }
        (bool handled, string response) Process(string senderNick, string message, string fullLine);
    }
}