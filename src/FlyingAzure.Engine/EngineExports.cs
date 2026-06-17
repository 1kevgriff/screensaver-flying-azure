using System.Runtime.InteropServices;
using FlyingAzure.Core;
using SkiaSharp;

namespace FlyingAzure.Engine;

/// <summary>
/// The C ABI the native hosts call. NativeAOT exports each <see cref="UnmanagedCallersOnlyAttribute"/>
/// method with a non-empty EntryPoint as a plain C function in the shared library, so a Swift
/// ScreenSaverView (macOS) or a Win32/WinForms host (Windows) can drive the engine with no .NET
/// knowledge. Contract:
///   nint fa_create(w, h, logoCount, speed, size, trail, backgroundArgb, clockCorner)
///   void fa_render(handle, byte* rgbaBuffer, int bufferLen, double dtSeconds)  // RGBA8888 premultiplied
///   void fa_destroy(handle)
/// The buffer is width*height*4 bytes, row-major, no padding (stride = width*4).
/// </summary>
public static class EngineExports
{
    private sealed class Instance(EngineRenderer renderer, SKBitmap bitmap)
    {
        public EngineRenderer Renderer { get; } = renderer;
        public SKBitmap Bitmap { get; } = bitmap;
    }

    private static readonly Lock Gate = new();
    private static readonly Dictionary<nint, Instance> Instances = [];
    private static nint _nextId = 1;

    [UnmanagedCallersOnly(EntryPoint = "fa_create")]
    public static nint Create(int width, int height, int logoCount, int speed, int size,
        int trailLength, int backgroundArgb, int clockCorner)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        try
        {
            var settings = new Settings
            {
                LogoCount = logoCount,
                Speed = speed,
                Size = size,
                TrailLength = trailLength,
                BackgroundArgb = backgroundArgb,
                Clock = (ClockCorner)clockCorner,
            };

            var renderer = new EngineRenderer(width, height, settings, new Random());
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));

            lock (Gate)
            {
                nint id = _nextId++;
                Instances[id] = new Instance(renderer, bitmap);
                return id;
            }
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "fa_render")]
    public static unsafe void Render(nint handle, byte* buffer, int bufferLen, double dtSeconds)
    {
        if (buffer is null)
        {
            return;
        }

        Instance? instance;
        lock (Gate)
        {
            Instances.TryGetValue(handle, out instance);
        }

        if (instance is null)
        {
            return;
        }

        instance.Renderer.Step(dtSeconds);
        using (var canvas = new SKCanvas(instance.Bitmap))
        {
            instance.Renderer.Render(canvas);
        }

        nint src = instance.Bitmap.GetPixels();
        if (src == 0)
        {
            return;
        }

        int copy = Math.Min(bufferLen, instance.Bitmap.ByteCount);
        Buffer.MemoryCopy((void*)src, buffer, bufferLen, copy);
    }

    [UnmanagedCallersOnly(EntryPoint = "fa_destroy")]
    public static void Destroy(nint handle)
    {
        Instance? instance;
        lock (Gate)
        {
            Instances.Remove(handle, out instance);
        }

        instance?.Bitmap.Dispose();
        instance?.Renderer.Dispose();
    }
}
