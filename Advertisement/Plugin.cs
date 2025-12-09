using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace Advertisement;

public enum Destination : byte
{
    Chat = 0,
    Center = 1,
    Panel = 2
}

public class Plugin : IModSharpModule
{
    public string DisplayName => "Advertisement";
    public string DisplayAuthor => "thesamefabius";

    private readonly ISharedSystem _sharedSystem;
    private readonly IModSharp _modSharp;
    private readonly IConVarManager _conVarManager;
    private readonly ISharpModuleManager _modules;
    private readonly IEventManager _eventManager;
    private readonly string _configPath;

    private Config _config;
    private ILocalizerManager? _localizer;
    private ISteamApi _steamGameServer;
    private readonly ILogger _logger;
    private readonly List<Guid> _timers = [];
    private readonly Dictionary<string, string> _colorCache = [];
    private string? _ip;

    public Plugin(ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
    {
        _sharedSystem = sharedSystem;
        _modSharp = sharedSystem.GetModSharp();
        _modules = sharedSystem.GetSharpModuleManager();
        _conVarManager = sharedSystem.GetConVarManager();
        _eventManager = sharedSystem.GetEventManager();

        _configPath = Path.Combine(sharpPath, "configs", "Advertisement", "Advertisement.json");
        _logger = sharedSystem.GetLoggerFactory().CreateLogger(DisplayName);
    }

    public bool Init()
    {
        _steamGameServer = _modSharp.GetSteamGameServer();
        _config = LoadConfig();

        if (_colorCache.Count == 0)
        {
            foreach (var field in typeof(ChatColor).GetFields())
            {
                if (field.GetValue(null) is string value)
                {
                    _colorCache[$"{{{field.Name}}}"] = value;
                }
            }
        }

        _conVarManager.CreateConsoleCommand("ms_ads_reload", (player, info) =>
        {
            StopTimers();
            _config = LoadConfig();
            StartTimers();

            _logger.LogInformation("Config successfully reloaded");
            return ECommandAction.Stopped;
        }, "Reload advertisements");
        return true;
    }


    private string GetPublicIP()
    {
        if (!string.IsNullOrEmpty(_ip) && _ip != "0.0.0.0")
            return _ip;

        var ip = _steamGameServer.GetPublicIP();
        var ipString = $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
        _ip = ipString;
        return ipString;
    }

    public void OnAllModulesLoaded()
    {
        _localizer = _modules.GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity)?.Instance;
        if (_localizer is null)
            _logger.LogWarning("Localizer Manager not initialized or not found.");
        else
            _localizer.LoadLocaleFile("Advertisement");
    }

    public void PostInit()
    {
        StartTimers();
    }

    private void StartTimers()
    {
        foreach (var ads in _config)
        {
            if (ads.Interval > 0)
                _timers.Add(_modSharp.PushTimer(() => ShowAd(ads), ads.Interval, GameTimerFlags.Repeatable));
        }
    }

    private void StopTimers()
    {
        foreach (var timer in _timers)
        {
            _modSharp.StopTimer(timer);
        }

        _timers.Clear();
    }

    private TimerAction ShowAd(Advertisement ads)
    {
        var messages = ads.NextMessages;
        if (messages.Count == 0) return TimerAction.Continue;

        foreach (var (type, message) in messages)
        {
            PrintWrappedLine(type, message);
        }

        return TimerAction.Continue;
    }

