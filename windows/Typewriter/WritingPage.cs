using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Typewriter.Core;

namespace Typewriter.Windows;

public sealed class WritingPage : Grid
{
    public TextBox Editor { get; } = new();
    private readonly PaperDrawing paper = new();
    private readonly DispatcherTimer motion = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Preferences preferences = new();
    private bool changing;
    private bool pinQueued;
    private bool pinning;
    private double targetX;
    private Guid? noteId;
    public event Action<string>? TextEdited;
    public double RailY => ActualHeight * 0.73;
    private double LineTargetY => RailY - preferences.FontSize * 1.7 - 8;

    public WritingPage()
    {
        ClipToBounds = true;
        Children.Add(paper);
        Editor.AcceptsReturn = true; Editor.AcceptsTab = true;
        Editor.TextWrapping = TextWrapping.Wrap;
        Editor.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        Editor.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Editor.FontFamily = new FontFamily("Courier New");
        Editor.Background = Brushes.Transparent; Editor.BorderThickness = new Thickness(0);
        Editor.FocusVisualStyle = null;
        Editor.VerticalContentAlignment = VerticalAlignment.Top;
        Editor.IsUndoEnabled = true; Editor.UndoLimit = 500;
        Editor.Template = EditorTemplate();
        AutomationProperties.SetName(Editor, "Note text");
        Editor.TextChanged += (_, _) => { if (!changing) TextEdited?.Invoke(Editor.Text); QueuePin(); };
        Editor.SelectionChanged += (_, _) => { if (!changing) QueuePin(); };
        Editor.AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) => { e.Handled = true; QueuePin(); }));
        Editor.PreviewTextInput += (_, _) => { if (preferences.KeySounds) KeySound.Play(false, preferences.Volume); };
        Editor.PreviewKeyDown += (_, e) => {
            if (preferences.KeySounds && Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Enter or Key.Back or Key.Delete)
                KeySound.Play(e.Key == Key.Enter, preferences.Volume);
        };
        Children.Add(Editor);
        SizeChanged += (_, _) => { LayoutEditor(); QueuePin(); };
        MouseDown += (_, _) => Editor.Focus();
        motion.Tick += (_, _) => {
            paper.CursorX += (targetX - paper.CursorX) * 0.27;
            if (Math.Abs(targetX - paper.CursorX) < 0.3) { paper.CursorX = targetX; motion.Stop(); }
            paper.InvalidateVisual();
        };
        Unloaded += (_, _) => motion.Stop();
    }

    public void Configure(Note? note, Preferences settings)
    {
        var switched = noteId != note?.Id;
        var restyle = preferences.FontSize != settings.FontSize || preferences.Theme != settings.Theme;
        preferences = settings;
        changing = true;
        if (switched) {
            noteId = note?.Id;
            Editor.IsUndoEnabled = false; Editor.Text = note?.Text ?? "";
            Editor.IsUndoEnabled = true; Editor.CaretIndex = Editor.Text.Length;
        }
        Editor.IsReadOnly = note is null || note.DeletedAt is not null;
        var theme = PaperTheme.For(settings.Theme);
        Editor.FontSize = settings.FontSize;
        TextBlock.SetLineHeight(Editor, settings.FontSize * 1.7);
        Editor.Foreground = theme.InkBrush(); Editor.CaretBrush = theme.InkBrush();
        Editor.SelectionBrush = theme.InkBrush(0.2);
        paper.Preferences = settings;
        paper.InvalidateVisual();
        changing = false;
        if (switched || restyle) { LayoutEditor(); QueuePin(); }
        if (switched) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { Editor.Focus(); PinCursor(false); }));
    }

    private void LayoutEditor()
    {
        if (ActualHeight <= 0 || ActualWidth <= 0) return;
        Editor.Margin = new Thickness(0, 112, 0, Math.Max(0, ActualHeight - RailY + 10));
        var margin = Math.Max(52, (ActualWidth - 680) / 2);
        Editor.Padding = new Thickness(margin, Math.Max(90, LineTargetY - 112), margin, ActualHeight);
    }
    private void QueuePin()
    {
        if (pinQueued || changing) return;
        pinQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => { pinQueued = false; PinCursor(); }));
    }
    public void PinCursor(bool animate = true)
    {
        if (pinning || ActualWidth <= 0 || ActualHeight <= 0) return;
        pinning = true;
        try {
            Editor.UpdateLayout();
            var rect = Editor.GetRectFromCharacterIndex(Editor.CaretIndex);
            if (rect.IsEmpty) return;
            var offset = Editor.VerticalOffset + rect.Top - (LineTargetY - Editor.Margin.Top);
            Editor.ScrollToVerticalOffset(Math.Max(0, offset));
            Editor.UpdateLayout();
            rect = Editor.GetRectFromCharacterIndex(Editor.CaretIndex);
            if (rect.IsEmpty) return;
            targetX = Math.Clamp(rect.Left, 20, Math.Max(20, ActualWidth - 20));
            if (!animate || paper.CursorX == 0) { paper.CursorX = targetX; paper.InvalidateVisual(); }
            else motion.Start();
        } finally { pinning = false; }
    }
    private static ControlTemplate EditorTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        border.AppendChild(content);
        return new ControlTemplate(typeof(TextBox)) { VisualTree = border };
    }
}

