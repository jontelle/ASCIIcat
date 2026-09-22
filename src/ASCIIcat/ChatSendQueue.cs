using ASCIIcat.Core;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace ASCIIcat;

public sealed class ChatSendQueue : IDisposable
{
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private IReadOnlyList<OutgoingChatLine> lines = [];
    private DateTimeOffset nextSendAt;

    public ChatSendQueue(IFramework framework, IPluginLog log)
    {
        this.framework = framework;
        this.log = log;
        framework.Update += OnFrameworkUpdate;
    }

    public Guid? ArtworkId { get; private set; }
    public string ArtworkName { get; private set; } = string.Empty;
    public ChatDestination Destination { get; private set; }
    public int DelayMilliseconds { get; private set; }
    public int NextQueueIndex { get; private set; }
    public int SentCount { get; private set; }
    public int SkippedBlankCount { get; private set; }
    public int LineCount => lines.Count;
    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public string? LastError { get; private set; }

    public bool Start(AsciiArtwork artwork, ChatDestination destination, int delayMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        Cancel();

        var plan = OutgoingChat.Build(artwork.Lines, destination);
        var oversized = plan.FirstOrDefault(line => line.Utf8Bytes > OutgoingChat.MaximumCommandBytes);
        if (oversized is not null)
        {
            LastError = $"Artwork line {oversized.ArtworkLineIndex + 1} is {oversized.Utf8Bytes} UTF-8 bytes with its chat command; the limit is {OutgoingChat.MaximumCommandBytes}.";
            return false;
        }

        if (plan.Count == 0)
        {
            LastError = "This artwork has no non-blank lines to send.";
            return false;
        }

        ArtworkId = artwork.Id;
        ArtworkName = artwork.Name;
        Destination = destination;
        DelayMilliseconds = Math.Clamp(delayMilliseconds, 1000, 10000);
        lines = plan;
        NextQueueIndex = 0;
        SentCount = 0;
        SkippedBlankCount = artwork.Lines.Count - plan.Count;
        LastError = null;
        IsComplete = false;
        IsRunning = true;
        nextSendAt = DateTimeOffset.UtcNow;
        return true;
    }

    public void Cancel()
    {
        IsRunning = false;
        IsComplete = false;
        ArtworkId = null;
        ArtworkName = string.Empty;
        lines = [];
        NextQueueIndex = 0;
        SentCount = 0;
        SkippedBlankCount = 0;
        LastError = null;
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        Cancel();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!IsRunning || DateTimeOffset.UtcNow < nextSendAt)
            return;

        if (NextQueueIndex >= lines.Count)
        {
            IsRunning = false;
            IsComplete = true;
            return;
        }

        var line = lines[NextQueueIndex];
        try
        {
            Execute(line.Command);
            SentCount++;
            NextQueueIndex++;
            nextSendAt = DateTimeOffset.UtcNow.AddMilliseconds(DelayMilliseconds);
            if (NextQueueIndex >= lines.Count)
            {
                IsRunning = false;
                IsComplete = true;
            }
        }
        catch (Exception exception)
        {
            LastError = $"Sending stopped at artwork line {line.ArtworkLineIndex + 1}: {exception.Message}";
            IsRunning = false;
            log.Error(exception, "ASCIIcat could not send artwork line {LineNumber}.", line.ArtworkLineIndex + 1);
        }
    }

    private static unsafe void Execute(string command)
    {
        var shell = RaptureShellModule.Instance();
        var ui = UIModule.Instance();
        if (shell == null || ui == null)
            throw new InvalidOperationException("FFXIV's chat processor is unavailable.");

        var text = Utf8String.FromString(command);
        try
        {
            shell->ExecuteCommandInner(text, ui);
        }
        finally
        {
            text->Dtor(true);
        }
    }
}
