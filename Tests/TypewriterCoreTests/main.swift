import Foundation
import TypewriterCore

struct CheckFailure: Error, CustomStringConvertible {
    let description: String
}
func expect(_ value: @autoclosure () throws -> Bool, _ message: String) throws {
    if try !value() { throw CheckFailure(description: message) }
}
func withRepository(_ body: (NoteRepository) throws -> Void) throws {
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    defer { try? FileManager.default.removeItem(at: directory) }
    try body(NoteRepository(directory: directory))
}
let checks: [(String, () throws -> Void)] = [
    ("Unicode and recoverable trash survive relaunch", {
        try withRepository { repository in
            var note = Note(title: "À demain", text: "Rain.\nDemain, écrire encore. 🌧️")
            note.deletedAt = Date(timeIntervalSince1970: 1_700_000_000)
            try repository.save(note)
            let restored = try repository.load()
            try expect(restored.count == 1, "Expected one note")
            try expect(restored[0].id == note.id, "ID changed")
            try expect(restored[0].title == note.title, "Title changed")
            try expect(restored[0].text == note.text, "Unicode text changed")
            try expect(restored[0].deletedAt == note.deletedAt, "Trash state changed")
        }
    }),
    ("An update preserves other notes and sorts by recency", {
        try withRepository { repository in
            var first = Note(text: "First draft", now: Date(timeIntervalSince1970: 100))
            let second = Note(text: "Keep me", now: Date(timeIntervalSince1970: 200))
            try repository.save(first)
            try repository.save(second)
            first.text = "Revised draft"
            first.updatedAt = Date(timeIntervalSince1970: 300)
            try repository.save(first)
            let restored = try repository.load()
            try expect(restored.count == 2, "Unexpected note count")
            try expect(restored[0].text == "Revised draft", "Update was lost")
            try expect(restored[1] == second, "Other note changed")
        }
    }),
    ("Corrupt data reports an error and stays intact", {
        try withRepository { repository in
            try FileManager.default.createDirectory(at: repository.directory, withIntermediateDirectories: true)
            let file = repository.directory.appendingPathComponent("broken.json")
            let data = Data("unreadable".utf8)
            try data.write(to: file)
            var reported = false
            do { _ = try repository.load() } catch { reported = true }
            try expect(reported, "Corruption was silently ignored")
            try expect(Data(contentsOf: file) == data, "Original file was changed")
        }
    }),
    ("Failed save preserves the previous version", {
        try withRepository { repository in
            let note = Note(text: "Previous version")
            try repository.save(note)
            let file = repository.directory.appendingPathComponent(note.id.uuidString + ".json")
            let original = try Data(contentsOf: file)
            let blocked = repository.directory.appendingPathComponent("not-a-directory")
            try Data().write(to: blocked)
            var reported = false
            do { try NoteRepository(directory: blocked).save(note) } catch { reported = true }
            try expect(reported, "Save failure was not reported")
            try expect(Data(contentsOf: file) == original, "Previous version was changed")
        }
    }),
    ("Whitespace and empty notes have correct word counts", {
        try expect(Note().wordCount == 0, "Empty note has words")
        try expect(Note(text: "  one\n two\tthree  ").wordCount == 3, "Whitespace word count incorrect")
    })
]
var failures = 0
for (name, check) in checks {
    do { try check(); print("PASS: \(name)") }
    catch { failures += 1; print("FAIL: \(name): \(error)") }
}
print("\(checks.count - failures)/\(checks.count) checks passed")
exit(failures == 0 ? 0 : 1)
