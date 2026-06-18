import AppKit
import ScreenSaver
import SwiftUI

/// User settings for the macOS screensaver, persisted via ScreenSaverDefaults (the
/// sandbox-correct prefs store for .saver bundles). Mirrors the Windows options.
struct FAConfig {
    var logoCount = 24
    var speed = 50
    var size = 50
    var trailLength = 55
    var background = 0x00_0000 // RGB; the engine forces it opaque
    var clock = 4              // 0=Off 1=TopLeft 2=TopRight 3=BottomLeft 4=BottomRight

    private static func store(_ moduleName: String) -> ScreenSaverDefaults? {
        ScreenSaverDefaults(forModuleWithName: moduleName)
    }

    static func load(moduleName: String) -> FAConfig {
        guard let d = store(moduleName) else { return FAConfig() }
        d.register(defaults: [
            "LogoCount": 24, "Speed": 50, "Size": 50,
            "TrailLength": 55, "Background": 0x00_0000, "Clock": 4,
        ])
        return FAConfig(
            logoCount: d.integer(forKey: "LogoCount"),
            speed: d.integer(forKey: "Speed"),
            size: d.integer(forKey: "Size"),
            trailLength: d.integer(forKey: "TrailLength"),
            background: d.integer(forKey: "Background"),
            clock: d.integer(forKey: "Clock"))
    }

    func save(moduleName: String) {
        guard let d = FAConfig.store(moduleName) else { return }
        d.set(logoCount, forKey: "LogoCount")
        d.set(speed, forKey: "Speed")
        d.set(size, forKey: "Size")
        d.set(trailLength, forKey: "TrailLength")
        d.set(background, forKey: "Background")
        d.set(clock, forKey: "Clock")
        d.synchronize()
    }

    /// Background packed as opaque ARGB for the engine's fa_create.
    var backgroundArgb: Int32 {
        Int32(bitPattern: 0xFF00_0000 | (UInt32(background) & 0x00FF_FFFF))
    }
}

/// Hosts the SwiftUI settings view in an NSWindow returned from `configureSheet`.
final class ConfigController {
    let window: NSWindow

    init(moduleName: String) {
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 380, height: 320),
                          styleMask: [.titled], backing: .buffered, defer: true)
        window.title = "Flying Azure"

        let root = ConfigView(
            config: FAConfig.load(moduleName: moduleName),
            onSave: { [weak window] cfg in cfg.save(moduleName: moduleName); ConfigController.end(window) },
            onCancel: { [weak window] in ConfigController.end(window) })
        window.contentViewController = NSHostingController(rootView: root)
    }

    private static func end(_ window: NSWindow?) {
        guard let window else { return }
        if let parent = window.sheetParent { parent.endSheet(window) } else { window.orderOut(nil) }
    }
}

private struct ConfigView: View {
    @State private var config: FAConfig
    let onSave: (FAConfig) -> Void
    let onCancel: () -> Void

    init(config: FAConfig, onSave: @escaping (FAConfig) -> Void, onCancel: @escaping () -> Void) {
        _config = State(initialValue: config)
        self.onSave = onSave
        self.onCancel = onCancel
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Flying Azure").font(.headline)
            sliderRow("Logos", value: $config.logoCount, range: 1...80)
            sliderRow("Speed", value: $config.speed, range: 0...100)
            sliderRow("Size", value: $config.size, range: 0...100)
            sliderRow("Trail length", value: $config.trailLength, range: 0...100)
            HStack {
                Text("Background").frame(width: 92, alignment: .leading)
                ColorPicker("", selection: backgroundBinding, supportsOpacity: false).labelsHidden()
                Spacer()
            }
            HStack {
                Text("Clock").frame(width: 92, alignment: .leading)
                Picker("", selection: $config.clock) {
                    Text("Off").tag(0)
                    Text("Top left").tag(1)
                    Text("Top right").tag(2)
                    Text("Bottom left").tag(3)
                    Text("Bottom right").tag(4)
                }.labelsHidden().frame(width: 160)
                Spacer()
            }
            Divider()
            HStack {
                Spacer()
                Button("Cancel") { onCancel() }.keyboardShortcut(.cancelAction)
                Button("OK") { onSave(config) }.keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(width: 380)
    }

    private func sliderRow(_ label: String, value: Binding<Int>, range: ClosedRange<Int>) -> some View {
        HStack {
            Text(label).frame(width: 92, alignment: .leading)
            Slider(value: Binding(get: { Double(value.wrappedValue) },
                                  set: { value.wrappedValue = Int($0.rounded()) }),
                   in: Double(range.lowerBound)...Double(range.upperBound))
            Text("\(value.wrappedValue)").frame(width: 34, alignment: .trailing).monospacedDigit()
        }
    }

    private var backgroundBinding: Binding<Color> {
        Binding(
            get: {
                let v = config.background
                return Color(.sRGB,
                             red: Double((v >> 16) & 0xFF) / 255,
                             green: Double((v >> 8) & 0xFF) / 255,
                             blue: Double(v & 0xFF) / 255)
            },
            set: { newColor in
                let ns = NSColor(newColor).usingColorSpace(.sRGB) ?? .black
                let r = Int((ns.redComponent * 255).rounded())
                let g = Int((ns.greenComponent * 255).rounded())
                let b = Int((ns.blueComponent * 255).rounded())
                config.background = (r << 16) | (g << 8) | b
            })
    }
}
