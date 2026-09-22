using System.Numerics;
using ASCIIcat.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace ASCIIcat;

public sealed class MainWindow : Window, IDisposable
{
    private enum Page
    {
        Collection,
        RecentChat,
        ImportEdit,
        Settings,
    }

    private readonly Plugin plugin;
    private readonly HashSet<long> selectedRecent = [];
    private Guid? selectedArtworkId;
    private Guid? editingArtworkId;
    private Page? pageToSelect = Page.Collection;
    private string search = string.Empty;
    private string draftName = "Untitled artwork";
    private string draftTags = string.Empty;
    private string draftSourceNote = string.Empty;
    private string draftText = string.Empty;
    private string originalDraftText = string.Empty;
    private ArtworkSourceKind draftSourceKind = ArtworkSourceKind.Manual;
    private string? tabConversionPreview;
    private int tabWidth = 4;
    private string status = "Ready.";
    private bool confirmDelete;
    private Guid? pendingSendArtworkId;

    public MainWindow(Plugin plugin)
        : base("ASCIIcat###ASCIIcatMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 440),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(860, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = true;
    }

    public void Dispose()
    {
    }

    public void OpenRecent()
    {
        pageToSelect = Page.RecentChat;
        IsOpen = true;
    }

    public void OpenSettings()
    {
        pageToSelect = Page.Settings;
        IsOpen = true;
    }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();

        if (ImGui.BeginTabBar("ASCIIcatPages"))
        {
            DrawTab("Collection", Page.Collection, DrawCollection);
            DrawTab("Recent Chat", Page.RecentChat, DrawRecentChat);
            DrawTab("Import / Edit", Page.ImportEdit, DrawEditor);
            DrawTab("Settings", Page.Settings, DrawSettings);
            ImGui.EndTabBar();
            pageToSelect = null;
        }

