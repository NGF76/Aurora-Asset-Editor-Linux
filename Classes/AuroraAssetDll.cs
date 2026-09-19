using System;

namespace PhoenixTools;

/// <summary>
/// Managed replacement for the Windows-only AuroraAsset.dll conversion layer.
/// </summary>
public static class AuroraAssetDll
{
    public static bool ProcessImageToAsset(ref byte[] pixelData, int imageWidth, int imageHeight, bool useCompression,
        ref byte[] headerData, ref byte[] videoData)
    {
        try
        {
            if (!AuroraAssetEditorLinux.Classes.XenosTextureCodec.Encode(pixelData, imageWidth, imageHeight, useCompression,
                    out headerData, out videoData))
            {
                headerData = Array.Empty<byte>();
                videoData = Array.Empty<byte>();
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"AuroraAsset conversion error: {exception.Message}");
            headerData = Array.Empty<byte>();
            videoData = Array.Empty<byte>();
            return false;
        }
    }

    public static bool ProcessAssetToImage(ref byte[] headerData, ref byte[] videoData,
        ref byte[] pixelData, out int imageWidth, out int imageHeight)
    {
        try
        {
            if (!AuroraAssetEditorLinux.Classes.XenosTextureCodec.Decode(headerData, videoData,
                    out pixelData, out imageWidth, out imageHeight))
            {
                pixelData = Array.Empty<byte>();
                imageWidth = 0;
                imageHeight = 0;
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"AuroraAsset conversion error: {exception.Message}");
            pixelData = Array.Empty<byte>();
            imageWidth = 0;
            imageHeight = 0;
            return false;
        }
    }

    public static bool ProcessDDSToImage(ref byte[] ddsData, ref byte[] pixelData,
        out int imageWidth, out int imageHeight)
    {
        pixelData = Array.Empty<byte>();
        imageWidth = 0;
        imageHeight = 0;
        return false;
    }
}
