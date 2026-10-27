public class BadgerOptions
{
    public string ServerUrl { get; set; } = "http://localhost:5080";
    public string PlayerName { get; set; } = "change-me";
    public string Opponent { get; set; } = "sitter";
    public int TickMs { get; set; } = 1000;

    // Where StarterBot.cs lives, relative to the BadgerBot.Api folder.
    public string BotFolder { get; set; } = "../BadgerBot.Bot";
    public string BotFile { get; set; } = "StarterBot.cs";
}

public record BotStatus(bool IsRunning, string Phase, string? MatchId, string? WatchUrl,
    int Tick, int Ore, int Units, IReadOnlyList<string> Log);