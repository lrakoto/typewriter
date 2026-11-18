using System.IO;
using System.Windows;
using Typewriter.Core;

namespace Typewriter.Windows;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--portable-smoke-test")) return PortableSmokeTest.Run(args);
        try {
            using var library = new PortableLibrary(AppContext.BaseDirectory);
            var session = new NoteSession(library);
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            return app.Run(new MainWindow(session));
        } catch (Exception error) {
            MessageBox.Show("Typewriter could not open its portable library.\n\nKeep Typewriter.exe in a folder you can write to, such as Documents or a USB drive. " +
                "If it is already running from this folder, close the other window first.\n\nYour existing files have been kept.\n\n" + error.Message,
                "Typewriter", MessageBoxButton.OK, MessageBoxImage.Information);
            return 1;
        }
    }
}

internal static class PortableSmokeTest
{
    public static int Run(string[] args)
    {
        // Run by the Windows build after copying the actual single-file EXE to
        // a fresh folder. No data-directory override: this tests the shipped path.
        var report = Path.Combine(AppContext.BaseDirectory, "portable-smoke-test.json");
        try {
            using var library = new PortableLibrary(AppContext.BaseDirectory);
            var session = new NoteSession(library);
            if (args.Contains("--verify-existing")) {
                if (session.Selected?.Title != "Portable check" || session.Selected.Text != "Travelling with the folder. 🌿")
                    throw new InvalidDataException("The portable note did not survive moving the folder.");
            } else {
                session.Create("Portable check", "Travelling with the folder. 🌿");
                session.Preferences = session.Preferences with { Theme = "parchment", KeySounds = true };
                session.SavePreferences(); session.Flush();
            }
            var app = new Application();
            var window = new MainWindow(session);
            window.Show(); window.UpdateLayout();
            window.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => window.Page.PinCursor(false)));
            if (window.Page.Editor.Text != session.Selected?.Text) throw new Exception("The bundled native editor did not open the saved note.");
            window.Close(); app.Shutdown();
            if (library.Root != Path.Combine(AppContext.BaseDirectory, "TypewriterData")) throw new Exception("Wrong portable data path.");
            File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(new { passed = true, dataDirectory = library.Root }));
            return 0;
        } catch (Exception error) {
            File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(new { passed = false, error = error.ToString() }));
            return 1;
        }
    }
}
