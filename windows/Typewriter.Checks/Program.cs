using System.Text.Json;
using Typewriter.Core;

var checks = new List<(string Name, Action<string> Body)> {
    ("Unicode notes and settings survive a relaunch", root => {
        Guid id;
        using (var library = new PortableLibrary(root)) {
            var session = new NoteSession(library);
            id = session.Create("À demain", "Écrire encore. 🌧️\r\nTomorrow.").Id;
            session.Edit(text: "Revised 🌧️\nPreserved."); session.Flush();
            session.Preferences = session.Preferences with { Theme = "midnight", KeySounds = true };
            session.SavePreferences();
        }
        using var reopened = new PortableLibrary(root);
        var restored = new NoteSession(reopened);
        Expect(restored.SelectedId == id && restored.Selected?.Text == "Revised 🌧️\nPreserved.", "Saved selection or text changed");
        Expect(restored.Preferences.Theme == "midnight" && restored.Preferences.KeySounds, "Settings changed");
    }),
    ("Switching notes flushes pending edits", root => {
        using var library = new PortableLibrary(root);
        var session = new NoteSession(library);
        var first = session.Create("First");
        session.Edit(text: "Keep the last keystroke");
        session.Create("Second", "Untouched");
        Expect(library.LoadNotes().Single(note => note.Id == first.Id).Text == "Keep the last keystroke", "Pending edit lost");
        Expect(library.LoadNotes().Count == 3, "Other note overwritten");
    }),
    ("Trash is recoverable after closing the app", root => {
        Guid id;
        using (var library = new PortableLibrary(root)) {
            var session = new NoteSession(library);
            id = session.Create("Recover me", "Still here").Id; session.Trash();
        }
        using var reopened = new PortableLibrary(root);
        var restored = new NoteSession(reopened);
        Expect(restored.Search("recover", true).Single().Id == id, "Trash was lost");
        restored.Select(id); restored.Restore();
        Expect(reopened.LoadNotes().Single(note => note.Id == id).DeletedAt is null, "Restore was not saved");
    }),
    ("Moving the executable folder moves its library", root => {
        var original = Path.Combine(root, "original folder");
        using (var library = new PortableLibrary(original)) {
            var session = new NoteSession(library); session.Create("Travelling page", "USB-ready 🌿"); session.SavePreferences();
        }
        var moved = Path.Combine(root, "Moved — Écrire"); Directory.Move(original, moved);
        using var reopened = new PortableLibrary(moved);
        Expect(new NoteSession(reopened).Selected?.Text == "USB-ready 🌿", "Library did not follow the folder");
    }),
    ("Working directory never controls the data path", root => {
        var old = Environment.CurrentDirectory;
        var elsewhere = Path.Combine(root, "unrelated"); Directory.CreateDirectory(elsewhere);
        try {
            Environment.CurrentDirectory = elsewhere;
            using var library = new PortableLibrary(Path.Combine(root, "app"));
            new NoteSession(library).Create("Here", "Beside the app");
            Expect(!Directory.Exists(Path.Combine(elsewhere, "TypewriterData")), "Data leaked to working directory");
        } finally { Environment.CurrentDirectory = old; }
    }),
    ("A second instance cannot write the same library", root => {
        using var first = new PortableLibrary(root);
        Throws(() => { using var second = new PortableLibrary(root); });
    }),
    ("Corrupt notes remain intact and report an error", root => {
        using var library = new PortableLibrary(root);
        var path = Path.Combine(library.NotesDirectory, "broken.json"); File.WriteAllText(path, "unreadable");
        Throws(() => library.LoadNotes());
        Expect(File.ReadAllText(path) == "unreadable", "Corrupt original was changed");
    }),
    ("Atomic updates retain a previous-version backup", root => {
        using var library = new PortableLibrary(root);
        var note = new Note { Text = "Previous version" }; library.Save(note); library.Save(note with { Text = "New version" });
        var backup = Directory.GetFiles(library.NotesDirectory, "*.bak").Single();
        Expect(JsonSerializer.Deserialize<Note>(File.ReadAllText(backup), NoteJson.Options)?.Text == "Previous version", "Backup incorrect");
        Expect(library.LoadNotes().Count == 1, "Backup appeared as a second note");
    }),
    ("Mac-compatible dates and identifiers round trip", root => {
        using var library = new PortableLibrary(root);
        var id = Guid.NewGuid();
        var json = $$"""{"id":"{{id.ToString().ToUpperInvariant()}}","title":"Mac note","text":"Hello 🌍","createdAt":"2026-11-18T21:00:00Z","updatedAt":"2026-11-18T22:00:00Z"}""";
        File.WriteAllText(Path.Combine(library.NotesDirectory, id + ".json"), json);
        var note = library.LoadNotes().Single(); library.Save(note);
        Expect(note.Text == "Hello 🌍", "Mac Unicode changed");
        var saved = File.ReadAllText(Path.Combine(library.NotesDirectory, id.ToString().ToUpperInvariant() + ".json"));
        Expect(saved.Contains("2026-11-18T22:00:00Z"), "Date not compatible with Swift decoder");
    }),
    ("Preferences reject invalid sizes and themes", root => {
        using var library = new PortableLibrary(root);
        library.SavePreferences(new Preferences { FontSize = 900, Volume = -2, Theme = "unknown" });
        var result = library.LoadPreferences();
        Expect(result.FontSize == 24 && result.Volume == 0 && result.Theme == "linen", "Settings not validated");
        Expect(new Note { Text = "  one\r\n two\tthree  " }.WordCount == 3, "Word count incorrect");
    })
};
int failed = 0;
foreach (var (name, body) in checks) {
    var root = Path.Combine(Path.GetTempPath(), "typewriter-check-" + Guid.NewGuid().ToString("N"));
    try { body(root); Console.WriteLine("PASS: " + name); }
    catch (Exception error) { failed++; Console.WriteLine($"FAIL: {name}: {error}"); }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
Console.WriteLine($"{checks.Count - failed}/{checks.Count} portable storage checks passed");
return failed == 0 ? 0 : 1;
static void Expect(bool passed, string message) { if (!passed) throw new Exception(message); }
static void Throws(Action body) { try { body(); } catch { return; } throw new Exception("Expected an error"); }
