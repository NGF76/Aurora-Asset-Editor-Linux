using System;
using System.Buffers.Binary;

namespace AuroraAssetEditorLinux.Classes;

internal static class XenosTextureCodec
{
    private const int HeaderSize = 52;

    public static bool Decode(byte[] header, byte[] video, out byte[] argb, out int width, out int height)
    {
        argb = Array.Empty<byte>();
        width = 0;
        height = 0;
        if (header.Length != HeaderSize || video.Length == 0)
            return false;

        var dimensions = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(36, 4));
        width = (int)(dimensions & 0x1fff) + 1;
        height = (int)((dimensions >> 13) & 0x1fff) + 1;
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
            return false;

        var pitch = ((width + 31) / 32) * 32;
        if (video.Length == checked(pitch * height * 4))
        {
            argb = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var source = TiledAddress2D(x, y, pitch);
                    var destination = (y * width + x) * 4;
                    Buffer.BlockCopy(video, source, argb, destination, 4);
                }
            }

            return true;
        }

        if (video.Length == checked(width * height * 4))
        {
            argb = (byte[])video.Clone();
            return true;
        }

        // Compressed Xenos blocks require the texture endian mode from the
        // fetch header in addition to the tile address. Do not guess here.
        return false;
    }

    public static bool Encode(byte[] argb, int width, int height, bool useCompression,
        out byte[] header, out byte[] video)
    {
        header = Array.Empty<byte>();
        video = Array.Empty<byte>();
        if (width <= 0 || height <= 0 || argb.Length != checked(width * height * 4))
            return false;

        var dimensions = (uint)(width - 1) | ((uint)(height - 1) << 13);
        header = new byte[HeaderSize];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 3);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20, 4), 0xffff0000);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(24, 4), 0xffff0000);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(28, 4), useCompression ? 0x08000002u : 0x07800002u);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(32, 4), useCompression ? 0x54u : 0x86u);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(36, 4), dimensions);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(40, 4), useCompression ? 0x0d10u : 0x0c14u);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(48, 4), 0x0a00u);

        if (useCompression)
            return false;

        var pitch = ((width + 31) / 32) * 32;
        var tiled = new byte[checked(pitch * height * 4)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var source = (y * width + x) * 4;
                var destination = TiledAddress2D(x, y, pitch);
                if (destination < 0 || destination > tiled.Length - 4)
                    return false;

                Buffer.BlockCopy(argb, source, tiled, destination, 4);
            }
        }

        video = tiled;
        return true;
    }

    private static int TiledAddress2D(int x, int y, int pitch)
    {
        var macro = ((x >> 5) + (y >> 5) * (pitch >> 5)) << 9;
        var micro = (((y & 0x1f) >> 1) << 3) + (x & 7);
        var offset = macro + ((micro << 2) & 0xf) + (((micro << 2) & 0xf0) << 1);
        return (offset & ~0x1ff) + ((offset & 0xf0) >> 1) + (offset & 0xf) + ((offset & 0x100) >> 1);
    }

}