    private void PrintWrappedLine(Destination destination, string messageKey)
    {
        if (string.IsNullOrEmpty(messageKey)) return;

        var players = GetPlayers().ToList();
        var globalReplacements = new Dictionary<string, string>
        {
            ["{MAP}"] = _modSharp.GetMapName() ?? "unknown",
            ["{TIME}"] = DateTime.Now.ToString("HH:mm:ss"),
            ["{DATE}"] = DateTime.Now.ToString("dd.MM.yyyy"),
            ["{IP}"] = GetPublicIP(),
            ["{PORT}"] = _conVarManager.FindConVar("hostport")?.GetString() ?? "0",
            ["{MAXPLAYERS}"] = PlayerSlot.MaxPlayerCount.ToString(),
            ["{PLAYERS}"] = players.Count.ToString(),
            ["{N}"] = "\n"
        };

        foreach (var controller in players)
        {
            var gameClient = controller.GetGameClient();
            if (gameClient == null) continue;

            var message = messageKey;
            if (_localizer != null)
            {
                var loc = _localizer.GetLocalizer(gameClient);

                message = Regex.Replace(message, @"\{([\w\d_]+)\}",
                    match => loc.TryGet(match.Groups[1].Value) ?? match.Value);
            }

            message = ReplaceTags(message, globalReplacements);
            switch (destination)
            {
                case Destination.Chat:
                    message = ReplaceColorTags(message);

                    var parts = message.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
                    foreach (var part in parts)
                    {
                        var finalPart = (part.StartsWith(" ") || part.Length == 0) ? part : $" {part}";
                        controller.Print(HudPrintChannel.Chat, finalPart);
                    }

                    break;

                case Destination.Center:
                    controller.Print(HudPrintChannel.Center, message);
                    break;

                case Destination.Panel:
                    ShowWinPanelHtml(controller, message);
                    break;
            }
        }
    }

    private string ReplaceTags(string message, Dictionary<string, string> replacements)
    {
        var sb = new StringBuilder(message);
        foreach (var kvp in replacements)
        {
            sb.Replace(kvp.Key, kvp.Value);
        }

        return sb.ToString();
    }

    private string ReplaceColorTags(string message)
    {
        foreach (var (tag, colorCode) in _colorCache)
        {
            if (message.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                message = message.Replace(tag, colorCode, StringComparison.OrdinalIgnoreCase);
            }
        }

        return message.StartsWith(" ") ? message : $" {message}";
    }

    public void Shutdown()
    {
        StopTimers();
    }

    private Config LoadConfig()
    {
        try
        {
            if (!File.Exists(_configPath))
                return CreateConfig(_configPath);

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<Config>(json)!;
            return config;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load config");
            throw;
        }
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            new Advertisement
            {
                Interval = 300,
                Messages =
                [
                    new Dictionary<Destination, string>
                    {
                        [Destination.Chat] = "{test}",
                        [Destination.Center] = "Test Center 1",
                        [Destination.Panel] = "Test Panel <font color='red'>1</font>"
                    },
                    new Dictionary<Destination, string>
                    {
                        [Destination.Chat] = "{test_currentmap}"
                    },
                    new Dictionary<Destination, string>
                    {
                        [Destination.Chat] = "{test_ip} test_ip"
                    },
                    new Dictionary<Destination, string>
                    {
                        [Destination.Chat] = "{test_players}",
                        [Destination.Panel] = "<font color='#ff0ff0'>{test_players}</font>"
                    },
                    new Dictionary<Destination, string>
                    {
                        [Destination.Center] = "Test Center 2"
                    }
                ]
            }
        };

        var dir = Path.GetDirectoryName(configPath);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(configPath, JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        _logger.LogInformation("The configuration was successfully saved to a file: {0}", configPath);

        return config;
    }

    private IEnumerable<IPlayerController> GetPlayers()
    {
        var entityMgr = _sharedSystem.GetEntityManager();
        for (var i = 0; i < PlayerSlot.MaxPlayerCount; i++)
        {
            var controller = entityMgr.FindPlayerControllerBySlot(new PlayerSlot((byte)i));
            if (controller is { ConnectedState: PlayerConnectedState.PlayerConnected })
            {
                yield return controller;
            }
        }
    }

    private void ShowWinPanelHtml(IPlayerController controller, string html)
    {
        var client = controller.GetGameClient();
        if (client is not { IsValid: true, IsFakeClient: false })
            return;

        var gameEvent = _eventManager.CreateEvent("cs_win_panel_round", true);
        if (gameEvent == null)
            return;

        gameEvent.SetBool("show_timer_defend", false);
        gameEvent.SetBool("show_timer_attack", false);
        gameEvent.SetInt("timer_time", 1);
        gameEvent.SetInt("final_event", -1);
        gameEvent.SetPlayer("funfact_player", controller);
        gameEvent.SetString("funfact_token", html);
        gameEvent.FireToClient(client);
        gameEvent.Dispose();
    }
}