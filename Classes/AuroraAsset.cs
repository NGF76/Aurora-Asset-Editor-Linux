using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using AuroraAssetEditorLinux.Dialogs;

namespace AuroraAssetEditorLinux.Classes
{
    public static class AuroraAsset
    {
        public enum AssetType
        {
            Icon, // Icon
            Banner, // Banner
            Boxart, // Cover
            Slot, // NXEArt, currently not used
            Background, // Background
            ScreenshotStart, // Screenshot 1
            ScreenshotEnd = ScreenshotStart + ScreenShotMax, // Screenshot 20
            Max = ScreenshotEnd // End of it all
        }

        private const int ScreenShotMax = 20;

        private static uint Swap(uint x)
        {
            return (x & 0x000000FF) << 24 | (x & 0x0000FF00) << 8 | (x & 0x00FF0000) >> 8 | (x & 0xFF000000) >> 24;
        }

        // تحويل البيانات الخام إلى Image باستخدام ImageSharp
        private static Image<Rgba32> RawArgbToImage(byte[] raw, int width, int height)
        {
            var pixelCount = checked(width * height);
            if (raw.Length < checked(pixelCount * 4))
                throw new ArgumentException("The raw pixel buffer is smaller than the image dimensions require.", nameof(raw));

            var hasAlpha = false;
            for (var index = 0; index < pixelCount; index++)
            {
                if (raw[index * 4] == 0)
                    continue;
                hasAlpha = true;
                break;
            }

            var image = new Image<Rgba32>(width, height);
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var rowOffset = y * width * 4;
                    for (var x = 0; x < width; x++)
                    {
                        var idx = rowOffset + (x * 4);
                        row[x] = new Rgba32(raw[idx + 1], raw[idx + 2], raw[idx + 3],
                            hasAlpha ? raw[idx] : (byte)255);
                    }
                }
            });
            return image;
        }

        // تحويل Image إلى بيانات خام
        private static byte[] ImageToRawArgb(Image<Rgba32> img)
        {
            var width = img.Width;
            var height = img.Height;
            var ret = new byte[checked(width * height * 4)];
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var rowOffset = y * width * 4;
                    for (var x = 0; x < width; x++)
                    {
                        var pixel = row[x];
                        var idx = rowOffset + (x * 4);
                        ret[idx] = pixel.A;
                        ret[idx + 1] = pixel.R;
                        ret[idx + 2] = pixel.G;
                        ret[idx + 3] = pixel.B;
                    }
                }
            });
            return ret;
        }

        public class AssetFile
        {
            public readonly int DataOffset;
            public readonly AssetPackEntryTable EntryTable;
            public readonly AssetPackHeader Header;

            public AssetFile()
            {
                Header = new AssetPackHeader(0x52584541, 1, 0);
                EntryTable = new AssetPackEntryTable();
                DataOffset = 20 + (EntryTable.Entries.Length * 64);
                DataOffset += 2048 - (DataOffset % 2048);
            }

            public AssetFile(byte[] data)
            {
                if (data == null || data.Length < 2048)
                {
                    Header = new AssetPackHeader(0x52584541, 1, 0);
                    EntryTable = new AssetPackEntryTable();
                    DataOffset = 20 + (EntryTable.Entries.Length * 64);
                    DataOffset += 2048 - (DataOffset % 2048);
                    return;
                }

                var magic = Swap(BitConverter.ToUInt32(data, 0));
                if (magic != 0x52584541)
                    throw new Exception("Invalid asset file magic!");

                var version = Swap(BitConverter.ToUInt32(data, 4));
                if (version != 1)
                    throw new NotSupportedException("Unsupported asset file version!");

                var datasize = Swap(BitConverter.ToUInt32(data, 8));
                Header = new AssetPackHeader(magic, version, datasize);
                EntryTable = new AssetPackEntryTable(data, 12);
                DataOffset = 20 + (EntryTable.Entries.Length * 64);
                DataOffset += 2048 - (DataOffset % 2048);

                for (var i = 0; i < EntryTable.Entries.Length; i++)
                {
                    var entry = EntryTable.Entries[i];
                    if (entry.Size <= 0)
                        continue;

                    var dataOffset = checked(DataOffset + (int)entry.Offset);
                    var dataSize = checked((int)entry.Size);
                    if (dataOffset < 0 || dataOffset > data.Length - dataSize)
                        throw new InvalidDataException($"Asset entry {i} points outside the file.");
                    if (entry.VideoData.Length != dataSize)
                        throw new InvalidDataException($"Asset entry {i} has an invalid size.");

                    Buffer.BlockCopy(data, dataOffset, entry.VideoData, 0, dataSize);
                    SetImage(entry.VideoData, i);
                }
            }

            public int PaddingSize => 0x800 - ((0x14 + EntryTable.Entries.Length * 0x40) % 0x800);

            public byte[] FileData
            {
                get
                {
                    var entries = EntryTable.Entries;
                    uint offset = 0;
                    Header.DataSize = 0;
                    EntryTable.Flags = 0;
                    EntryTable.ScreenshotCount = 0;

                    for (var i = 0; i < entries.Length; i++)
                    {
                        var entry = entries[i];
                        if (entry.Size <= 0)
                            continue;
                        entry.Offset = offset;
                        offset += entry.Size;
                        Header.DataSize += entry.Size;
                        EntryTable.Flags |= (uint)(1 << i);
                        if (i <= (int)AssetType.ScreenshotEnd && i >= (int)AssetType.ScreenshotStart)
                            EntryTable.ScreenshotCount++;
                    }

                    var paddingSize = PaddingSize;
                    var dataSize = checked((int)Header.DataSize);
                    var ret = new byte[checked(20 + (entries.Length * 64) + paddingSize + dataSize)];
                    var span = ret.AsSpan();

                    BinaryPrimitives.WriteUInt32BigEndian(span, Header.Magic);
                    BinaryPrimitives.WriteUInt32BigEndian(span[4..], Header.Version);
                    BinaryPrimitives.WriteUInt32BigEndian(span[8..], Header.DataSize);
                    BinaryPrimitives.WriteUInt32BigEndian(span[12..], EntryTable.Flags);
                    BinaryPrimitives.WriteUInt32BigEndian(span[16..], EntryTable.ScreenshotCount);

                    var position = 20;
                    foreach (var entry in entries)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(span[position..], entry.Offset);
                        BinaryPrimitives.WriteUInt32BigEndian(span[(position + 4)..], entry.Size);
                        BinaryPrimitives.WriteUInt32BigEndian(span[(position + 8)..], entry.ExtendedInfo);
                        entry.TextureHeader.CopyTo(span[(position + 12)..]);
                        position += 64;
                    }

                    position += paddingSize;

                    foreach (var entry in entries)
                    {
                        if (entry.Size <= 0)
                            continue;
                        entry.VideoData.CopyTo(span[position..]);
                        position += entry.VideoData.Length;
                    }

                    return ret;
                }
            }

            public bool HasBoxArt => EntryTable.Entries[(int)AssetType.Boxart].Size > 0;
            public bool HasBackground => EntryTable.Entries[(int)AssetType.Background].Size > 0;
            public bool HasScreenshots => EntryTable.ScreenshotCount > 0;
            public bool HasIconBanner => EntryTable.Entries[(int)AssetType.Icon].Size > 0 || EntryTable.Entries[(int)AssetType.Banner].Size > 0;

            private bool SetImage(Image<Rgba32> img, int index, bool useCompression)
            {
                if (index > (int)AssetType.Max)
                    return false;

                if (img == null)
                {
                    EntryTable.Entries[index].ImageData = null;
                    EntryTable.Entries[index].VideoData = new byte[0];
                    EntryTable.Entries[index].TextureHeader = new byte[EntryTable.Entries[index].TextureHeader.Length];
                    return true;
                }

                EntryTable.Entries[index].ImageData = img;
                var data = ImageToRawArgb(img);
                byte[] video = new byte[0], header = new byte[0];

                // TODO: استبدال AuroraAssetDll بمعالجة محلية
                // هذا يحتاج إلى مكتبة خارجية لمعالجة الصور
                if (!ProcessImageToAsset(data, img.Width, img.Height, useCompression, ref header, ref video))
                    return false;

                EntryTable.Entries[index].VideoData = video;
                EntryTable.Entries[index].TextureHeader = header;
                return true;
            }

            private void SetImage(byte[] videoData, int index)
            {
                var imageData = new byte[0];
                int imageWidth, imageHeight;

                // TODO: استبدال AuroraAssetDll بمعالجة محلية
                if (!ProcessAssetToImage(ref EntryTable.Entries[index].TextureHeader, ref videoData, ref imageData, out imageWidth, out imageHeight))
                    return;

                EntryTable.Entries[index].VideoData = videoData;
                EntryTable.Entries[index].ImageData = RawArgbToImage(imageData, imageWidth, imageHeight);
            }

            // ========== دوال بديلة لمعالجة الصور (تحتاج إلى تنفيذ) ==========

            private static bool ProcessImageToAsset(byte[] data, int width, int height, bool useCompression, ref byte[] header, ref byte[] video)
            {
                return PhoenixTools.AuroraAssetDll.ProcessImageToAsset(
                    ref data, width, height, useCompression, ref header, ref video);
            }

            private static bool ProcessAssetToImage(ref byte[] header, ref byte[] videoData, ref byte[] imageData, out int imageWidth, out int imageHeight)
            {
                return PhoenixTools.AuroraAssetDll.ProcessAssetToImage(
                    ref header, ref videoData, ref imageData, out imageWidth, out imageHeight);
            }

            private void SetImage(AssetFile asset, int index)
            {
                var target = EntryTable.Entries[index];
                var src = asset.EntryTable.Entries[index];
                target.TextureHeader = src.TextureHeader;
                target.VideoData = src.VideoData;
                target.ImageData = src.ImageData;
            }

            // ========== الدوال العامة ==========

            public bool SetIcon(Image<Rgba32> img, bool useCompression) => SetImage(img, (int)AssetType.Icon, useCompression);
            public bool SetBackground(Image<Rgba32> img, bool useCompression) => SetImage(img, (int)AssetType.Background, useCompression);
            public bool SetBanner(Image<Rgba32> img, bool useCompression) => SetImage(img, (int)AssetType.Banner, useCompression);
            public bool SetBoxart(Image<Rgba32> img, bool useCompression) => SetImage(img, (int)AssetType.Boxart, useCompression);

            public bool SetScreenshot(Image<Rgba32> img, int num, bool useCompression)
            {
                num += (int)AssetType.ScreenshotStart - 1;
                return num <= (int)AssetType.ScreenshotEnd && SetImage(img, num, useCompression);
            }

            public Image<Rgba32>? GetIcon() => EntryTable.Entries[(int)AssetType.Icon].Size > 0 ? EntryTable.Entries[(int)AssetType.Icon].ImageData : null;
            public Image<Rgba32>? GetBoxart() => EntryTable.Entries[(int)AssetType.Boxart].Size > 0 ? EntryTable.Entries[(int)AssetType.Boxart].ImageData : null;
            public Image<Rgba32>? GetBanner() => EntryTable.Entries[(int)AssetType.Banner].Size > 0 ? EntryTable.Entries[(int)AssetType.Banner].ImageData : null;
            public Image<Rgba32>? GetBackground() => EntryTable.Entries[(int)AssetType.Background].Size > 0 ? EntryTable.Entries[(int)AssetType.Background].ImageData : null;

            public Image<Rgba32>? GetScreenshot(int num)
            {
                num += (int)AssetType.ScreenshotStart - 1;
                if (num > (int)AssetType.ScreenshotEnd)
                    return null;
                return EntryTable.Entries[num].Size > 0 ? EntryTable.Entries[num].ImageData : null;
            }

            public Image<Rgba32>[] GetScreenshots()
            {
                var ret = new List<Image<Rgba32>>();
                for (var i = 0; i < ScreenShotMax; i++)
                {
                    var screenshot = GetScreenshot(i + 1);
                    if (screenshot != null)
                        ret.Add(screenshot);
                }
                return ret.ToArray();
            }

            public void SetBoxart(AssetFile asset) => SetImage(asset, (int)AssetType.Boxart);
            public void SetBackground(AssetFile asset) => SetImage(asset, (int)AssetType.Background);
            public void SetBackground(System.Drawing.Image img, AssetFile asset) => SetImage(asset, (int)AssetType.Background);
            public void SetIcon(AssetFile asset) => SetImage(asset, (int)AssetType.Icon);
            public void SetBanner(AssetFile asset) => SetImage(asset, (int)AssetType.Banner);

            public void SetScreenshots(AssetFile asset)
            {
                for (var i = (int)AssetType.ScreenshotStart; i < (int)AssetType.ScreenshotEnd; i++)
                    SetImage(asset, i);
            }
        }

        // ========== الفئات المساعدة ==========

        public class AssetPackEntry
        {
            public Image<Rgba32>? ImageData;
            public byte[] TextureHeader;
            public byte[] VideoData;

            public AssetPackEntry()
            {
                Offset = 0;
                VideoData = new byte[0];
                TextureHeader = new byte[52];
            }

            public AssetPackEntry(byte[] data, int offset)
            {
                Offset = Swap(BitConverter.ToUInt32(data, offset));
                VideoData = new byte[Swap(BitConverter.ToUInt32(data, offset + 4))];
                TextureHeader = new byte[52];
                Buffer.BlockCopy(data, offset + 12, TextureHeader, 0, TextureHeader.Length);
            }

            public uint Offset { get; internal set; }
            public uint Size => (uint)VideoData.Length;
            public uint ExtendedInfo => 0;
            public (int Width, int Height) ImageSize => ImageData != null ? (ImageData.Width, ImageData.Height) : (0, 0);

            public override string ToString()
            {
                var sz = ImageSize;
                return $"Offset: 0x{Offset:X}{Environment.NewLine}Size: 0x{Size:X}{Environment.NewLine}Extended Info: 0x{ExtendedInfo:X}{Environment.NewLine}Texture Header Size: 0x{TextureHeader.Length:X}{Environment.NewLine}Width: {sz.Width}{Environment.NewLine}Height: {sz.Height}";
            }
        }

        public class AssetPackEntryTable
        {
            public readonly AssetPackEntry[] Entries = new AssetPackEntry[(int)AssetType.Max];

            public AssetPackEntryTable()
            {
                Flags = 0;
                ScreenshotCount = 0;
                for (var i = 0; i < Entries.Length; i++)
                    Entries[i] = new AssetPackEntry();
            }

            public AssetPackEntryTable(byte[] data, int offset)
            {
                Flags = Swap(BitConverter.ToUInt32(data, offset));
                ScreenshotCount = Swap(BitConverter.ToUInt32(data, offset + 4));
                offset += 8;
                for (var i = 0; i < Entries.Length; i++, offset += 64)
                    Entries[i] = new AssetPackEntry(data, offset);
            }

            public uint Flags { get; internal set; }
            public uint ScreenshotCount { get; internal set; }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendFormat("Flags: 0x{0:X}{1}", Flags, Environment.NewLine);
                sb.AppendFormat("ScreenshotCount: {0}{1}", ScreenshotCount, Environment.NewLine);
                sb.AppendLine("Entries:");
                for (var i = 0; i < Entries.Length; i++)
                {
                    if (i < (int)AssetType.ScreenshotStart || i > (int)AssetType.ScreenshotEnd)
                        sb.AppendLine(((AssetType)i) + ":");
                    else
                        sb.AppendLine($"ScreenShot {i - (int)AssetType.ScreenshotStart}:");
                    sb.AppendLine(Entries[i].Size > 0 ? Entries[i].ToString() : "No data...");
                }
                return sb.ToString();
            }
        }

        public class AssetPackHeader
        {
            public AssetPackHeader(uint magic, uint version, uint dataSize)
            {
                Magic = magic;
                Version = version;
                DataSize = dataSize;
            }

            public uint Magic { get; private set; }
            public uint Version { get; private set; }
            public uint DataSize { get; internal set; }

            public override string ToString()
            {
                return $"Magic: {Encoding.ASCII.GetString(BitConverter.GetBytes(Swap(Magic)))}{Environment.NewLine}Version: {Version}{Environment.NewLine}DataSize: {DataSize}";
            }
        }
    }
}
