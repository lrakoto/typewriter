import SwiftUI
import TypewriterCore

struct ContentView: View {
    @EnvironmentObject private var model: AppModel
    @AppStorage("paperTheme") private var themeName = "linen"
    @AppStorage("keySounds") private var sounds = false
    @AppStorage("spotlight") private var spotlight = true
    @AppStorage("fontSize") private var fontSize = 17.0
    @AppStorage("soundVolume") private var volume = 0.25
    @State private var settingsVisible = false
    @State private var focused = false
    private var theme: PaperTheme { PaperTheme(rawValue: themeName) ?? .linen }

    var body: some View {
        HStack(spacing: 0) {
            if model.libraryVisible {
                library.frame(width: 260)
                Rectangle().fill(Color(theme.ink).opacity(0.10)).frame(width: 1)
            }
            ZStack {
                Color(theme.paper)
                if let note = model.selectedNote {
                    WritingSurface(noteID: note.id, text: note.text, fontSize: fontSize, theme: theme,
                                   spotlight: spotlight, sounds: sounds, volume: volume,
                                   editable: note.deletedAt == nil, onChange: model.updateText)
                        .accessibilityLabel("Writing page")
                    VStack(spacing: 0) {
                        header(note).padding(.top, 35).padding(.horizontal, 34)
                        Spacer()
                        footer(note).padding(.horizontal, 34).padding(.bottom, 23)
                    }
                    .opacity(focused ? 0 : 1)
                    .allowsHitTesting(!focused)
                } else {
                    VStack(spacing: 18) {
                        Image(systemName: "text.cursor").font(.system(size: 32, weight: .ultraLight))
                        Text("A little room to think.").font(.system(size: 25, design: .serif))
                        Button("Begin a new note") { model.createNote() }.buttonStyle(.bordered)
                    }.foregroundStyle(Color(theme.ink))
                    VStack { HStack { libraryButton; Spacer() }; Spacer() }.padding(34)
                }
                if focused {
                    VStack { HStack { Spacer(); Button { focused = false } label: { Image(systemName: "arrow.down.right.and.arrow.up.left") }.help("Leave focus mode (Escape)").buttonStyle(QuietButton()) }; Spacer() }.padding(28)
                }
            }
        }
        .background(Color(theme.paper))
        .foregroundStyle(Color(theme.ink))
        .tint(theme.accent)
        .preferredColorScheme(theme == .midnight ? .dark : .light)
        .onExitCommand { focused = false }
    }

    private var libraryButton: some View {
        Button { withAnimation(.easeInOut(duration: 0.2)) { model.libraryVisible.toggle() } } label: {
            Image(systemName: "sidebar.left").font(.system(size: 16, weight: .regular))
        }.buttonStyle(QuietButton()).help("Library  ⇧⌘L").accessibilityLabel("Toggle library")
    }

    private func header(_ note: Note) -> some View {
        HStack(alignment: .top, spacing: 18) {
            libraryButton
            VStack(alignment: .leading, spacing: 7) {
                Text("TYPEWRITER").font(.system(size: 9, weight: .semibold, design: .monospaced)).tracking(3.5).opacity(0.45)
                TextField("Untitled", text: Binding(get: { model.selectedNote?.title ?? "" }, set: model.updateTitle))
                    .font(.system(size: 16, weight: .regular, design: .serif))
                    .textFieldStyle(.plain).frame(maxWidth: 380).disabled(note.deletedAt != nil)
                    .accessibilityLabel("Note title")
            }
            Spacer()
            if note.deletedAt != nil {
                Button("Restore note") { model.restoreSelected() }.buttonStyle(.bordered)
            }
            Button { focused = true; model.libraryVisible = false } label: {
                Image(systemName: "arrow.up.left.and.arrow.down.right").font(.system(size: 14))
            }.buttonStyle(QuietButton()).help("Focus mode · Escape to leave").accessibilityLabel("Focus mode")
        }
    }

    private func footer(_ note: Note) -> some View {
        HStack(spacing: 12) {
            Text("\(note.wordCount) \(note.wordCount == 1 ? "word" : "words")")
            Circle().frame(width: 2, height: 2)
            Text(model.saveStatus)
            Spacer()
            Button { sounds.toggle() } label: { Image(systemName: sounds ? "speaker.wave.2" : "speaker.slash") }
                .help(sounds ? "Mute key sounds" : "Enable key sounds").accessibilityLabel(sounds ? "Mute key sounds" : "Enable key sounds")
            Button { spotlight.toggle() } label: { Image(systemName: spotlight ? "lightbulb" : "lightbulb.slash") }
                .help("Toggle spotlight").accessibilityLabel("Toggle spotlight")
            Button { settingsVisible.toggle() } label: { Image(systemName: "slider.horizontal.3") }
                .help("Writing preferences").accessibilityLabel("Writing preferences")
                .popover(isPresented: $settingsVisible, arrowEdge: .top) { preferences }
        }
        .font(.system(size: 10, design: .monospaced))
        .foregroundStyle(Color(theme.ink).opacity(0.55))
        .buttonStyle(QuietButton())
    }