        ImGui.Separator();
        ImGui.TextDisabled(status);
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted("ASCIIcat");
        ImGui.SameLine();
        ImGui.TextDisabled("local chat-art scrapbook");
        ImGui.SameLine(ImGui.GetWindowWidth() - 180 * ImGuiHelpers.GlobalScale);
        ImGui.TextDisabled($"{plugin.Library.Artworks.Count} saved");
    }

    private void DrawTab(string label, Page page, Action draw)
    {
        var flags = pageToSelect == page ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (ImGui.BeginTabItem(label, flags))
        {
            draw();
            ImGui.EndTabItem();
        }
    }

    private void DrawCollection()
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", "Search names, tags, or artwork...", ref search, 256);
        ImGui.Spacing();

        var listWidth = 240 * ImGuiHelpers.GlobalScale;
        if (ImGui.BeginChild("ArtworkList", new Vector2(listWidth, -1), true))
        {
            if (ImGui.Button("+ New artwork", new Vector2(-1, 0)))
                BeginNewArtwork(ArtworkSourceKind.Manual, string.Empty);

            ImGui.Separator();
            foreach (var artwork in FilteredArtworks())
            {
                var safeName = artwork.Name.Replace("##", "# #", StringComparison.Ordinal);
                var label = $"{(artwork.IsFavorite ? "* " : string.Empty)}{safeName}##{artwork.Id}";
                if (ImGui.Selectable(label, selectedArtworkId == artwork.Id))
                {
                    selectedArtworkId = artwork.Id;
                    confirmDelete = false;
                    pendingSendArtworkId = null;
                    if (plugin.SendingSession.ArtworkId != artwork.Id)
                        plugin.SendingSession.Stop();
                }
            }

            ImGui.EndChild();
        }

        ImGui.SameLine();
        if (ImGui.BeginChild("ArtworkDetail", new Vector2(0, -1), true))
        {
            var artwork = SelectedArtwork();
            if (artwork is null)
            {
                ImGui.TextWrapped("Choose an artwork, import one from the clipboard, or capture recent chat.");
            }
            else
            {
                DrawArtworkDetail(artwork);
            }

            ImGui.EndChild();
        }
    }

    private void DrawArtworkDetail(AsciiArtwork artwork)
    {
        ImGui.TextUnformatted(artwork.Name);
        ImGui.SameLine();
        var favorite = artwork.IsFavorite;
        if (ImGui.Checkbox("Favorite", ref favorite))
        {
            artwork.IsFavorite = favorite;
            artwork.UpdatedAt = DateTimeOffset.UtcNow;
            SaveLibrary("Favorite updated.");
        }

        if (artwork.Tags.Count > 0)
            ImGui.TextDisabled(string.Join("  ", artwork.Tags.Select(tag => $"#{tag}")));

        ImGui.Spacing();
        DrawPreview(artwork.Lines, plugin.Configuration.ShowInvisibleCharacters, 230);

        ImGui.Spacing();
        if (ImGui.Button("Edit"))
            BeginEditArtwork(artwork);
        ImGui.SameLine();
        if (ImGui.Button("Copy whole artwork"))
        {
            ImGui.SetClipboardText(artwork.ToText());
            status = "Copied the whole artwork. Nothing was sent to chat.";
        }
        ImGui.SameLine();
        if (ImGui.Button(confirmDelete ? "Confirm delete" : "Delete..."))
        {
            if (confirmDelete)
            {
                plugin.Library.Artworks.RemoveAll(candidate => candidate.Id == artwork.Id);
                selectedArtworkId = null;
                plugin.SendingSession.Stop();
                SaveLibrary("Artwork deleted.");
                confirmDelete = false;
                return;
            }

            confirmDelete = true;
        }

        ImGui.Separator();
        DrawAutomaticSender(artwork);
        ImGui.Separator();
        DrawSendingSession(artwork);
    }

    private void DrawAutomaticSender(AsciiArtwork artwork)
    {
        var queue = plugin.ChatSendQueue;
        if (queue.ArtworkId == artwork.Id && (queue.IsRunning || queue.IsComplete))
        {
            var state = queue.IsComplete ? "Complete" : "Sending";
            ImGui.TextUnformatted($"{state}: {queue.SentCount} of {queue.LineCount} lines to {DestinationLabel(queue.Destination)}");
            ImGui.ProgressBar(queue.LineCount == 0 ? 0 : (float)queue.SentCount / queue.LineCount, new Vector2(-1, 0));
            if (queue.SkippedBlankCount > 0)
                ImGui.TextDisabled($"Skipped {queue.SkippedBlankCount} empty line(s); FFXIV cannot submit an empty chat message.");

            if (queue.IsRunning)
            {
                if (ImGui.Button("Cancel sending now"))
                {
                    queue.Cancel();
                    status = "Automatic sending cancelled.";
                }

                return;
            }

            if (ImGui.Button("Send again"))
                pendingSendArtworkId = artwork.Id;
            ImGui.SameLine();
            if (ImGui.Button("Clear send result"))
                queue.Cancel();
            return;
        }

        ImGui.TextUnformatted("Automatic sender");
        var destination = plugin.Configuration.AutomaticSendDestination;
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("Destination", DestinationLabel(destination)))
        {
            foreach (var candidate in Enum.GetValues<ChatDestination>())
            {
                if (ImGui.Selectable(DestinationLabel(candidate), candidate == destination))
                {
                    destination = candidate;
                    plugin.Configuration.AutomaticSendDestination = candidate;
                    plugin.Configuration.Save();
                    pendingSendArtworkId = null;
                }
            }

            ImGui.EndCombo();
        }

        var delay = plugin.Configuration.AutomaticSendDelayMilliseconds;
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Delay (ms)", ref delay, 100, 500))
        {
            plugin.Configuration.AutomaticSendDelayMilliseconds = Math.Clamp(delay, 1000, 10000);
            plugin.Configuration.Save();
        }
        ImGui.TextDisabled("Minimum 1000 ms between lines. Empty lines are skipped.");
        ImGui.TextDisabled("Edge spaces are protected so FFXIV cannot trim the artwork's alignment.");

        if (pendingSendArtworkId != artwork.Id)
        {
            if (ImGui.Button("Send whole artwork..."))
                pendingSendArtworkId = artwork.Id;
        }
        else
        {
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), $"Send {artwork.Lines.Count} stored lines to {DestinationLabel(destination)}?");
            if (ImGui.Button("Confirm send"))
            {
                pendingSendArtworkId = null;
                plugin.SendingSession.Stop();
                if (queue.Start(artwork, destination, plugin.Configuration.AutomaticSendDelayMilliseconds))
                    status = $"Sending {artwork.Name} to {DestinationLabel(destination)}.";
                else
                    status = queue.LastError ?? "Could not start automatic sending.";
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                pendingSendArtworkId = null;
        }

        if (queue.LastError is not null)
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.35f, 1f), queue.LastError);
    }

    private void DrawSendingSession(AsciiArtwork artwork)
    {
        if (plugin.SendingSession.ArtworkId != artwork.Id)
        {
            if (ImGui.Button("Manual copy fallback"))
            {
                plugin.SendingSession.Start(artwork);
                status = "Manual line-by-line fallback started.";
            }

            return;
        }

        var index = plugin.SendingSession.NextLineIndex;
        var count = plugin.SendingSession.LineCount;
        if (plugin.SendingSession.IsComplete)
        {
            ImGui.TextColored(new Vector4(0.55f, 0.9f, 0.6f, 1), $"Complete: {count} of {count} lines matched or advanced.");
            if (ImGui.Button("Start again"))
                plugin.SendingSession.Reset();
            return;
        }

        ImGui.TextUnformatted($"Next line: {index + 1} of {count}");
        var current = plugin.SendingSession.CurrentLine ?? string.Empty;
        ImGui.TextDisabled(ExactText.ShowInvisibles(current));

        if (ImGui.Button("Copy next line"))
        {
            ImGui.SetClipboardText(current);
            status = $"Copied line {index + 1} exactly.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Mark sent / next"))
        {
            plugin.SendingSession.AdvanceManually();
            status = "Advanced manually.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
            plugin.SendingSession.Reset();

        ImGui.TextWrapped("Fallback mode: copy one line manually. If the exact line appears in chat, ASCIIcat advances.");
    }

    private void DrawRecentChat()
    {
        var lines = plugin.RecentChat.Snapshot();
        ImGui.TextWrapped("ASCIIcat keeps this small buffer only in memory. Select lines, then move them into the editor as one ordered artwork.");

        if (ImGui.Button("Clear selection"))
            selectedRecent.Clear();
        ImGui.SameLine();
        if (ImGui.Button("Select suggested burst") && selectedRecent.Count > 0)
        {
            var anchor = selectedRecent.Max();
            foreach (var line in plugin.RecentChat.SuggestBurst(anchor))
                selectedRecent.Add(line.Sequence);
        }
        ImGui.SameLine();
        if (ImGui.Button("Edit selected as artwork") && selectedRecent.Count > 0)
        {
            var chosen = lines.Where(line => selectedRecent.Contains(line.Sequence))
                .OrderBy(line => line.Sequence)
                .ToArray();
            BeginNewArtwork(ArtworkSourceKind.Chat, ExactText.JoinLines(chosen.Select(line => line.Body)));
            draftSourceNote = chosen.Length == 0
                ? string.Empty
                : $"Captured from {chosen[0].Channel} chat";
            status = $"Loaded {chosen.Length} chat lines into the editor in chronological order.";
        }

        ImGui.Spacing();
        if (ImGui.BeginChild("RecentChatLines", new Vector2(0, -1), true))
        {
            if (lines.Count == 0)
            {
                ImGui.TextDisabled("No eligible player or /echo messages observed this session yet.");
            }
            else
            {
                foreach (var line in lines)
                {
                    var selected = selectedRecent.Contains(line.Sequence);
                    var sender = string.IsNullOrEmpty(line.Sender) ? "you (/echo)" : line.Sender;
                    var visible = plugin.Configuration.ShowInvisibleCharacters
                        ? ExactText.ShowInvisibles(line.Body)
                        : line.Body;
                    if (visible.Length > 180)
                        visible = visible[..180] + "...";

                    var label = $"{line.Timestamp:HH:mm:ss}  [{line.Channel}]  {sender}: {visible}";
                    ImGui.PushID(line.Sequence.ToString());
                    var changed = ImGui.Checkbox("##Selected", ref selected);
                    ImGui.SameLine();
                    ImGui.TextUnformatted(label);
                    ImGui.PopID();
                    if (changed)
                    {
                        if (selected)
                            selectedRecent.Add(line.Sequence);
                        else
                            selectedRecent.Remove(line.Sequence);
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawEditor()
    {
        ImGui.SetNextItemWidth(330 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("Name", ref draftName, 256);
        ImGui.SameLine();
        if (ImGui.Button("Paste from clipboard"))
        {
            BeginNewArtwork(ArtworkSourceKind.Clipboard, ImGui.GetClipboardText() ?? string.Empty);
            status = "Pasted plain text from the clipboard without normalization.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Import .txt"))
        {
            plugin.FileDialogs.OpenFileDialog(
                "Import text artwork",
                ".txt",
                (success, path) =>
                {
                    if (!success)
                        return;

                    try
                    {
                        var text = File.ReadAllText(path);
                        BeginNewArtwork(ArtworkSourceKind.TextFile, text);
                        draftName = Path.GetFileNameWithoutExtension(path);
                        draftSourceNote = Path.GetFileName(path);
                        status = $"Imported {Path.GetFileName(path)} without trimming or normalization.";
                    }
                    catch (Exception exception)
                    {
                        Plugin.Log.Error(exception, "Could not import text artwork from {Path}.", path);
                        status = "Text-file import failed; see /xllog.";
                    }
                });
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Tags", "Tags separated by commas", ref draftTags, 512);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Source", "Optional source note or URL", ref draftSourceNote, 1024);

        var editorHeight = Math.Max(180, ImGui.GetContentRegionAvail().Y * 0.52f);
        ImGui.InputTextMultiline(
            "##ArtworkEditor",
            ref draftText,
            131072,
            new Vector2(-1, editorHeight),
            ImGuiInputTextFlags.AllowTabInput);

        var lines = ExactText.SplitLines(draftText);
        var tabCount = draftText.Count(c => c == '\t');
        ImGui.TextDisabled($"{lines.Count} lines | {draftText.Length} UTF-16 code units | {tabCount} tabs");
        if (tabCount > 0)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Tab width", ref tabWidth);
            tabWidth = Math.Clamp(tabWidth, 1, 16);
            ImGui.SameLine();
            if (ImGui.Button("Preview tab conversion"))
                tabConversionPreview = ExactText.ExpandTabs(draftText, tabWidth);
        }

        if (tabConversionPreview is not null)
        {
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), "Tab conversion preview (not applied):");
            DrawPreview(ExactText.SplitLines(tabConversionPreview), true, 100);
            if (ImGui.Button("Apply tab conversion"))
            {
                draftText = tabConversionPreview;
                tabConversionPreview = null;
                status = $"Converted tabs to spaces using {tabWidth}-column tab stops.";
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel conversion"))
                tabConversionPreview = null;
        }
        else
        {
            DrawPreview(lines, plugin.Configuration.ShowInvisibleCharacters, 120);
        }

        if (ImGui.Button(editingArtworkId.HasValue ? "Save changes" : "Save artwork"))
            SaveDraft();
        ImGui.SameLine();
        if (ImGui.Button("Revert to original"))
        {
            draftText = originalDraftText;
            tabConversionPreview = null;
            status = "Reverted the editor to its original captured/imported text.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Save as copy"))
        {
            editingArtworkId = null;
            SaveDraft();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear editor"))
            BeginNewArtwork(ArtworkSourceKind.Manual, string.Empty);
    }

    private void DrawSettings()
    {
        var showInvisible = plugin.Configuration.ShowInvisibleCharacters;
        if (ImGui.Checkbox("Show invisible whitespace by default", ref showInvisible))
        {
            plugin.Configuration.ShowInvisibleCharacters = showInvisible;
            plugin.Configuration.Save();
        }

        var capacity = plugin.Configuration.RecentMessageCapacity;
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Recent message capacity", ref capacity))
            plugin.Configuration.RecentMessageCapacity = capacity;

        var minutes = plugin.Configuration.RecentMessageMinutes;
        ImGui.SetNextItemWidth(140 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Recent retention (minutes)", ref minutes))
            plugin.Configuration.RecentMessageMinutes = minutes;

        if (ImGui.Button("Apply buffer settings"))
        {
            plugin.ReconfigureRecentBuffer();
            status = "Recent-chat buffer settings updated.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear recent buffer"))
        {
            plugin.RecentChat.Clear();
            selectedRecent.Clear();
            status = "Recent session buffer cleared.";
        }

        ImGui.Separator();
        ImGui.TextWrapped("Privacy: recent chat stays in memory and disappears when ASCIIcat unloads. Only deliberately saved artwork is written to disk.");
        ImGui.TextWrapped($"Library file: {plugin.LibraryPath}");
        if (ImGui.Button("Export timestamped JSON backup"))
        {
            try
            {
                status = $"Backup exported to {plugin.ExportBackup()}";
            }
            catch (Exception exception)
            {
                Plugin.Log.Error(exception, "Could not export ASCIIcat backup.");
                status = "Backup export failed; see /xllog.";
            }
        }

        ImGui.Separator();
        ImGui.TextWrapped("Automatic sending is deliberate and rate-limited: choose a destination, confirm once, and use Cancel Sending to stop the remaining queue immediately.");
        ImGui.TextWrapped("Programmatic chat sending is intended for this private development build and may not meet the official Dalamud repository's automation rules.");
    }

    private void DrawPreview(IReadOnlyList<string> lines, bool showInvisibles, float height)
    {
        if (ImGui.BeginChild($"Preview##{showInvisibles}_{height}", new Vector2(0, height * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var line = showInvisibles ? ExactText.ShowInvisibles(lines[i]) : lines[i];
                ImGui.TextUnformatted(line.Length == 0 && showInvisibles ? "⟦blank line⟧" : line);

                var diagnostics = ExactText.Diagnose(lines[i], i + 1);
                if (diagnostics.Utf8Bytes > plugin.Configuration.ChatByteWarningThreshold)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1f, 0.55f, 0.35f, 1f), $"  [{diagnostics.Utf8Bytes} UTF-8 bytes]");
                }
            }

            ImGui.EndChild();
        }
    }

    private IEnumerable<AsciiArtwork> FilteredArtworks()
    {
        var query = search.Trim();
        return plugin.Library.Artworks
            .Where(artwork => query.Length == 0
                || artwork.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || artwork.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
                || artwork.Lines.Any(line => line.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(artwork => artwork.IsFavorite)
            .ThenBy(artwork => artwork.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string DestinationLabel(ChatDestination destination) => destination switch
    {
        ChatDestination.Echo => "Echo (local test)",
        ChatDestination.Party => "Party",
        ChatDestination.Say => "Say",
        ChatDestination.FreeCompany => "Free Company",
        ChatDestination.Alliance => "Alliance",
        ChatDestination.Yell => "Yell",
        ChatDestination.Shout => "Shout",
        _ => destination.ToString(),
    };

    private AsciiArtwork? SelectedArtwork()
    {
        return selectedArtworkId.HasValue
            ? plugin.Library.Artworks.FirstOrDefault(artwork => artwork.Id == selectedArtworkId.Value)
            : null;
    }

    private void BeginNewArtwork(ArtworkSourceKind sourceKind, string text)
    {
        editingArtworkId = null;
        draftName = "Untitled artwork";
        draftTags = string.Empty;
        draftSourceNote = string.Empty;
        draftText = text;
        originalDraftText = text;
        draftSourceKind = sourceKind;
        tabConversionPreview = null;
        pageToSelect = Page.ImportEdit;
        IsOpen = true;
    }

    private void BeginEditArtwork(AsciiArtwork artwork)
    {
        editingArtworkId = artwork.Id;
        draftName = artwork.Name;
        draftTags = string.Join(", ", artwork.Tags);
        draftSourceNote = artwork.SourceNote ?? string.Empty;
        draftText = artwork.ToText();
        originalDraftText = ExactText.JoinLines(artwork.OriginalLines);
        draftSourceKind = artwork.SourceKind;
        tabConversionPreview = null;
        pageToSelect = Page.ImportEdit;
    }

    private void SaveDraft()
    {
        var lines = ExactText.SplitLines(draftText);
        var tags = draftTags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        AsciiArtwork artwork;
        if (editingArtworkId.HasValue)
        {
            artwork = plugin.Library.Artworks.First(candidate => candidate.Id == editingArtworkId.Value);
            artwork.Name = string.IsNullOrWhiteSpace(draftName) ? "Untitled artwork" : draftName.Trim();
            artwork.Lines = lines;
            artwork.Tags = tags;
            artwork.SourceNote = string.IsNullOrWhiteSpace(draftSourceNote) ? null : draftSourceNote.Trim();
            artwork.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            artwork = new AsciiArtwork
            {
                Name = string.IsNullOrWhiteSpace(draftName) ? "Untitled artwork" : draftName.Trim(),
                Lines = lines,
                OriginalLines = ExactText.SplitLines(originalDraftText),
                Tags = tags,
                SourceKind = draftSourceKind,
                SourceNote = string.IsNullOrWhiteSpace(draftSourceNote) ? null : draftSourceNote.Trim(),
            };
            plugin.Library.Artworks.Add(artwork);
            editingArtworkId = artwork.Id;
        }

        selectedArtworkId = artwork.Id;
        SaveLibrary($"Saved {artwork.Name} with {artwork.Lines.Count} exact lines.");
        pageToSelect = Page.Collection;
    }

    private void SaveLibrary(string successStatus)
    {
        try
        {
            plugin.SaveLibrary();
            status = successStatus;
        }
        catch
        {
            status = "Save failed; see /xllog. The in-memory copy is still open.";
        }
    }
}
