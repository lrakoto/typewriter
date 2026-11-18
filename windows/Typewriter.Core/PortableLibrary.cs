using System.Text.Json;

namespace Typewriter.Core;

public sealed class PortableLibrary : IDisposable
{
    public string Root { get; }
    public string NotesDirectory => Path.Combine(Root, "Notes");
    private FileStream? instanceLock;

    public PortableLibrary(string executableDirectory, bool acquireLock = true)
    {
        Root = Path.GetFullPath(Path.Combine(executableDirectory, "TypewriterData"));
        Directory.CreateDirectory(NotesDirectory);
        if (acquireLock)
        {
            try {
                instanceLock = new FileStream(Path.Combine(Root, ".open.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            } catch (IOException error) {
                throw new IOException("This folder's Typewriter library is already open, or the folder cannot be written. Close its other window and try again.", error);
            }
        }
        // Verify portable storage before accepting edits; never fall back to a hidden user profile.
        var probe = Path.Combine(Root, ".write-check-" + Guid.NewGuid().ToString("N"));
        try { File.WriteAllText(probe, ""); File.Delete(probe); }
        catch { instanceLock?.Dispose(); instanceLock = null; throw; }
    }

    public IReadOnlyList<Note> LoadNotes() => Directory.EnumerateFiles(NotesDirectory, "*.json")
        .Select(file => {
            var note = JsonSerializer.Deserialize<Note>(File.ReadAllBytes(file), NoteJson.Options)
                ?? throw new InvalidDataException($"The note {Path.GetFileName(file)} is empty.");
            if (note.Id == Guid.Empty || note.Text is null || note.Title is null ||
                !string.Equals(Path.GetFileNameWithoutExtension(file), note.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The note {Path.GetFileName(file)} has invalid fields or an inconsistent ID. Its file has been kept.");
            return note;
        }).OrderByDescending(note => note.UpdatedAt).ThenBy(note => note.Id).ToArray();

    public void Save(Note note) => AtomicWrite(Path.Combine(NotesDirectory, note.Id.ToString("D").ToUpperInvariant() + ".json"),
        JsonSerializer.SerializeToUtf8Bytes(note, NoteJson.Options));

    public Preferences LoadPreferences()
    {
        var path = Path.Combine(Root, "settings.json");
        return File.Exists(path) ? (JsonSerializer.Deserialize<Preferences>(File.ReadAllBytes(path), NoteJson.Options)
            ?? throw new InvalidDataException("Settings could not be read.")).Validated() : new Preferences();
    }

    public void SavePreferences(Preferences preferences) => AtomicWrite(Path.Combine(Root, "settings.json"),
        JsonSerializer.SerializeToUtf8Bytes(preferences.Validated(), NoteJson.Options));

    public static void AtomicWrite(string destination, byte[] contents)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".bak");
            else File.Move(temporary, destination);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Dispose() { instanceLock?.Dispose(); instanceLock = null; }
}
