import AppKit

let size = NSSize(width: 1024, height: 1024)
let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: 1024, pixelsHigh: 1024, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: bitmap)
let tile = NSBezierPath(roundedRect: NSRect(x: 70, y: 70, width: 884, height: 884), xRadius: 195, yRadius: 195)
NSColor(red: 0.22, green: 0.25, blue: 0.24, alpha: 1).setFill(); tile.fill()
let page = NSBezierPath(roundedRect: NSRect(x: 275, y: 370, width: 474, height: 438), xRadius: 14, yRadius: 14)
NSColor(red: 0.92, green: 0.89, blue: 0.78, alpha: 1).setFill(); page.fill()
NSColor(red: 0.35, green: 0.36, blue: 0.31, alpha: 1).setFill()
for (y, width): (CGFloat, CGFloat) in [(704, 296), (652, 296), (600, 180)] {
    NSBezierPath(roundedRect: NSRect(x: 353, y: y, width: width, height: 10), xRadius: 5, yRadius: 5).fill()
}
let body = NSBezierPath(roundedRect: NSRect(x: 200, y: 230, width: 624, height: 230), xRadius: 48, yRadius: 48)
NSColor(red: 0.64, green: 0.66, blue: 0.59, alpha: 1).setFill(); body.fill()
NSColor(red: 0.15, green: 0.18, blue: 0.17, alpha: 1).setFill()
NSBezierPath(roundedRect: NSRect(x: 175, y: 440, width: 674, height: 38), xRadius: 15, yRadius: 15).fill()
for row in 0..<3 {
    for column in 0..<9 {
        let x = 280 + CGFloat(column) * 52 + (row % 2 == 0 ? 0 : 12)
        NSBezierPath(ovalIn: NSRect(x: x, y: 365 - CGFloat(row) * 42, width: 23, height: 23)).fill()
    }
}
NSBezierPath(roundedRect: NSRect(x: 386, y: 252, width: 252, height: 16), xRadius: 8, yRadius: 8).fill()
NSColor(red: 0.95, green: 0.90, blue: 0.69, alpha: 1).setFill()
NSBezierPath(roundedRect: NSRect(x: 573, y: 429, width: 91, height: 55), xRadius: 22, yRadius: 22).fill()
NSGraphicsContext.restoreGraphicsState()
try bitmap.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
