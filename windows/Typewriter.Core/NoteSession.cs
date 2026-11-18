namespace Typewriter.Core;

public sealed class NoteSession
{
    public const string WelcomeText = "The world can wait a little.\n\nThere is a kind of quiet that only arrives when you begin to write. One word, then another. Nothing to get right yet.\n\nJust follow the light.";
    public PortableLibrary Library { get; }
    public List<Note> Notes { get; }
    public Preferences Preferences { get; set; }
    public Guid? SelectedId { get; private set; }
    public Note? Selected => Notes.FirstOrDefault(note => note.Id == SelectedId);
    private readonly HashSet<Guid> dirty = [];
    public bool HasUnsavedChanges => dirty.Count > 0;

    public NoteSession(PortableLibrary library)
    {
        Library = library;
        Notes = library.LoadNotes().ToList();
        Preferences = library.LoadPreferences();
        if (Notes.Count == 0) Create("A little room to think", WelcomeText);
        SelectedId = Notes.FirstOrDefault(note => note.Id == Preferences.SelectedNote && note.DeletedAt is null)?.Id
            ?? Notes.FirstOrDefault(note => note.DeletedAt is null)?.Id;
    }

    public void Select(Guid? id) { Flush(); SelectedId = id; SavePreferences(); }
    public Note Create(string title = "Untitled", string text = "")
    {
        Flush();
        var note = new Note { Title = title, Text = text };
        Library.Save(note);
        Notes.Insert(0, note);
        SelectedId = note.Id;
        return note;
    }
    public void Edit(string? text = null, string? title = null)
    {
        if (Selected is not { DeletedAt: null } selected) return;
        var updated = selected with { Text = text ?? selected.Text, Title = title ?? selected.Title, UpdatedAt = DateTimeOffset.UtcNow };
        Notes[Notes.IndexOf(selected)] = updated;
        dirty.Add(updated.Id);
    }
    public void Trash()
    {
        if (Selected is not { DeletedAt: null } selected) return;
        Notes[Notes.IndexOf(selected)] = selected with { DeletedAt = DateTimeOffset.UtcNow };
        dirty.Add(selected.Id);
        Flush();
        SelectedId = Notes.Where(note => note.DeletedAt is null).OrderByDescending(note => note.UpdatedAt).FirstOrDefault()?.Id;
        SavePreferences();
    }
    public void Restore()
    {
        if (Selected is not { DeletedAt: not null } selected) return;
        Notes[Notes.IndexOf(selected)] = selected with { DeletedAt = null, UpdatedAt = DateTimeOffset.UtcNow };
        dirty.Add(selected.Id);
        Flush();
    }
    public IEnumerable<Note> Search(string query, bool trash) => Notes
        .Where(note => (note.DeletedAt is not null) == trash &&
            (note.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || note.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        .OrderByDescending(note => note.UpdatedAt);
    public void Flush()
    {
        foreach (var id in dirty.ToArray()) {
            Library.Save(Notes.First(note => note.Id == id));
            dirty.Remove(id);
        }
    }
    public void SavePreferences()
    {
        Preferences = Preferences with { SelectedNote = SelectedId };
        Library.SavePreferences(Preferences);
    }
}
