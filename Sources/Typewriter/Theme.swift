import SwiftUI
import AppKit

enum PaperTheme: String, CaseIterable, Identifiable {
    case linen, parchment, midnight
    var id: String { rawValue }
    var label: String { rawValue.capitalized }
    var paper: NSColor {
        switch self {
        case .linen: return NSColor(red: 0.88, green: 0.875, blue: 0.84, alpha: 1)
        case .parchment: return NSColor(red: 0.85, green: 0.80, blue: 0.70, alpha: 1)
        case .midnight: return NSColor(red: 0.14, green: 0.16, blue: 0.16, alpha: 1)
        }
    }
    var ink: NSColor {
        self == .midnight ? NSColor(red: 0.84, green: 0.82, blue: 0.74, alpha: 1) : NSColor(red: 0.22, green: 0.235, blue: 0.22, alpha: 1)
    }
    var accent: Color { self == .midnight ? Color(red: 0.74, green: 0.68, blue: 0.48) : Color(red: 0.39, green: 0.40, blue: 0.31) }
}
