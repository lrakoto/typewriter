import AppKit
import SwiftUI
import TypewriterCore
import UniformTypeIdentifiers

@MainActor
final class AppModel: ObservableObject {
    @Published var notes: [Note] = []
    @Published var selectedID: UUID? {
        didSet {
            flush()
            UserDefaults.standard.set(selectedID?.uuidString, forKey: "selectedNote")
        }
    }
    @Published var libraryVisible = false
    @Published var search = ""
    @Published var showingTrash = false
    @Published var saveStatus = "Saved on this Mac"
    @Published var errorMessage: String?
    private var dirtyIDs: Set<UUID> = []
    private var saveTask: Task<Void, Never>?
    let repository: NoteRepository

    init() {
        let override = ProcessInfo.processInfo.environment["TYPEWRITER_DATA_DIR"]
        repository = NoteRepository(directory: override.map { URL(fileURLWithPath: $0) } ?? NoteRepository.defaultDirectory())
        do {
            notes = try repository.load()
            if notes.isEmpty {
                let welcome = Note(title: "A little room to think", text: "The world can wait a little.\n\nThere is a kind of quiet that only arrives when you begin to write. One word, then another. Nothing to get right yet.\n\nJust follow the light.")
                try repository.save(welcome)
                notes = [welcome]
            }
            let remembered = UserDefaults.standard.string(forKey: "selectedNote").flatMap(UUID.init(uuidString:))
            selectedID = notes.first(where: { $0.id == remembered && $0.deletedAt == nil })?.id
                ?? notes.first(where: { $0.deletedAt == nil })?.id
        } catch {
            errorMessage = "Your notes could not be opened. The original files have been kept. \(error.localizedDescription)"
            saveStatus = "Could not open library"
        }
    }

    var selectedNote: Note? { notes.first { $0.id == selectedID } }
    var visibleNotes: [Note] {
        notes.filter {
            ($0.deletedAt != nil) == showingTrash &&
            (search.isEmpty || $0.title.localizedCaseInsensitiveContains(search) || $0.text.localizedCaseInsensitiveContains(search))
        }.sorted { $0.updatedAt > $1.updatedAt }
    }

    func select(_ note: Note) { selectedID = note.id }

    func createNote(text: String = "", title: String = "Untitled") {
        flush()
        let note = Note(title: title, text: text)
        do {
            try repository.save(note)
            notes.insert(note, at: 0)
            showingTrash = false
            search = ""
            selectedID = note.id
            saveStatus = "Saved on this Mac"
        } catch { report(error) }
    }

    func updateText(_ text: String) {
        guard let index = notes.firstIndex(where: { $0.id == selectedID }), notes[index].deletedAt == nil else { return }
        notes[index].text = text
        markDirty(index)
    }

    func updateTitle(_ title: String) {
        guard let index = notes.firstIndex(where: { $0.id == selectedID }), notes[index].deletedAt == nil else { return }
        notes[index].title = title
        markDirty(index)
    }

    private func markDirty(_ index: Int) {
        notes[index].updatedAt = Date()
        dirtyIDs.insert(notes[index].id)
        saveStatus = "Saving…"
        saveTask?.cancel()
        saveTask = Task { [weak self] in
            try? await Task.sleep(for: .milliseconds(400))
            guard !Task.isCancelled else { return }
            self?.flush()
        }
    }

    func flush() {
        saveTask?.cancel()
        for id in dirtyIDs {
            guard let note = notes.first(where: { $0.id == id }) else { continue }
            do {
                try repository.save(note)
                dirtyIDs.remove(id)
            } catch { report(error); return }
        }
        if errorMessage == nil { saveStatus = "Saved on this Mac" }
    }

    func trashSelected() {
        guard let index = notes.firstIndex(where: { $0.id == selectedID }) else { return }
        notes[index].deletedAt = Date()
        dirtyIDs.insert(notes[index].id)
        flush()
        selectedID = notes.filter { $0.deletedAt == nil }.sorted { $0.updatedAt > $1.updatedAt }.first?.id
    }

    func restoreSelected() {
        guard let index = notes.firstIndex(where: { $0.id == selectedID }) else { return }
        notes[index].deletedAt = nil
        markDirty(index)
        flush()
        showingTrash = false
    }

    func importNote() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.plainText, .utf8PlainText, UTType(filenameExtension: "md")!]
        panel.allowsMultipleSelection = false
        panel.message = "Bring a text or Markdown file into your library."
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do { createNote(text: try String(contentsOf: url, encoding: .utf8), title: url.deletingPathExtension().lastPathComponent) }
        catch { report(error) }
    }

    func exportNote() {
        guard let note = selectedNote else { return }
        flush()
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText, UTType(filenameExtension: "md")!]
        panel.nameFieldStringValue = (note.title.isEmpty ? "Untitled" : note.title).replacingOccurrences(of: "/", with: "-") + ".txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do { try note.text.write(to: url, atomically: true, encoding: .utf8) }
        catch { report(error) }
    }

    func revealNotes() { flush(); NSWorkspace.shared.open(repository.directory) }
    private func report(_ error: Error) {
        saveStatus = "Could not save"
        errorMessage = "Typewriter could not complete that action. \(error.localizedDescription)"
    }
}
