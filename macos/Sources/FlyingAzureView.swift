import AppKit
import ScreenSaver

/// macOS screensaver shell. The animation itself is rendered by the shared cross-platform
/// engine (FlyingAzure.Engine, a NativeAOT C library) — this view just drives it and blits
/// the RGBA frame. The same engine backs the Windows .scr, so both platforms share one renderer.
@objc(FlyingAzureView)
final class FlyingAzureView: ScreenSaverView {
    private typealias CreateFn = @convention(c) (Int32, Int32, Int32, Int32, Int32, Int32, Int32, Int32) -> Int
    private typealias RenderFn = @convention(c) (Int, UnsafeMutablePointer<UInt8>, Int32, Double) -> Void
    private typealias DestroyFn = @convention(c) (Int) -> Void

    private var createFn: CreateFn?
    private var renderFn: RenderFn?
    private var destroyFn: DestroyFn?

    private var handle: Int = 0
    private var pixelWidth: Int = 0
    private var pixelHeight: Int = 0
    private var buffer = [UInt8]()
    private var lastTime: CFTimeInterval = 0
    private var activeConfig: ConfigController?

    private var moduleName: String {
        Bundle(for: type(of: self)).bundleIdentifier ?? "com.kevgriffin.flyingazure"
    }

    // System Settings shows an "Options…" button and presents this sheet.
    override var hasConfigureSheet: Bool { true }

    override var configureSheet: NSWindow? {
        let controller = ConfigController(moduleName: moduleName)
        activeConfig = controller // retain while the sheet is open
        return controller.window
    }

    override init?(frame: NSRect, isPreview: Bool) {
        super.init(frame: frame, isPreview: isPreview)
        animationTimeInterval = 1.0 / 60.0
        loadEngine()
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        animationTimeInterval = 1.0 / 60.0
        loadEngine()
    }

    /// dlopen the engine and its native deps from the .saver's Frameworks folder. Deps are
    /// preloaded in dependency order with RTLD_GLOBAL so the engine's SkiaSharp P/Invokes
    /// resolve to the already-loaded images regardless of the host process's library paths.
    private func loadEngine() {
        let frameworks = Bundle(for: type(of: self)).bundlePath + "/Contents/Frameworks/"
        _ = dlopen(frameworks + "libHarfBuzzSharp.dylib", RTLD_NOW | RTLD_GLOBAL)
        _ = dlopen(frameworks + "libSkiaSharp.dylib", RTLD_NOW | RTLD_GLOBAL)
        guard let engine = dlopen(frameworks + "FlyingAzure.Engine.dylib", RTLD_NOW | RTLD_GLOBAL) else {
            NSLog("FlyingAzure: failed to load engine dylib: \(String(cString: dlerror()))")
            return
        }

        if let sym = dlsym(engine, "fa_create") { createFn = unsafeBitCast(sym, to: CreateFn.self) }
        if let sym = dlsym(engine, "fa_render") { renderFn = unsafeBitCast(sym, to: RenderFn.self) }
        if let sym = dlsym(engine, "fa_destroy") { destroyFn = unsafeBitCast(sym, to: DestroyFn.self) }
    }

    private func createInstanceIfNeeded() {
        guard handle == 0, let create = createFn else { return }

        let scale = window?.backingScaleFactor ?? 1.0
        pixelWidth = max(1, Int(bounds.width * scale))
        pixelHeight = max(1, Int(bounds.height * scale))
        buffer = [UInt8](repeating: 0, count: pixelWidth * pixelHeight * 4)

        let cfg = FAConfig.load(moduleName: moduleName)
        handle = create(Int32(pixelWidth), Int32(pixelHeight),
                        Int32(cfg.logoCount), Int32(cfg.speed), Int32(cfg.size),
                        Int32(cfg.trailLength), cfg.backgroundArgb, Int32(cfg.clock))
        lastTime = CACurrentMediaTime()
    }

    override func startAnimation() {
        super.startAnimation()
        createInstanceIfNeeded()
    }

    override func stopAnimation() {
        super.stopAnimation()
        destroyInstance()
    }

    override func animateOneFrame() {
        guard let render = renderFn, handle != 0 else { return }
        let now = CACurrentMediaTime()
        let dt = min(now - lastTime, 0.1)
        lastTime = now
        buffer.withUnsafeMutableBufferPointer { ptr in
            if let base = ptr.baseAddress {
                render(handle, base, Int32(ptr.count), dt)
            }
        }
        setNeedsDisplay(bounds)
    }

    override func draw(_ rect: NSRect) {
        guard handle != 0, pixelWidth > 0, pixelHeight > 0 else {
            NSColor.black.setFill()
            bounds.fill()
            return
        }

        buffer.withUnsafeMutableBytes { raw in
            guard let base = raw.baseAddress,
                  let nsContext = NSGraphicsContext.current?.cgContext else { return }

            // Engine frames are RGBA8888 premultiplied.
            let info = CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue
            guard let ctx = CGContext(
                data: base, width: pixelWidth, height: pixelHeight,
                bitsPerComponent: 8, bytesPerRow: pixelWidth * 4,
                space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: info),
                let image = ctx.makeImage() else { return }

            // The screensaver view's draw context is already top-left oriented (matching
            // Skia's top-left frame), so blit the image directly — no vertical flip.
            nsContext.draw(image, in: bounds)
        }
    }

    private func destroyInstance() {
        if let destroy = destroyFn, handle != 0 {
            destroy(handle)
            handle = 0
        }
    }

    deinit {
        destroyInstance()
    }
}