internal sealed class PaperDrawing : FrameworkElement
{
    public Preferences Preferences { get; set; } = new();
    public double CursorX { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        var theme = PaperTheme.For(Preferences.Theme);
        var dark = Preferences.Theme == "midnight";
        var bounds = new Rect(RenderSize);
        var railY = ActualHeight * 0.73;
        var x = CursorX == 0 ? ActualWidth / 2 : CursorX;
        dc.DrawRectangle(theme.PaperBrush, null, bounds);
        if (Preferences.Spotlight) {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(dark ? 77 : 87), 14, 14, 14)), null, bounds);
            var radius = Math.Clamp(ActualHeight * 0.31, 190, 240);
            var light = new RadialGradientBrush {
                MappingMode = BrushMappingMode.Absolute, Center = new Point(x, railY - 130),
                GradientOrigin = new Point(x, railY - Preferences.FontSize * 1.7 - 22), RadiusX = radius * 0.86, RadiusY = radius,
                GradientStops = new GradientStopCollection {
                    new(Color.FromArgb((byte)(dark ? 92 : 235), 255, 245, 199), 0),
                    new(Color.FromArgb((byte)(dark ? 71 : 199), 255, 242, 189), 0.24),
                    new(Color.FromArgb((byte)(dark ? 33 : 82), 255, 240, 186), 0.56),
                    new(Color.FromArgb((byte)(dark ? 6 : 15), 255, 240, 194), 0.84),
                    new(Color.FromArgb(0, 255, 240, 194), 1)
                }
            };
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, railY + 2)));
            dc.DrawRectangle(light, null, bounds); dc.Pop();
        }
        var fiber = new Pen(theme.InkBrush(0.012), 0.5);
        for (double y = 0; y < ActualHeight; y += 3) dc.DrawLine(fiber, new Point(0, y), new Point(ActualWidth, y));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(166, (byte)(dark ? 10 : 64), (byte)(dark ? 10 : 64), (byte)(dark ? 10 : 64))), null,
            new Rect(0, railY, ActualWidth, 6));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(71, 255, 255, 255)), null, new Rect(0, railY + 1, ActualWidth, 1));
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)), null, new Rect(0, railY + 6, ActualWidth, 3));
        dc.DrawRectangle(theme.InkBrush(0.65), null, new Rect(x - 0.7, railY - 12, 1.4, 13));
        dc.DrawRectangle(theme.InkBrush(0.65), null, new Rect(x - 4, railY - 13, 8, 1.4));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(32, 0, 0, 0)), null, new Rect(x - 24, railY - 1, 48, 18), 8, 8);
        dc.DrawRoundedRectangle(new SolidColorBrush(dark ? Color.FromRgb(92, 92, 92) : Color.FromRgb(194, 194, 194)),
            new Pen(new SolidColorBrush(Color.FromArgb(82, 255, 255, 255)), 0.7), new Rect(x - 23, railY - 3, 46, 16), 8, 8);
        foreach (var shift in new[] { -9, -3, 3 }) dc.DrawEllipse(theme.InkBrush(0.6), null, new Point(x + shift, railY + 4.4), 0.9, 0.9);
    }
}
