import AppKit

/// Exercise the actual AppKit editor, measuring the native insertion rectangle
/// after layout. Uses no saved notes and requires no third-party test runtime.
@main
struct LayoutChecks {
    @MainActor static func main() {
        let application = NSApplication.shared
        application.setActivationPolicy(.prohibited)
        let page = TypewriterPage(frame: NSRect(x: 0, y: 0, width: 1120, height: 780))
        let window = NSWindow(contentRect: page.bounds, styleMask: [.titled, .resizable], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.contentView = page
        window.makeFirstResponder(page.editor)
        let id = UUID()
        var failures = 0
        var count = 0

        func check(_ label: String, size: CGFloat) {
            page.layoutSubtreeIfNeeded()
            page.editor.scrollRangeToVisible(page.editor.selectedRange())
            let range = NSRange(location: page.editor.selectedRange().location, length: 0)
            let screenRect = page.editor.firstRect(forCharacterRange: range, actualRange: nil)
            let caret = page.convert(window.convertFromScreen(screenRect), from: nil)
            let gap = page.bounds.height * 0.73 - caret.minY
            let visible = page.scrollView.frame.insetBy(dx: -2, dy: -2).contains(caret.origin)
            let passed = gap >= 16 && gap <= size * 1.7 + 20 && visible
            count += 1
            if !passed { failures += 1 }
            print("\(passed ? "PASS" : "FAIL"): \(label) (caret gap \(Int(gap))pt)")
        }
        func configure(_ text: String, size: CGFloat = 17, newID: UUID = UUID()) {
            page.configure(id: newID, text: text, size: size, theme: .linen,
                           spotlight: true, sounds: false, volume: 0, editable: true)
            page.layoutSubtreeIfNeeded()
        }

        configure("", newID: id)
        check("Empty page", size: 17)
        let longText = (1...100).map { "Line \($0): A thought with enough room to wander, then return to the page." }.joined(separator: "\n")
        page.editor.insertText(longText, replacementRange: page.editor.selectedRange())
        check("Large paste and soft wrapping", size: 17)
        page.editor.insertText("\nÉcrire encore. 🌧️\n", replacementRange: page.editor.selectedRange())
        check("Newline after Unicode", size: 17)
        page.editor.insertText("Still on the line.", replacementRange: page.editor.selectedRange())
        check("Insertion after a long paste", size: 17)
        let text = page.editor.string
        page.configure(id: id, text: text, size: 24, theme: .linen, spotlight: true, sounds: false, volume: 0, editable: true)
        check("Type size changes", size: 24)
        window.setContentSize(NSSize(width: 760, height: 540))
        check("Small window reflows the document", size: 24)
        page.editor.setSelectedRange(NSRange(location: 10, length: 0))
        check("Editing an earlier line", size: 24)
        configure("One short note.")
        check("Switching to a short note", size: 17)
        window.close()
        print("\(count - failures)/\(count) native layout checks passed")
        exit(failures == 0 ? 0 : 1)
    }
}
