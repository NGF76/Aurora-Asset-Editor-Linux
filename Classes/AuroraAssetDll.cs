using System;
using System.Runtime.InteropServices;

namespace PhoenixTools
{
    /// <summary>
    /// Class for interacting with the AuroraAsset.dll library
    /// </summary>
    public static class AuroraAssetDll
    {
        #region DLL Imports

        [DllImport("AuroraAsset.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ConvertImageToAsset")]
        private static extern int ConvertImageToAsset(IntPtr imageData, int imageDataLen, int imageWidth, int imageHeight, int useCompression,
                                                      IntPtr headerData, out int headerDataLen, IntPtr videoData, out int videoDataLen);

        [DllImport("AuroraAsset.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ConvertAssetToImage")]
        private static extern int ConvertAssetToImage(IntPtr headerData, int headerDataLen, IntPtr videoData, int videoDataLen,
                                                      IntPtr imageData, out int imageDataLen, out int imageWidth, out int imageHeight);

        [DllImport("AuroraAsset.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "ConvertDDSToImage")]
        private static extern int ConvertDDSToImage(IntPtr ddsData, int ddsDataLen, IntPtr imageData,
                                                    out int imageDataLen, out int imageWidth, out int imageHeight);

        #endregion

        #region Public Methods

        /// <summary>
        /// Takes raw pixel data in linear ARGB format and outputs Aurora .asset formatted header and video data
        /// </summary>
        public static bool ProcessImageToAsset(ref byte[] pixelData, int imageWidth, int imageHeight, bool useCompression,
                                               ref byte[] headerData, ref byte[] videoData)
        {
            IntPtr hd = IntPtr.Zero, vd = IntPtr.Zero, pd = IntPtr.Zero;
            try
            {
                bool status = false;

                int pixelDataLen = pixelData?.Length ?? 0;
                if (pixelData == null || pixelDataLen == 0)
                {
                    return false;
                }

                // Copy pixel data to unmanaged memory
                pd = Marshal.AllocHGlobal(pixelDataLen);
                Marshal.Copy(pixelData, 0, pd, pixelDataLen);

                // First call to get buffer sizes
                int headerDataLen;
                int videoDataLen;
                int result = ConvertImageToAsset(pd, pixelDataLen, imageWidth, imageHeight, useCompression ? 1 : 0,
                                                 IntPtr.Zero, out headerDataLen, IntPtr.Zero, out videoDataLen);

                if (result == 1)
                {
                    // Allocate unmanaged memory for asset data
                    hd = Marshal.AllocHGlobal(headerDataLen);
                    vd = Marshal.AllocHGlobal(videoDataLen);

                    // Second call to get actual data
                    result = ConvertImageToAsset(pd, pixelDataLen, imageWidth, imageHeight, useCompression ? 1 : 0,
                                                 hd, out headerDataLen, vd, out videoDataLen);

                    if (result == 1)
                    {
                        // Copy header data
                        headerData = new byte[headerDataLen];
                        Marshal.Copy(hd, headerData, 0, headerDataLen);

                        // Copy video data
                        videoData = new byte[videoDataLen];
                        Marshal.Copy(vd, videoData, 0, videoDataLen);

                        status = true;
                    }
                }
                return status;
            }
            catch (Exception e)
            {
                Console.WriteLine($"AuroraAssetDll.ProcessImageToAsset Error: {e.Message}");
                return false;
            }
            finally
            {
                // Clean up unmanaged memory
                if (pd != IntPtr.Zero) Marshal.FreeHGlobal(pd);
                if (hd != IntPtr.Zero) Marshal.FreeHGlobal(hd);
                if (vd != IntPtr.Zero) Marshal.FreeHGlobal(vd);
            }
        }

        /// <summary>
        /// Takes asset header and video data and outputs raw pixel data in BGRA format
        /// </summary>
        public static bool ProcessAssetToImage(ref byte[] headerData, ref byte[] videoData,
                                               ref byte[] pixelData, out int imageWidth, out int imageHeight)
        {
            IntPtr hd = IntPtr.Zero, vd = IntPtr.Zero, pd = IntPtr.Zero;
            try
            {
                bool status = false;

                int headerDataLen = headerData?.Length ?? 0;
                int videoDataLen = videoData?.Length ?? 0;

                if (headerDataLen == 0 || videoDataLen == 0)
                {
                    imageWidth = 0;
                    imageHeight = 0;
                    return false;
                }

                // Copy header data to unmanaged memory
                hd = Marshal.AllocHGlobal(headerDataLen);
                Marshal.Copy(headerData, 0, hd, headerDataLen);

                // Copy video data to unmanaged memory
                vd = Marshal.AllocHGlobal(videoDataLen);
                Marshal.Copy(videoData, 0, vd, videoDataLen);

                // First call to get buffer sizes
                int imageDataLen;
                int result = ConvertAssetToImage(hd, headerDataLen, vd, videoDataLen,
                                                 IntPtr.Zero, out imageDataLen, out imageWidth, out imageHeight);

                if (result == 1)
                {
                    // Allocate unmanaged memory for pixel data
                    pd = Marshal.AllocHGlobal(imageDataLen);

                    // Second call to get actual data
                    result = ConvertAssetToImage(hd, headerDataLen, vd, videoDataLen,
                                                 pd, out imageDataLen, out imageWidth, out imageHeight);

                    if (result == 1)
                    {
                        pixelData = new byte[imageDataLen];
                        Marshal.Copy(pd, pixelData, 0, imageDataLen);
                        status = true;
                    }
                }
                return status;
            }
            catch (Exception e)
            {
                Console.WriteLine($"AuroraAssetDll.ProcessAssetToImage Error: {e.Message}");
                imageWidth = 0;
                imageHeight = 0;
                return false;
            }
            finally
            {
                if (hd != IntPtr.Zero) Marshal.FreeHGlobal(hd);
                if (vd != IntPtr.Zero) Marshal.FreeHGlobal(vd);
                if (pd != IntPtr.Zero) Marshal.FreeHGlobal(pd);
            }
        }

        /// <summary>
        /// Takes a DDS image data (with header) and outputs raw pixel data in BGRA format
        /// </summary>
        public static bool ProcessDDSToImage(ref byte[] ddsData, ref byte[] pixelData, out int imageWidth, out int imageHeight)
        {
            IntPtr dds = IntPtr.Zero, pd = IntPtr.Zero;
            try
            {
                bool status = false;

                int ddsDataLen = ddsData?.Length ?? 0;
                if (ddsDataLen == 0)
                {
                    imageWidth = 0;
                    imageHeight = 0;
                    return false;
                }

                // Copy DDS data to unmanaged memory
                dds = Marshal.AllocHGlobal(ddsDataLen);
                Marshal.Copy(ddsData, 0, dds, ddsDataLen);

                // First call to get buffer sizes
                int imageDataLen;
                int result = ConvertDDSToImage(dds, ddsDataLen, IntPtr.Zero, out imageDataLen, out imageWidth, out imageHeight);

                if (result == 1)
                {
                    // Allocate unmanaged memory for pixel data
                    pd = Marshal.AllocHGlobal(imageDataLen);

                    // Second call to get actual data
                    result = ConvertDDSToImage(dds, ddsDataLen, pd, out imageDataLen, out imageWidth, out imageHeight);

                    if (result == 1)
                    {
                        pixelData = new byte[imageDataLen];
                        Marshal.Copy(pd, pixelData, 0, imageDataLen);
                        status = true;
                    }
                }
                return status;
            }
            catch (Exception e)
            {
                Console.WriteLine($"AuroraAssetDll.ProcessDDSToImage Error: {e.Message}");
                imageWidth = 0;
                imageHeight = 0;
                return false;
            }
            finally
            {
                if (dds != IntPtr.Zero) Marshal.FreeHGlobal(dds);
                if (pd != IntPtr.Zero) Marshal.FreeHGlobal(pd);
            }
        }

        #endregion
    }
}
