using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

// Runs StarterBot.cs unchanged, as its own process, and turns what it prints into a status for the page.
public partial class BotService(IOptions<BadgerOptions> options, IWebHostEnvironment env)
{
    private static readonly HttpClient Http = new();

    private readonly BadgerOptions _options = options.Value;
    private readonly object _lock = new();
    private readonly List<string> _log = [];
    private Process? _bot;
    private bool _stopRequested;
    private string _phase = "Idle";
    private string? _matchId, _watchUrl;
    private int _tick, _ore, _units;

    private string BotFolder => Path.GetFullPath(Path.Combine(env.ContentRootPath, _options.BotFolder));

    public BotStatus GetStatus()
    {
        lock (_lock)
        {
            return new BotStatus(_bot != null, _phase, _matchId, _watchUrl, _tick, _ore, _units, _log.ToList());
        }
    }

    public bool Start()
    {
        lock (_lock)
        {
            if (_bot != null) return false;  // already running

            // The same command you would type: dotnet run StarterBot.cs -- --player-name ... --server ...
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = BotFolder,     // the bot saves apikey-<name>.txt here
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,     // nothing to type; the name is passed below
                UseShellExecute = false,
            };
            foreach (var arg in new[]
                     {
                         "run", _options.BotFile, "--",
                         "--player-name", _options.PlayerName,
                         "--server", _options.ServerUrl,
                         "--opponent", _options.Opponent,
                         "--tick-ms", _options.TickMs.ToString(),
                     })
            {
                start.ArgumentList.Add(arg);
            }

            var bot = new Process { StartInfo = start, EnableRaisingEvents = true };
            bot.OutputDataReceived += (_, e) => { if (e.Data != null) OnBotOutput(e.Data); };
            bot.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) AddLog("bot error: " + e.Data); };
            bot.Exited += (_, _) => OnBotExited();

            _phase = "Starting";  // the first run also compiles the bot, so this can take a while
            _matchId = _watchUrl = null;
            _tick = _ore = _units = 0;
            _stopRequested = false;
            _log.Clear();
            try
            {
                bot.Start();
            }
            catch (Exception ex)
            {
                _phase = "Error";
                _log.Add("Could not start the bot: " + ex.Message);
                return true;
            }

            bot.StandardInput.Close();
            bot.BeginOutputReadLine();
            bot.BeginErrorReadLine();
            _bot = bot;
        }

        AddLog($"Started {_options.BotFile} as {_options.PlayerName} against {_options.Opponent}.");
        return true;
    }

    public async Task StopAsync()
    {
        Process? bot;
        string? matchId;
        lock (_lock)
        {
            bot = _bot;
            matchId = _matchId;
            _stopRequested = true;
        }

        if (bot == null) return;
        try { bot.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }

        // Killing the bot doesn't end the match, so surrender with the key the bot saved.
        var keyFile = Path.Combine(BotFolder, $"apikey-{_options.PlayerName}.txt");
        if (matchId != null && File.Exists(keyFile))
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_options.ServerUrl.TrimEnd('/')}/api/v1/matches/{matchId}/surrender");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await File.ReadAllTextAsync(keyFile)).Trim());
            try { await Http.SendAsync(request); } catch (HttpRequestException) { }
        }
    }

    // StarterBot prints "In match <id>. Watch it at <url>", "Tick 12: 340 ore, 7 units",
    // "Could not ..." on errors, and "<name>: <n> points" at the end.
    private void OnBotOutput(string line)
    {
        var tick = TickLine().Match(line);
        if (tick.Success)
        {
            lock (_lock)
            {
                _phase = "Playing";
                _tick = int.Parse(tick.Groups[1].Value);
                _ore = int.Parse(tick.Groups[2].Value);
                _units = int.Parse(tick.Groups[3].Value);
            }
            return;  // one per tick: too many for the log
        }

        var match = MatchLine().Match(line);
        lock (_lock)
        {
            if (match.Success)
            {
                _watchUrl = match.Groups[1].Value;
                _matchId = match.Groups[2].Value;
                _phase = "WaitingForStart";
            }
            else if (line.StartsWith("Could not") || line.StartsWith("No match"))
            {
                _phase = "Error";
            }
        }

        AddLog(line);
    }

    private void OnBotExited()
    {
        lock (_lock)
        {
            if (_stopRequested) _phase = "Stopped";
            else if (_phase != "Error") _phase = "Finished";
            _bot?.Dispose();
            _bot = null;
        }

        AddLog("Bot process ended.");
    }

    private void AddLog(string message)
    {
        lock (_lock)
        {
            _log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_log.Count > 30) _log.RemoveAt(0);
        }
    }

    [GeneratedRegex(@"^Tick (\d+): (\d+) ore, (\d+) units")]
    private static partial Regex TickLine();

    [GeneratedRegex(@"Watch it at (\S+/#/match/(\S+))$")]
    private static partial Regex MatchLine();
}