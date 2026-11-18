import Foundation

public struct Note: Codable, Identifiable, Equatable {
    public var id: UUID
    public var title: String
    public var text: String
    public var createdAt: Date
    public var updatedAt: Date
    public var deletedAt: Date?

    public init(id: UUID = UUID(), title: String = "Untitled", text: String = "", now: Date = Date()) {
        self.id = id
        self.title = title
        self.text = text
        self.createdAt = now
        self.updatedAt = now
    }

    public var wordCount: Int { text.split(whereSeparator: { $0.isWhitespace }).count }
    public var preview: String {
        let line = text.split(whereSeparator: { $0.isNewline }).joined(separator: " ")
        return line.isEmpty ? "A blank page, a fresh start." : String(line.prefix(110))
    }
}

/// One atomic UTF-8 JSON file per note. A failed write leaves the previous file intact.
public struct NoteRepository {
    public let directory: URL
    private let fileManager: FileManager

    public init(directory: URL, fileManager: FileManager = .default) {
        self.directory = directory
        self.fileManager = fileManager
    }

    public func load() throws -> [Note] {
        try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        let files = try fileManager.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil)
            .filter { $0.pathExtension == "json" }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        // Do not silently ignore unreadable notes or overwrite their files.
        return try files.map { try decoder.decode(Note.self, from: Data(contentsOf: $0)) }
            .sorted { $0.updatedAt > $1.updatedAt }
    }

    public func save(_ note: Note) throws {
        try fileManager.createDirectory(at: directory, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        try encoder.encode(note).write(to: directory.appendingPathComponent(note.id.uuidString + ".json"), options: .atomic)
    }

    public static func defaultDirectory() -> URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("Typewriter/Notes", isDirectory: true)
    }
}
