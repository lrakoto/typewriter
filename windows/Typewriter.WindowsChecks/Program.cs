using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Typewriter.Core;
using Typewriter.Windows;

namespace Typewriter.WindowsChecks;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var artifacts = Path.GetFullPath(args.FirstOrDefault() ?? "windows-validation"); Directory.CreateDirectory(artifacts);
        var fixture = Path.Combine(Path.GetTempPath(), "Typewriter native checks " + Guid.NewGuid().ToString("N"));
        using var library = new PortableLibrary(fixture);
        var session = new NoteSession(library);
        var app = new Application();
        var window = new MainWindow(session);
        window.Show(); Drain(window);
        int count = 0, failed = 0;
        void Check(string name, Action body) {
            count++;
            try { body(); Console.WriteLine("PASS: " + name); }
            catch (Exception error) { failed++; Console.WriteLine($"FAIL: {name}: {error}"); }
        }
        void CheckCaret(string name) => Check(name, () => {
            Drain(window); window.Page.PinCursor(false); Drain(window);
            var editor = window.Page.Editor;
            var rect = editor.GetRectFromCharacterIndex(editor.CaretIndex);
            Expect(!rect.IsEmpty, "No native insertion rectangle");
            var point = editor.TranslatePoint(rect.TopLeft, window.Page);
            var gap = window.Page.RailY - point.Y;
            Expect(gap >= 12 && gap <= session.Preferences.FontSize * 1.7 + 24, $"Caret is {gap:0.0}pt above rail at {point}; expected close to active line");
        });
        Check("Native text is visibly rendered on the paper", () => {
            window.Page.PinCursor(false); Drain(window);
            var host = window.Page.Editor.Template.FindName("PART_ContentHost", window.Page.Editor) as ScrollViewer;
            Expect(host is not null && host.ViewportHeight >= session.Preferences.FontSize, "TextBox viewport clips the text");
            var bitmap = Render(window.Page);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            int ink = 0;
            for (int y = Math.Max(0, (int)window.Page.RailY - 65); y < (int)window.Page.RailY - 20; y++)
                for (int x = 40; x < bitmap.PixelWidth - 40; x++) {
                    var offset = (y * bitmap.PixelWidth + x) * 4;
                    if (pixels[offset] < 115 && pixels[offset + 1] < 115 && pixels[offset + 2] < 115 && pixels[offset + 3] > 128) ink++;
                }
            Expect(ink > 50, $"No readable text pixels found at the active line (found {ink})");
        });
        Snapshot(window.Content as FrameworkElement, Path.Combine(artifacts, "windows-linen.png"));
        window.ToggleLibrary(); Drain(window);
        Snapshot(window.Content as FrameworkElement, Path.Combine(artifacts, "windows-library.png"));
        window.ToggleLibrary(); window.NewNote();
        CheckCaret("Blank note pins to the carriage");
        var text = string.Join("\n", Enumerable.Range(1, 100).Select(index => $"Line {index}: A thought with enough room to wander, then return to the page."));
        window.Page.Editor.SelectedText = text;
        CheckCaret("Large paste and soft wrapping stay pinned");
        window.Page.Editor.CaretIndex = window.Page.Editor.Text.Length;
        window.Page.Editor.SelectedText = "\nÉcrire encore. 🌧️\n";
        CheckCaret("Unicode and trailing newline stay pinned");
        Check("Native undo and redo preserve text", () => {
            var before = window.Page.Editor.Text;
            window.Page.Editor.BeginChange(); window.Page.Editor.SelectedText = "Undo me."; window.Page.Editor.EndChange();
            window.Page.Editor.Undo(); Expect(window.Page.Editor.Text == before, "Undo changed the wrong text");
            window.Page.Editor.Redo(); Expect(window.Page.Editor.Text.EndsWith("Undo me."), "Redo failed");
        });
        session.Preferences = session.Preferences with { FontSize = 24 };
        window.Page.Configure(session.Selected, session.Preferences);
        CheckCaret("Type-size changes preserve the active line");
        window.Width = 760; window.Height = 570; window.ToggleLibrary();
        CheckCaret("Small window with library reflows correctly");
        window.Page.Editor.CaretIndex = 10;
        CheckCaret("Editing an earlier line follows the cursor");
        window.Page.Editor.CaretIndex = window.Page.Editor.Text.Length;
        window.Page.PinCursor(false); Drain(window);
        Snapshot(window.Content as FrameworkElement, Path.Combine(artifacts, "windows-narrow.png"));
        window.NewNote();
        Check("Undo history never crosses notes", () => Expect(!window.Page.Editor.CanUndo, "Previous note can be restored through Undo"));
        CheckCaret("Switching to a short note resets the carriage");
        window.Width = 1120; window.Height = 810; window.ToggleLibrary();
        session.Preferences = session.Preferences with { FontSize = 17, Theme = "midnight" };
        session.Edit(text: NoteSession.WelcomeText);
        // Configure a new ID to exercise the same note-switch path used by the UI.
        window.Page.Configure(session.Selected! with { Id = Guid.NewGuid() }, session.Preferences); Drain(window);
        Snapshot(window.Page, Path.Combine(artifacts, "windows-midnight.png"));
        window.Close(); app.Shutdown();
        File.WriteAllText(Path.Combine(artifacts, "native-checks.txt"), $"{count - failed}/{count} native Windows checks passed");
        Console.WriteLine($"{count - failed}/{count} native Windows checks passed");
        library.Dispose(); Directory.Delete(fixture, true);
        return failed == 0 ? 0 : 1;
    }
    private static void Drain(MainWindow window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
        window.UpdateLayout();
    }
    private static void Snapshot(FrameworkElement? element, string path)
    {
        if (element is null) throw new Exception("No visual to render");
        var bitmap = Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }
    private static void Expect(bool passed, string message) { if (!passed) throw new Exception(message); }
}
