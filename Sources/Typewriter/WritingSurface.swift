import AppKit
import SwiftUI

struct WritingSurface: NSViewRepresentable {
    let noteID: UUID
    let text: String
    let fontSize: Double
    let theme: PaperTheme
    let spotlight: Bool
    let sounds: Bool
    let volume: Double
    let editable: Bool
    let onChange: (String) -> Void

    func makeNSView(context: Context) -> TypewriterPage {
        let page = TypewriterPage()
        page.onChange = onChange
        return page
    }

    func updateNSView(_ page: TypewriterPage, context: Context) {
        page.onChange = onChange
        page.configure(id: noteID, text: text, size: fontSize, theme: theme,
                       spotlight: spotlight, sounds: sounds, volume: volume, editable: editable)
    }
}

final class TypewriterPage: NSView, NSTextViewDelegate {
    override var isFlipped: Bool { true }
    let scrollView = NSScrollView()
    let editor = TypewriterTextView()
    var onChange: ((String) -> Void)?
    private var noteID: UUID?
    private var theme: PaperTheme = .linen
    private var spotlight = true
    private var fontSize: CGFloat = 17
    private var updating = false
    private var cursorX: CGFloat = 0
    private var targetX: CGFloat = 0
    private var motionTimer: Timer?
    private var lastSize: NSSize = .zero
    private var initialSelection = false
    private var railY: CGFloat { bounds.height * 0.73 }
    private var lineTargetY: CGFloat { railY - 37 }

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        scrollView.drawsBackground = false
        scrollView.hasVerticalScroller = false
        scrollView.hasHorizontalScroller = false
        scrollView.verticalScrollElasticity = .none
        scrollView.horizontalScrollElasticity = .none
        scrollView.borderType = .noBorder
        scrollView.automaticallyAdjustsContentInsets = false
        editor.isRichText = false
        editor.isAutomaticQuoteSubstitutionEnabled = false
        editor.isAutomaticDashSubstitutionEnabled = false
        editor.isAutomaticTextReplacementEnabled = false
        editor.isAutomaticSpellingCorrectionEnabled = false
        editor.isContinuousSpellCheckingEnabled = false
        editor.isGrammarCheckingEnabled = false
        editor.isIncrementalSearchingEnabled = true
        editor.usesFindBar = true
        editor.allowsUndo = true
        editor.drawsBackground = false
        editor.isHorizontallyResizable = false
        editor.isVerticallyResizable = true
        editor.autoresizingMask = [.width]
        editor.minSize = .zero
        editor.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        editor.textContainer?.widthTracksTextView = true
        editor.textContainer?.lineFragmentPadding = 0
        editor.delegate = self
        editor.setAccessibilityLabel("Note text")
        scrollView.documentView = editor
        addSubview(scrollView)
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    deinit { motionTimer?.invalidate() }

    func configure(id: UUID, text: String, size: Double, theme: PaperTheme, spotlight: Bool, sounds: Bool, volume: Double, editable: Bool) {
        let switched = noteID != id
        let styleChanged = fontSize != size || self.theme != theme
        self.theme = theme
        self.spotlight = spotlight
        self.fontSize = size
        editor.soundEnabled = sounds
        editor.soundVolume = Float(volume)
        editor.isEditable = editable
        editor.isSelectable = true
        updating = true
        if switched {
            noteID = id
            editor.string = text
            editor.undoManager?.removeAllActions()
            editor.setSelectedRange(NSRange(location: (text as NSString).length, length: 0))
            initialSelection = true
        }
        if switched || styleChanged {
            applyTypography()
            needsLayout = true
        }
        updating = false
        needsDisplay = true
    }

