using ASCIIcat.Core;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ASCIIcat;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string PrimaryCommand = "/asciicat";
    private const string ShortCommand = "/acat";
    private readonly ArtworkRepository repository;
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        repository = new ArtworkRepository(Path.Combine(PluginInterface.ConfigDirectory.FullName, "artworks.json"));

        try
        {
            Library = repository.Load();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not load ASCIIcat's artwork library. Starting with an empty in-memory library.");
            Library = new ArtworkLibraryFile();
        }

        RecentChat = new RecentChatBuffer(
            Configuration.RecentMessageCapacity,
            TimeSpan.FromMinutes(Configuration.RecentMessageMinutes));
        SendingSession = new SendingSession();
        ChatSendQueue = new ChatSendQueue(Framework, Log);
        FileDialogs = new FileDialogManager();

        mainWindow = new MainWindow(this);
        WindowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(PrimaryCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open ASCIIcat's local chat-art scrapbook.",
        });
        CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open ASCIIcat.",
        });

        ChatGui.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;

        Log.Information("ASCIIcat loaded with {Count} saved artworks.", Library.Artworks.Count);
    }

    public Configuration Configuration { get; }
    public ArtworkLibraryFile Library { get; }
    public RecentChatBuffer RecentChat { get; }
    public SendingSession SendingSession { get; }
    public ChatSendQueue ChatSendQueue { get; }
    public FileDialogManager FileDialogs { get; }
    public WindowSystem WindowSystem { get; } = new("ASCIIcat");
    public string LibraryPath => repository.FilePath;

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        CommandManager.RemoveHandler(PrimaryCommand);
        CommandManager.RemoveHandler(ShortCommand);
        WindowSystem.RemoveAllWindows();
        ChatSendQueue.Dispose();
        mainWindow.Dispose();
    }

    public void SaveLibrary()
    {
        try
        {
            repository.Save(Library);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not save ASCIIcat's artwork library.");
            throw;
        }
    }

    public string ExportBackup() => repository.ExportBackup(Library);

    public void ReconfigureRecentBuffer()
    {
        Configuration.RecentMessageCapacity = Math.Clamp(Configuration.RecentMessageCapacity, 20, 2000);
        Configuration.RecentMessageMinutes = Math.Clamp(Configuration.RecentMessageMinutes, 1, 1440);
        RecentChat.Configure(
            Configuration.RecentMessageCapacity,
            TimeSpan.FromMinutes(Configuration.RecentMessageMinutes));
        Configuration.Save();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("recent", StringComparison.OrdinalIgnoreCase))
            mainWindow.OpenRecent();
        else
            mainWindow.Toggle();
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var body = message.Message.TextValue;
        if (body.Length == 0)
            return;

        var sender = message.Sender.TextValue;
        var channel = message.LogKind.ToString();

        // Player messages have a sender. Echo is retained as a safe local test channel.
        if (sender.Length == 0 && !channel.Equals("Echo", StringComparison.OrdinalIgnoreCase))
            return;

        var timestamp = message.Timestamp > 0
            ? DateTimeOffset.FromUnixTimeSeconds(message.Timestamp)
            : DateTimeOffset.UtcNow;

        RecentChat.Add(timestamp, channel, sender, body);
        if (SendingSession.ObserveExactChatEcho(body))
            Log.Debug("ASCIIcat observed an exact match for the next copied line.");
    }

    private void ToggleMainUi() => mainWindow.Toggle();

    private void OpenSettings() => mainWindow.OpenSettings();

    private void Draw()
    {
        WindowSystem.Draw();
        FileDialogs.Draw();
    }
}
