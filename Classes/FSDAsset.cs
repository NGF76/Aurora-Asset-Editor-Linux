using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using PhoenixTools;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AuroraAssetEditorLinux.Classes
{
    internal class FsdAsset
    {
        public enum FsdAssetType : uint
        {
            Thumbnail = 0x01,
            Background = 0x02,
            Banner = 0x04,
            Boxart = 0x08,
            Preview = 0x10,
            Screenshot = 0x20,
            Slot = 0x40,
            FullCover = 0x80
        }

        public readonly uint Magic;
        public readonly uint Version;
        public readonly uint Reserved;
        public readonly uint AssetFlags;
        public readonly uint AssetCount;
        public readonly uint ScreenshotCount;
        public readonly FsdAssetEntry[] Entries = new FsdAssetEntry[10];
        public readonly FsdScreenshotEntry[] Screenshots = new FsdScreenshotEntry[20];

        public FsdAsset(byte[] data)
        {
            if (data.Length < 0x1F8)
                throw new Exception("Invalid file size");

            Magic = Swap(BitConverter.ToUInt32(data, 0));
            if (Magic != 0x46534441)
                throw new Exception("Invalid asset file magic!");

            Version = Swap(BitConverter.ToUInt32(data, 4));
            if (Version != 1)
                throw new NotSupportedException("Unsupported asset file version!");

            Reserved = Swap(BitConverter.ToUInt32(data, 8));
            AssetFlags = Swap(BitConverter.ToUInt32(data, 12));
            AssetCount = Swap(BitConverter.ToUInt32(data, 16));
            ScreenshotCount = Swap(BitConverter.ToUInt32(data, 20));

            for (var i = 0; i < Entries.Length; i++)
                Entries[i] = new FsdAssetEntry(ref data, 24 + (i * 16));

            for (var i = 0; i < Screenshots.Length; i++)
                Screenshots[i] = new FsdScreenshotEntry(ref data, (24 + (16 * Entries.Length)) + (i * 16));
        }

        private static uint Swap(uint x)
        {
            return (x & 0x000000FF) << 24 | (x & 0x0000FF00) << 8 | (x & 0x00FF0000) >> 8 | (x & 0xFF000000) >> 24;
        }

        // ========== معالجة الصور باستخدام SixLabors.ImageSharp ==========

        private static Image? GetImage(byte[] data)
        {
            try
            {
                // التحقق من تنسيق DDS
                if (data[0] == 'D' && data[1] == 'D' && data[2] == 'S')
                {
                    var imageData = new byte[0];
                    int imageWidth, imageHeight;
                    if (AuroraAssetDll.ProcessDDSToImage(ref data, ref imageData, out imageWidth, out imageHeight))
                    {
                        return RawArgbToImage(imageData, imageWidth, imageHeight);
                    }
                    return null;
                }

                // تحميل الصورة من البيانات
                using var ms = new MemoryStream(data);
                return Image.Load(ms);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "FsdAsset.GetImage");
                return null;
            }
        }

        // ========== تحويل البيانات الخام إلى Image باستخدام ImageSharp ==========

        private static Image RawArgbToImage(byte[] raw, int width, int height)
        {
            var image = new Image<Rgba32>(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width + x) * 4;
                    image[x, y] = new Rgba32(raw[idx + 1], raw[idx + 2], raw[idx + 3], raw[idx]);
                }
            }
            return image;
        }

        // ========== دوال الحصول على الأصول ==========

        public Image? GetBanner()
        {
            return Entries
                .Where(entry => entry.AssetType == FsdAssetType.Banner && entry.Size > 0)
                .Select(entry => GetImage(entry.Data))
                .FirstOrDefault();
        }

        public Image? GetBackground()
        {
            return Entries
                .Where(entry => entry.AssetType == FsdAssetType.Background && entry.Size > 0)
                .Select(entry => GetImage(entry.Data))
                .FirstOrDefault();
        }

        public Image? GetIcon()
        {
            return Entries
                .Where(entry => entry.AssetType == FsdAssetType.Thumbnail && entry.Size > 0)
                .Select(entry => GetImage(entry.Data))
                .FirstOrDefault();
        }

        public Image? GetBoxart()
        {
            return Entries
                .Where(entry => entry.AssetType == FsdAssetType.FullCover && entry.Size > 0)
                .Select(entry => GetImage(entry.Data))
                .FirstOrDefault();
        }

        public Image[] GetScreenshots()
        {
            var ret = Screenshots
                .Where(entry => entry.Size > 0)
                .Select(entry => GetImage(entry.Data))
                .Where(img => img != null)
                .Cast<Image>()
                .ToList();

            if (ret.Count == 0)
            {
                ret.AddRange(Entries
                    .Where(entry => entry.AssetType == FsdAssetType.Screenshot && entry.Size > 0)
                    .Select(entry => GetImage(entry.Data))
                    .Where(img => img != null)
                    .Cast<Image>());
            }

            return ret.ToArray();
        }

        // ========== فئات مساعدة ==========

        public class FsdAssetEntry
        {
            public readonly FsdAssetType AssetType;
            public readonly uint Offset;
            public readonly uint Size;
            public readonly uint TotalSize;
            public readonly byte[] Data;

            public FsdAssetEntry(ref byte[] data, int offset)
            {
                AssetType = (FsdAssetType)Swap(BitConverter.ToUInt32(data, offset));
                Offset = Swap(BitConverter.ToUInt32(data, offset + 4));
                Size = Swap(BitConverter.ToUInt32(data, offset + 8));
                TotalSize = Swap(BitConverter.ToUInt32(data, offset + 12));

                if (Size <= 0)
                {
                    Data = Array.Empty<byte>();
                    return;
                }

                Data = new byte[Size];
                Buffer.BlockCopy(data, (int)Offset, Data, 0, Data.Length);
            }
        }

        public class FsdScreenshotEntry
        {
            public readonly uint Offset;
            public readonly uint Size;
            public readonly uint TotalSize;
            public readonly uint Reserved;
            public readonly byte[] Data;

            public FsdScreenshotEntry(ref byte[] data, int offset)
            {
                Offset = Swap(BitConverter.ToUInt32(data, offset));
                Size = Swap(BitConverter.ToUInt32(data, offset + 4));
                TotalSize = Swap(BitConverter.ToUInt32(data, offset + 8));
                Reserved = Swap(BitConverter.ToUInt32(data, offset + 12));

                if (Size <= 0)
                {
                    Data = Array.Empty<byte>();
                    return;
                }

                Data = new byte[Size];
                Buffer.BlockCopy(data, (int)Offset, Data, 0, Data.Length);
            }
        }
    }
}