    private func applyTypography() {
        let font = NSFont(name: "Courier", size: fontSize) ?? .monospacedSystemFont(ofSize: fontSize, weight: .regular)
        let paragraph = NSMutableParagraphStyle()
        paragraph.minimumLineHeight = fontSize * 1.7
        paragraph.maximumLineHeight = fontSize * 1.7
        paragraph.lineBreakMode = .byWordWrapping
        editor.font = font
        editor.textColor = theme.ink
        editor.insertionPointColor = theme.ink
        editor.defaultParagraphStyle = paragraph
        let attributes: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: theme.ink, .paragraphStyle: paragraph, .kern: 0.2]
        editor.typingAttributes = attributes
        if editor.textStorage!.length > 0 { editor.textStorage?.setAttributes(attributes, range: NSRange(location: 0, length: editor.textStorage!.length)) }
        editor.selectedTextAttributes = [.backgroundColor: theme.ink.withAlphaComponent(0.15)]
    }

    override func layout() {
        super.layout()
        guard bounds.width > 0, bounds.height > 0 else { return }
        updating = true
        let changed = lastSize != bounds.size
        lastSize = bounds.size
        scrollView.frame = bounds
        let margin = max(52, (bounds.width - 680) / 2)
        editor.textContainerInset = NSSize(width: margin, height: max(150, lineTargetY))
        editor.setFrameSize(NSSize(width: bounds.width, height: max(editor.frame.height, bounds.height * 2)))
        editor.textContainer?.containerSize = NSSize(width: max(100, bounds.width - margin * 2), height: .greatestFiniteMagnitude)
        editor.layoutManager?.ensureLayout(for: editor.textContainer!)
        editor.sizeToFit()
        editor.setFrameSize(NSSize(width: bounds.width, height: max(editor.frame.height, bounds.height * 2)))
        updating = false
        if changed || initialSelection { pinCursor(animated: !initialSelection) }
        if initialSelection {
            initialSelection = false
            DispatchQueue.main.async { [weak self] in
                guard let self = self else { return }
                self.window?.makeFirstResponder(self.editor)
                self.pinCursor(animated: false)
            }
        }
        needsDisplay = true
    }

    func textDidChange(_ notification: Notification) {
        guard !updating else { return }
        onChange?(editor.string)
        pinCursor(animated: true)
    }

    func textViewDidChangeSelection(_ notification: Notification) {
        guard !updating else { return }
        pinCursor(animated: true)
    }

    private func pinCursor(animated: Bool) {
        guard bounds.width > 0, let manager = editor.layoutManager, let container = editor.textContainer else { return }
        manager.ensureLayout(for: container)
        let range = editor.selectedRange()
        let character = min(range.location, (editor.string as NSString).length)
        let rect: NSRect
        if character == (editor.string as NSString).length && (editor.string.isEmpty || editor.string.hasSuffix("\n")) {
            rect = manager.extraLineFragmentRect
        } else if manager.numberOfGlyphs > 0 {
            let glyph = manager.glyphIndexForCharacter(at: min(character, (editor.string as NSString).length - 1))
            let line = manager.lineFragmentRect(forGlyphAt: min(glyph, manager.numberOfGlyphs - 1), effectiveRange: nil)
            var x = manager.location(forGlyphAt: min(glyph, manager.numberOfGlyphs - 1)).x
            if character == (editor.string as NSString).length {
                let glyphBounds = manager.boundingRect(forGlyphRange: NSRange(location: manager.numberOfGlyphs - 1, length: 1), in: container)
                x = glyphBounds.maxX
            }
            rect = NSRect(x: x, y: line.minY, width: 2, height: line.height)
        } else { rect = .zero }
        let y = editor.textContainerOrigin.y + rect.minY
        let desiredScroll = max(0, y - lineTargetY)
        let contentHeight = max(bounds.height * 2, editor.textContainerOrigin.y * 2 + manager.usedRect(for: container).height + bounds.height)
        if abs(editor.frame.height - contentHeight) > 1 { editor.setFrameSize(NSSize(width: bounds.width, height: contentHeight)) }
        scrollView.contentView.scroll(to: NSPoint(x: 0, y: desiredScroll))
        scrollView.reflectScrolledClipView(scrollView.contentView)
        targetX = max(35, min(bounds.width - 35, editor.textContainerOrigin.x + rect.minX))
        if !animated || cursorX == 0 {
            cursorX = targetX
        } else if motionTimer == nil {
            motionTimer = Timer.scheduledTimer(withTimeInterval: 1.0 / 60.0, repeats: true) { [weak self] timer in
                guard let self = self else { timer.invalidate(); return }
                self.cursorX += (self.targetX - self.cursorX) * 0.27
                if abs(self.targetX - self.cursorX) < 0.3 {
                    self.cursorX = self.targetX
                    timer.invalidate()
                    self.motionTimer = nil
                }
                self.needsDisplay = true
            }
        }
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        theme.paper.setFill()
        bounds.fill()
        guard let context = NSGraphicsContext.current?.cgContext else { return }
        let center = CGPoint(x: cursorX == 0 ? bounds.midX : cursorX, y: railY - 110)
        if spotlight {
            let dark = theme == .midnight
            let colors = [
                NSColor(red: 1, green: 0.91, blue: 0.62, alpha: dark ? 0.14 : 0.38).cgColor,
                NSColor(red: 1, green: 0.94, blue: 0.76, alpha: dark ? 0.05 : 0.17).cgColor,
                NSColor(white: 0.06, alpha: dark ? 0.0 : 0.05).cgColor,
                NSColor(white: 0.02, alpha: dark ? 0.26 : 0.19).cgColor
            ] as CFArray
            if let gradient = CGGradient(colorsSpace: CGColorSpaceCreateDeviceRGB(), colors: colors, locations: [0, 0.35, 0.72, 1]) {
                context.drawRadialGradient(gradient, startCenter: center, startRadius: 0,
                                           endCenter: center, endRadius: max(440, bounds.width * 0.75), options: [.drawsAfterEndLocation])
            }
        }
        // Fine horizontal fibers keep the paper tactile without image assets.
        context.setLineWidth(0.5)
        for row in stride(from: CGFloat(0), to: bounds.height, by: 3) {
            context.setStrokeColor(theme.ink.withAlphaComponent(0.012).cgColor)
            context.move(to: CGPoint(x: 0, y: row)); context.addLine(to: CGPoint(x: bounds.width, y: row)); context.strokePath()
        }
        drawRail()
    }

    private func drawRail() {
        let dark = theme == .midnight
        let rail = NSRect(x: 0, y: railY, width: bounds.width, height: 6)
        NSColor(white: dark ? 0.04 : 0.25, alpha: 0.65).setFill()
        rail.fill()
        NSColor(white: 1, alpha: 0.28).setFill()
        NSRect(x: 0, y: railY + 1, width: bounds.width, height: 1).fill()
        NSColor(white: 0, alpha: 0.10).setFill()
        NSRect(x: 0, y: railY + 6, width: bounds.width, height: 3).fill()
        let x = cursorX == 0 ? bounds.midX : cursorX
        theme.ink.withAlphaComponent(0.65).setFill()
        NSRect(x: x - 0.7, y: railY - 12, width: 1.4, height: 13).fill()
        NSRect(x: x - 4, y: railY - 13, width: 8, height: 1.4).fill()
        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow()
        shadow.shadowColor = NSColor.black.withAlphaComponent(0.24)
        shadow.shadowOffset = NSSize(width: 0, height: -2)
        shadow.shadowBlurRadius = 4
        shadow.set()
        let carriage = NSBezierPath(roundedRect: NSRect(x: x - 23, y: railY - 3, width: 46, height: 16), xRadius: 8, yRadius: 8)
        NSColor(white: dark ? 0.36 : 0.76, alpha: 1).setFill()
        carriage.fill()
        NSGraphicsContext.restoreGraphicsState()
        NSColor.white.withAlphaComponent(0.32).setStroke()
        carriage.lineWidth = 0.7
        carriage.stroke()
        for offset: CGFloat in [-9, -3, 3] {
            NSColor(white: dark ? 0.7 : 0.35, alpha: 0.6).setFill()
            NSBezierPath(ovalIn: NSRect(x: x + offset, y: railY + 3.5, width: 1.8, height: 1.8)).fill()
        }
    }
}

final class TypewriterTextView: NSTextView {
    var soundEnabled = false
    var soundVolume: Float = 0.25
    override func keyDown(with event: NSEvent) {
        super.keyDown(with: event)
        if soundEnabled, !event.modifierFlags.contains(.command), !event.modifierFlags.contains(.control),
           let characters = event.characters, !characters.isEmpty {
            KeySound.shared.play(returnKey: event.keyCode == 36 || event.keyCode == 76, volume: soundVolume)
        }
    }
}