    private var preferences: some View {
        VStack(alignment: .leading, spacing: 20) {
            Text("Make yourself at home.").font(.system(size: 19, design: .serif))
            VStack(alignment: .leading, spacing: 10) {
                Text("PAPER").font(.system(size: 9, weight: .semibold)).tracking(2).opacity(0.5)
                Picker("Paper", selection: $themeName) {
                    ForEach(PaperTheme.allCases) { item in Text(item.label).tag(item.rawValue) }
                }.pickerStyle(.segmented).labelsHidden()
            }
            HStack { Text("Type size"); Slider(value: $fontSize, in: 14...24, step: 1); Text("\(Int(fontSize))").monospacedDigit().frame(width: 23) }
            Toggle("Follow the cursor with light", isOn: $spotlight)
            Toggle("Mechanical key sounds", isOn: $sounds)
            HStack { Image(systemName: "speaker.wave.1"); Slider(value: $volume, in: 0...0.7); Image(systemName: "speaker.wave.3") }.disabled(!sounds)
            Divider()
            Text("Your words stay on this Mac.\nSaved automatically, as you write.").font(.system(size: 11)).foregroundStyle(.secondary).lineSpacing(4)
            Button("Open notes folder") { model.revealNotes() }.font(.system(size: 11))
        }.font(.system(size: 13)).foregroundStyle(Color.primary).padding(24).frame(width: 330)
    }

    private var library: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("Your pages").font(.system(size: 22, design: .serif))
                Spacer()
                Button { model.createNote() } label: { Image(systemName: "square.and.pencil") }.buttonStyle(QuietButton()).help("New note  ⌘N").accessibilityLabel("New note")
            }.padding(.top, 50).padding(.horizontal, 23).padding(.bottom, 22)
            HStack { Image(systemName: "magnifyingglass").opacity(0.4); TextField("Find a thought…", text: $model.search).textFieldStyle(.plain).accessibilityLabel("Search notes") }
                .font(.system(size: 12)).padding(10).background(Color(theme.ink).opacity(0.045), in: RoundedRectangle(cornerRadius: 7)).padding(.horizontal, 17)
            HStack {
                Button("Notes") { model.showingTrash = false }.opacity(model.showingTrash ? 0.4 : 1)
                Spacer()
                Button { model.showingTrash.toggle() } label: { Label("Trash", systemImage: "trash").labelStyle(.titleAndIcon) }.opacity(model.showingTrash ? 1 : 0.4)
            }.font(.system(size: 10, weight: .medium)).buttonStyle(.plain).padding(.horizontal, 23).padding(.vertical, 19)
            ScrollView {
                LazyVStack(spacing: 5) {
                    ForEach(model.visibleNotes) { note in
                        Button { model.select(note) } label: {
                            VStack(alignment: .leading, spacing: 8) {
                                Text(note.title.isEmpty ? "Untitled" : note.title).font(.system(size: 14, weight: .medium, design: .serif)).lineLimit(1)
                                Text(note.preview).font(.system(size: 11)).lineSpacing(3).opacity(0.55).lineLimit(2).frame(maxWidth: .infinity, alignment: .leading)
                                Text(note.updatedAt, format: .dateTime.month(.abbreviated).day()).font(.system(size: 9, design: .monospaced)).opacity(0.35)
                            }.padding(14).frame(maxWidth: .infinity, alignment: .leading)
                                .background(Color(theme.ink).opacity(model.selectedID == note.id ? 0.07 : 0), in: RoundedRectangle(cornerRadius: 8))
                        }.buttonStyle(.plain)
                            .contextMenu {
                                if note.deletedAt == nil { Button("Move to Trash") { model.select(note); model.trashSelected() } }
                                else { Button("Restore Note") { model.select(note); model.restoreSelected() } }
                                Button("Export…") { model.select(note); model.exportNote() }
                            }
                    }
                    if model.visibleNotes.isEmpty {
                        Text(model.search.isEmpty ? (model.showingTrash ? "Nothing in the trash." : "A new page is waiting.") : "No matching notes.")
                            .font(.system(size: 12)).opacity(0.5).padding(25)
                    }
                }.padding(.horizontal, 10)
            }
            HStack {
                Text("LOCAL LIBRARY").font(.system(size: 8, weight: .medium, design: .monospaced)).tracking(1.8).opacity(0.35)
                Spacer()
                Button { model.importNote() } label: { Image(systemName: "square.and.arrow.down") }.buttonStyle(QuietButton()).help("Import text").accessibilityLabel("Import text")
            }.padding(23)
        }.background(Color(theme.paper).opacity(0.95))
    }
}

struct QuietButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.padding(5).contentShape(Rectangle()).opacity(configuration.isPressed ? 0.35 : 0.7)
    }
}
