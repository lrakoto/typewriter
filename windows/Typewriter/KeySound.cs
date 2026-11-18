using System.IO;
using System.Runtime.InteropServices;

namespace Typewriter.Windows;

internal static class KeySound
{
    private static double cachedVolume = -1;
    private static byte[][] clicks = [];
    private static byte[] carriage = [];
    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] sound, IntPtr module, uint flags);
    public static void Play(bool returnKey, double volume)
    {
        if (volume <= 0) return;
        if (Math.Abs(cachedVolume - volume) > 0.001) {
            cachedVolume = volume;
            clicks = Enumerable.Range(0, 5).Select(index => CreateWave((uint)(index + 41), false, volume)).ToArray();
            carriage = CreateWave(82, true, volume);
        }
        var bytes = returnKey ? carriage : clicks[Random.Shared.Next(clicks.Length)];
        // A synchronous call on a worker keeps the managed WAV buffer alive
        // throughout playback without blocking the writing thread.
        _ = Task.Run(() => PlaySound(bytes, IntPtr.Zero, 0x0004 | 0x0002));
    }
    private static byte[] CreateWave(uint seed, bool isReturn, double volume)
    {
        var length = isReturn ? 6500 : 1900;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + length * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
        writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(length * 2);
        var state = seed;
        for (int index = 0; index < length; index++) {
            var t = index / 44100.0; state = unchecked(1664525 * state + 1013904223);
            var noise = state / (double)uint.MaxValue * 2 - 1;
            var body = Math.Sin(t * 2 * Math.PI * (isReturn ? 210 : 760 + seed * 6)) * Math.Exp(-t * 110);
            var click = noise * Math.Exp(-t * (isReturn ? 48 : 190));
            var bell = isReturn ? Math.Sin(t * 2 * Math.PI * 2400) * Math.Exp(-t * 25) * 0.15 : 0;
            var sample = (click * 0.55 + body * 0.25 + bell) * Math.Min(1, t / 0.001) * volume;
            writer.Write((short)(Math.Clamp(sample, -1, 1) * short.MaxValue));
        }
        return stream.ToArray();
    }
}
