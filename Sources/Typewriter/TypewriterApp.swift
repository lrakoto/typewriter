import SwiftUI
import AppKit

@main
struct TypewriterApp: App {
    @StateObject private var model = AppModel()
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    var body: some Scene {
        Window("Typewriter", id: "main") {
            ContentView()
                .environmentObject(model)
                .frame(minWidth: 760, minHeight: 540)
                .onAppear { delegate.model = model }
                .onReceive(NotificationCenter.default.publisher(for: NSApplication.willResignActiveNotification)) { _ in model.flush() }
                .alert("A note from Typewriter", isPresented: Binding(get: { model.errorMessage != nil }, set: { if !$0 { model.errorMessage = nil } })) {
                    Button("OK") { model.errorMessage = nil }
                    Button("Open Notes Folder") { model.revealNotes(); model.errorMessage = nil }
                } message: { Text(model.errorMessage ?? "") }
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1120, height: 780)
        .commands {
            CommandGroup(replacing: .newItem) {
                Button("New Note") { model.createNote() }.keyboardShortcut("n")
                Button("Import Text…") { model.importNote() }.keyboardShortcut("o")
            }
            CommandGroup(replacing: .saveItem) {
                Button("Save Now") { model.flush() }.keyboardShortcut("s")
                Button("Export Note…") { model.exportNote() }.keyboardShortcut("e", modifiers: [.command, .shift]).disabled(model.selectedNote == nil)
            }
            CommandGroup(after: .sidebar) {
                Button(model.libraryVisible ? "Hide Library" : "Show Library") { model.libraryVisible.toggle() }
                    .keyboardShortcut("l", modifiers: [.command, .shift])
                Button("Enter Full Screen") { NSApp.keyWindow?.toggleFullScreen(nil) }
                    .keyboardShortcut("f", modifiers: [.control, .command])
            }
            CommandMenu("Note") {
                Button("Move to Trash") { model.trashSelected() }
                    .disabled(model.selectedNote == nil || model.selectedNote?.deletedAt != nil)
                Button("Restore Note") { model.restoreSelected() }.disabled(model.selectedNote?.deletedAt == nil)
                Divider()
                Button("Show Notes in Finder") { model.revealNotes() }
            }
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var model: AppModel?
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
    }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        model?.flush()
        if model?.saveStatus == "Could not save" {
            let alert = NSAlert()
            alert.messageText = "Your latest changes have not been saved."
            alert.informativeText = "Keep Typewriter open to retry saving, or quit without saving the latest changes."
            alert.addButton(withTitle: "Keep Writing")
            alert.addButton(withTitle: "Quit Anyway")
            return alert.runModal() == .alertFirstButtonReturn ? .terminateCancel : .terminateNow
        }
        return .terminateNow
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}
