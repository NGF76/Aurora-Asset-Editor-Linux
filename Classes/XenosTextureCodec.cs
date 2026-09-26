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

        var pitch = ((width + 63) / 64) * 64;
        var storageHeight = ((height + 31) / 32) * 32;
        if (video.Length == checked(pitch * storageHeight * 4))
        {
            argb = new byte[checked(width * height * 4)];
            var rowBytes = checked(width * 4);
            for (var y = 0; y < height; y++)
            {
                Buffer.BlockCopy(video, y * pitch * 4, argb, y * rowBytes, rowBytes);
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

        var pitch = ((width + 63) / 64) * 64;
        var storageHeight = ((height + 31) / 32) * 32;
        var padded = new byte[checked(pitch * storageHeight * 4)];
        var rowBytes = checked(width * 4);
        for (var y = 0; y < height; y++)
            Buffer.BlockCopy(argb, y * rowBytes, padded, y * pitch * 4, rowBytes);

        video = padded;
        return true;
    }
}
