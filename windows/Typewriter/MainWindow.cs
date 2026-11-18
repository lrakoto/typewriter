using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Typewriter.Core;

namespace Typewriter.Windows;

public sealed class MainWindow : Window
{
    public WritingPage Page { get; } = new();
    public NoteSession Session { get; }
    private readonly Grid root = new();
    private readonly Grid stage = new();
    private readonly Border sidebar = new();
    private readonly StackPanel noteRows = new();
    private readonly TextBox title = new();
    private readonly TextBox search = new();
    private readonly TextBlock status = new();
    private readonly TextBlock words = new();
    private readonly TextBlock sectionTitle = new();
    private readonly DispatcherTimer autosave = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DockPanel header = new();
    private readonly DockPanel footer = new();
    private readonly Popup preferencesPopup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Top };
    private readonly Button soundButton;
    private readonly Button lightButton;
    private readonly Button settingsButton;
    private readonly Button leaveFocus;
    private bool loading;
    private bool trash;
    private bool libraryVisible;
    private bool focused;
    private bool fullscreen;
    private WindowState previousState;
    private PaperTheme Theme => PaperTheme.For(Session.Preferences.Theme);

    public MainWindow(NoteSession session)
    {
        Session = session;
        Title = "Typewriter"; Width = 1120; Height = 810; MinWidth = 760; MinHeight = 570;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Typewriter;component/Assets/Typewriter.ico"));
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        Content = root;
        BuildLibrary(); root.Children.Add(sidebar);
        Grid.SetColumn(stage, 1); root.Children.Add(stage); stage.Children.Add(Page);

        header.Margin = new Thickness(34, 35, 34, 0); header.VerticalAlignment = VerticalAlignment.Top;
        var library = IconButton("\uE700", "Library · Ctrl+Shift+L", () => ToggleLibrary());
        library.Margin = new Thickness(0, 0, 18, 0); DockPanel.SetDock(library, Dock.Left); header.Children.Add(library);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        actions.Children.Add(IconButton("\uE740", "Focus mode · Escape to leave", () => SetFocusMode(true)));
        var menuButton = IconButton("\uE712", "Note actions", () => { });
        menuButton.Click += (_, _) => { menuButton.ContextMenu = NoteMenu(); menuButton.ContextMenu.IsOpen = true; };
        actions.Children.Add(menuButton); DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        var names = new StackPanel();
        names.Children.Add(new TextBlock { Text = "T Y P E W R I T E R", FontFamily = new FontFamily("Consolas"), FontSize = 9, Opacity = 0.45, Margin = new Thickness(0, 0, 0, 7) });
        title.FontFamily = new FontFamily("Georgia"); title.FontSize = 16; title.BorderThickness = new Thickness(0);
        title.Background = Brushes.Transparent; title.Padding = new Thickness(0); title.FocusVisualStyle = null;
        title.MaxWidth = 380; title.HorizontalAlignment = HorizontalAlignment.Left; title.MinWidth = 180;
        AutomationProperties.SetName(title, "Note title");
        title.TextChanged += (_, _) => { if (!loading) { Session.Edit(title: title.Text); MarkDirty(); } };
        names.Children.Add(title); header.Children.Add(names); stage.Children.Add(header);

        footer.Margin = new Thickness(34, 0, 34, 23); footer.VerticalAlignment = VerticalAlignment.Bottom;
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        soundButton = IconButton("\uE74F", "Toggle mechanical key sounds", () => ChangePreferences(Session.Preferences with { KeySounds = !Session.Preferences.KeySounds }));
        lightButton = IconButton("\uEA80", "Toggle spotlight", () => ChangePreferences(Session.Preferences with { Spotlight = !Session.Preferences.Spotlight }));
        settingsButton = IconButton("\uE713", "Writing preferences", OpenPreferences);
        tools.Children.Add(soundButton); tools.Children.Add(lightButton); tools.Children.Add(settingsButton);
        DockPanel.SetDock(tools, Dock.Right); footer.Children.Add(tools);
        var counts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        words.FontFamily = new FontFamily("Consolas"); words.FontSize = 10;
        status.FontFamily = new FontFamily("Consolas"); status.FontSize = 10; status.Margin = new Thickness(12, 0, 0, 0);
        counts.Children.Add(words); counts.Children.Add(status); footer.Children.Add(counts); stage.Children.Add(footer);
        leaveFocus = IconButton("\uE73F", "Leave focus mode · Escape", () => SetFocusMode(false));
        leaveFocus.HorizontalAlignment = HorizontalAlignment.Right; leaveFocus.VerticalAlignment = VerticalAlignment.Top;
        leaveFocus.Margin = new Thickness(0, 25, 28, 0); leaveFocus.Visibility = Visibility.Collapsed; stage.Children.Add(leaveFocus);

        Page.TextEdited += text => { Session.Edit(text: text); MarkDirty(); };
        autosave.Tick += (_, _) => { autosave.Stop(); TrySave(false); RefreshLibrary(); };
        Deactivated += (_, _) => { if (!loading) TrySave(false); };
        Closing += OnClosing;
        PreviewKeyDown += OnShortcut;
        Loaded += (_, _) => { RefreshNote(); Page.Editor.Focus(); };
        ApplyTheme(); RefreshLibrary(); RefreshNote();
    }

    private Button IconButton(string glyph, string help, Action action)
    {
        var button = new Button { Content = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 16,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(7),
            Foreground = Theme.InkBrush(0.7), ToolTip = help, Focusable = false, Cursor = Cursors.Hand };
        AutomationProperties.SetName(button, help);
        button.Click += (_, _) => action();
        return button;
    }
    private Button TextButton(string text, Action action)
    {
        var button = new Button { Content = text, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(5), Foreground = Theme.InkBrush(0.7), Cursor = Cursors.Hand, Focusable = false };
        button.Click += (_, _) => action(); return button;
    }
    private void BuildLibrary()
    {
        var grid = new Grid { Margin = new Thickness(17, 0, 17, 0) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        var heading = new DockPanel { Margin = new Thickness(6, 46, 0, 20) };
        var add = IconButton("\uE70F", "New note · Ctrl+N", NewNote); DockPanel.SetDock(add, Dock.Right); heading.Children.Add(add);
        heading.Children.Add(new TextBlock { Text = "Your pages", FontFamily = new FontFamily("Georgia"), FontSize = 22 }); grid.Children.Add(heading);
        search.Margin = new Thickness(0, 0, 0, 10); search.Padding = new Thickness(10); search.BorderThickness = new Thickness(0);
        search.ToolTip = "Find a thought…"; AutomationProperties.SetName(search, "Search notes");
        search.TextChanged += (_, _) => RefreshLibrary(); Grid.SetRow(search, 1); grid.Children.Add(search);
        var sections = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var trashButton = TextButton("Trash", () => { trash = !trash; RefreshLibrary(); }); DockPanel.SetDock(trashButton, Dock.Right); sections.Children.Add(trashButton);
        var notesButton = TextButton("Notes", () => { trash = false; RefreshLibrary(); }); sections.Children.Add(notesButton);
        Grid.SetRow(sections, 2); grid.Children.Add(sections);
        var scroll = new ScrollViewer { Content = noteRows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 3); grid.Children.Add(scroll);
        var bottom = new DockPanel { Margin = new Thickness(6, 18, 0, 22) };
        var import = IconButton("\uE8B5", "Import text · Ctrl+O", Import); DockPanel.SetDock(import, Dock.Right); bottom.Children.Add(import);
        sectionTitle.Text = "LOCAL LIBRARY"; sectionTitle.FontFamily = new FontFamily("Consolas"); sectionTitle.FontSize = 9;
        sectionTitle.Opacity = 0.4; sectionTitle.VerticalAlignment = VerticalAlignment.Center; bottom.Children.Add(sectionTitle);
        Grid.SetRow(bottom, 4); grid.Children.Add(bottom);
        sidebar.Visibility = Visibility.Collapsed; sidebar.ClipToBounds = true;
        sidebar.Child = grid; sidebar.BorderThickness = new Thickness(0, 0, 1, 0);
    }
    private void RefreshLibrary()
    {
        if (Session is null) return;
        noteRows.Children.Clear();
        foreach (var note in Session.Search(search.Text, trash)) {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(note.Title) ? "Untitled" : note.Title,
                FontFamily = new FontFamily("Georgia"), FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new TextBlock { Text = note.Preview, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxHeight = 36,
                TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.55, Margin = new Thickness(0, 8, 0, 8) });
            content.Children.Add(new TextBlock { Text = note.UpdatedAt.LocalDateTime.ToString("MMM d"), FontFamily = new FontFamily("Consolas"), FontSize = 9, Opacity = 0.4 });
            var row = new Button { Content = content, Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 5), BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Foreground = Theme.InkBrush(),
                Background = note.Id == Session.SelectedId ? Theme.InkBrush(0.07) : Brushes.Transparent };
            row.Click += (_, _) => SelectNote(note.Id);
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem(note.DeletedAt is null ? "Move to Trash" : "Restore note", () => { SelectNote(note.Id); if (note.DeletedAt is null) TrashNote(); else RestoreNote(); }));
            menu.Items.Add(MenuItem("Export…", () => { SelectNote(note.Id); Export(); })); row.ContextMenu = menu;
            noteRows.Children.Add(row);
        }
        if (noteRows.Children.Count == 0) noteRows.Children.Add(new TextBlock {
            Text = search.Text.Length > 0 ? "No matching notes." : trash ? "Nothing in the trash." : "A new page is waiting.",
            FontSize = 12, Foreground = Theme.InkBrush(0.5), Margin = new Thickness(14, 20, 10, 0), TextWrapping = TextWrapping.Wrap
        });
        sectionTitle.Text = trash ? "RECOVERABLE TRASH" : "PORTABLE LIBRARY";
    }
    private void RefreshNote()
    {
        loading = true;
        title.Text = Session.Selected?.Title ?? "No note selected";
        title.IsReadOnly = Session.Selected is null || Session.Selected.DeletedAt is not null;
        Page.Configure(Session.Selected, Session.Preferences);
        loading = false;
        RefreshCount(); status.Text = "·  Saved in this folder";
    }
    private void RefreshCount() => words.Text = $"{Session.Selected?.WordCount ?? 0} {(Session.Selected?.WordCount == 1 ? "word" : "words")}";
    private void MarkDirty() { status.Text = "·  Saving…"; RefreshCount(); autosave.Stop(); autosave.Start(); }
    private bool TrySave(bool showError)
    {
        autosave.Stop();
        try { Session.Flush(); Session.SavePreferences(); status.Text = "·  Saved in this folder"; return true; }
        catch (Exception error) { status.Text = "·  Could not save — Ctrl+S to retry"; if (showError) Report(error); return false; }
    }
    private void SelectNote(Guid id)
    {
        try { Session.Select(id); RefreshNote(); RefreshLibrary(); }
        catch (Exception error) { Report(error); }
    }
    public void NewNote()
    {
        try { Session.Create(); trash = false; search.Text = ""; RefreshNote(); RefreshLibrary(); }
        catch (Exception error) { Report(error); }
    }
    public void TrashNote()
    {
        try { Session.Trash(); RefreshNote(); RefreshLibrary(); }
        catch (Exception error) { Report(error); }
    }
    public void RestoreNote()
    {
        try { Session.Restore(); trash = false; RefreshNote(); RefreshLibrary(); }
        catch (Exception error) { Report(error); }
    }
    public void ToggleLibrary()
    {
        libraryVisible = !libraryVisible;
        root.ColumnDefinitions[0].Width = new GridLength(libraryVisible ? 260 : 0);
        sidebar.Visibility = libraryVisible ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SetFocusMode(bool value)
    {
        focused = value;
        header.Visibility = footer.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        leaveFocus.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (value && libraryVisible) ToggleLibrary();
        Page.Editor.Focus();
    }
    private void ToggleFullscreen()
    {
        fullscreen = !fullscreen;
        if (fullscreen) { previousState = WindowState; WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; }
        else { WindowStyle = WindowStyle.SingleBorderWindow; WindowState = previousState; }
    }
    private void OnShortcut(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Escape && (focused || preferencesPopup.IsOpen || fullscreen)) {
            preferencesPopup.IsOpen = false; SetFocusMode(false); if (fullscreen) ToggleFullscreen(); e.Handled = true; return;
        }
        if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; return; }
        if (modifiers == ModifierKeys.Control) {
            switch (e.Key) {
                case Key.N: NewNote(); break;
                case Key.O: Import(); break;
                case Key.S: TrySave(true); break;
                case Key.F: if (!libraryVisible) ToggleLibrary(); search.Focus(); search.SelectAll(); break;
                default: return;
            }
            e.Handled = true;
        } else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) {
            if (e.Key == Key.L) ToggleLibrary(); else if (e.Key == Key.E) Export(); else return;
            e.Handled = true;
        }
    }
    private ContextMenu NoteMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("New note", NewNote, "Ctrl+N"));
        menu.Items.Add(MenuItem("Import text…", Import, "Ctrl+O"));
        menu.Items.Add(MenuItem("Export note…", Export, "Ctrl+Shift+E", Session.Selected is not null));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Move to Trash", TrashNote, enabled: Session.Selected is { DeletedAt: null }));
        menu.Items.Add(MenuItem("Restore note", RestoreNote, enabled: Session.Selected?.DeletedAt is not null));
        menu.Items.Add(new Separator()); menu.Items.Add(MenuItem("Open portable notes folder", RevealNotes));
        return menu;
    }
    private static MenuItem MenuItem(string title, Action action, string shortcut = "", bool enabled = true)
    {
        var item = new MenuItem { Header = title, InputGestureText = shortcut, IsEnabled = enabled };
        item.Click += (_, _) => action(); return item;
    }
    private void Import()
    {
        var dialog = new OpenFileDialog { Title = "Bring a text or Markdown file into your library", Filter = "Text and Markdown|*.txt;*.md|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try {
            var text = File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true));
            Session.Create(Path.GetFileNameWithoutExtension(dialog.FileName), text); trash = false; search.Text = "";
            RefreshNote(); RefreshLibrary();
        } catch (Exception error) { Report(error); }
    }
    private void Export()
    {
        if (Session.Selected is not { } note || !TrySave(true)) return;
        var name = string.Join("_", (string.IsNullOrWhiteSpace(note.Title) ? "Untitled" : note.Title).Split(Path.GetInvalidFileNameChars()));
        var dialog = new SaveFileDialog { Title = "Export note", FileName = name + ".txt", DefaultExt = ".txt", Filter = "Plain text|*.txt|Markdown|*.md" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, note.Text, new UTF8Encoding(false)); }
        catch (Exception error) { Report(error); }
    }
    private void RevealNotes()
    {
        TrySave(false);
        try { Process.Start(new ProcessStartInfo(Session.Library.NotesDirectory) { UseShellExecute = true }); }
        catch (Exception error) { Report(error); }
    }
    private void ChangePreferences(Preferences preferences)
    {
        Session.Preferences = preferences.Validated(); ApplyTheme(); Page.Configure(Session.Selected, Session.Preferences);
        try { Session.SavePreferences(); } catch (Exception error) { Report(error); }
    }
    private void ApplyTheme()
    {
        Background = Theme.PaperBrush; Foreground = Theme.InkBrush();
        sidebar.Background = Theme.PaperBrush; sidebar.BorderBrush = Theme.InkBrush(0.1);
        search.Background = Theme.InkBrush(0.045); search.Foreground = Theme.InkBrush(); search.CaretBrush = Theme.InkBrush();
        title.Foreground = Theme.InkBrush(); title.CaretBrush = Theme.InkBrush();
        words.Foreground = status.Foreground = Theme.InkBrush(0.55);
        foreach (var button in Descendants<Button>(root)) button.Foreground = Theme.InkBrush(0.7);
        if (soundButton is not null) soundButton.Content = Session.Preferences.KeySounds ? "\uE767" : "\uE74F";
        if (lightButton is not null) lightButton.Opacity = Session.Preferences.Spotlight ? 1 : 0.4;
        if (preferencesPopup.Child is Border popupBorder) {
            popupBorder.Background = Theme.PaperBrush;
            popupBorder.BorderBrush = Theme.InkBrush(0.2);
            TextElement.SetForeground(popupBorder, Theme.InkBrush());
        }
        RefreshLibrary();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private void OpenPreferences()
    {
        if (preferencesPopup.IsOpen) { preferencesPopup.IsOpen = false; return; }
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Make yourself at home.", FontFamily = new FontFamily("Georgia"), FontSize = 19, Margin = new Thickness(0, 0, 0, 22) });
        panel.Children.Add(new TextBlock { Text = "P A P E R", FontSize = 9, Opacity = 0.5, Margin = new Thickness(0, 0, 0, 10) });
        var themes = new ComboBox { ItemsSource = new[] { "Linen", "Parchment", "Midnight" }, SelectedIndex = Session.Preferences.Theme switch { "parchment" => 1, "midnight" => 2, _ => 0 }, Margin = new Thickness(0, 0, 0, 18) };
        themes.SelectionChanged += (_, _) => ChangePreferences(Session.Preferences with { Theme = ((string)themes.SelectedItem).ToLowerInvariant() }); panel.Children.Add(themes);
        var sizeLabel = new TextBlock { Text = $"Type size · {Session.Preferences.FontSize:0}", Margin = new Thickness(0, 0, 0, 8) }; panel.Children.Add(sizeLabel);
        var size = new Slider { Minimum = 14, Maximum = 24, TickFrequency = 1, IsSnapToTickEnabled = true, Value = Session.Preferences.FontSize, Margin = new Thickness(0, 0, 0, 18) };
        size.ValueChanged += (_, _) => { sizeLabel.Text = $"Type size · {size.Value:0}"; ChangePreferences(Session.Preferences with { FontSize = size.Value }); }; panel.Children.Add(size);
        var spotlight = new CheckBox { Content = "Follow the cursor with light", IsChecked = Session.Preferences.Spotlight, Margin = new Thickness(0, 0, 0, 14) };
        spotlight.Click += (_, _) => ChangePreferences(Session.Preferences with { Spotlight = spotlight.IsChecked == true }); panel.Children.Add(spotlight);
        var sounds = new CheckBox { Content = "Mechanical key sounds", IsChecked = Session.Preferences.KeySounds, Margin = new Thickness(0, 0, 0, 12) };
        var volume = new Slider { Minimum = 0, Maximum = 0.7, Value = Session.Preferences.Volume, IsEnabled = Session.Preferences.KeySounds, Margin = new Thickness(0, 0, 0, 18) };
        sounds.Click += (_, _) => { ChangePreferences(Session.Preferences with { KeySounds = sounds.IsChecked == true }); volume.IsEnabled = Session.Preferences.KeySounds; };
        panel.Children.Add(sounds); panel.Children.Add(new TextBlock { Text = "Key volume", Opacity = 0.6, FontSize = 11 });
        volume.ValueChanged += (_, _) => ChangePreferences(Session.Preferences with { Volume = volume.Value }); panel.Children.Add(volume);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(new TextBlock { Text = "Your words stay in this folder.\nMove Typewriter.exe and TypewriterData\ntogether to take your library with you.", FontSize = 11, Opacity = 0.6, LineHeight = 18 });
        panel.Children.Add(TextButton("Open notes folder", RevealNotes));
        preferencesPopup.PlacementTarget = settingsButton;
        preferencesPopup.Child = new Border { Child = panel, Width = 350, Background = Theme.PaperBrush, BorderBrush = Theme.InkBrush(0.2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12) };
        TextElement.SetForeground(preferencesPopup.Child, Theme.InkBrush());
        TextElement.SetFontFamily(preferencesPopup.Child, FontFamily);
        TextElement.SetFontSize(preferencesPopup.Child, 13);
        preferencesPopup.IsOpen = true;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!TrySave(false)) e.Cancel = MessageBox.Show(this, "The latest changes could not be saved in this folder.\n\nClose without saving those changes? Choose No to keep Typewriter open and retry.",
            "Typewriter", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes;
        if (!e.Cancel) { autosave.Stop(); preferencesPopup.IsOpen = false; }
    }
    private void Report(Exception error) => MessageBox.Show(this, "Typewriter could not complete that action. Your existing files have been kept.\n\n" + error.Message,
        "Typewriter", MessageBoxButton.OK, MessageBoxImage.Information);
}
