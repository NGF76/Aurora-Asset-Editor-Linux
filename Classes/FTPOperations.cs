using FluentFTP;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using AuroraAssetEditorLinux.Classes;
using AuroraAssetEditorLinux.Dialogs;
using AuroraAssetEditorLinux.Controls;
using AuroraAssetEditorLinux.Models;
using AuroraAssetEditorLinux.Helpers;

namespace AuroraAssetEditorLinux.Classes
{
    internal class FtpOperations
    {
        private readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(FtpSettings));
        public EventHandler<FtpStatusArgs>? StatusChanged;
        private AsyncFtpClient? _client;
        private FtpSettings _settings;

        public FtpOperations()
        {
            LoadSettings();
        }

        public string IpAddress => _settings.IpAddress ?? string.Empty;
        public string Username => _settings.Username ?? string.Empty;
        public string Password => _settings.Password ?? string.Empty;
        public string Port => _settings.Port ?? "21";
        public bool HaveSettings => _settings.Loaded;

        public bool ConnectionEstablished
        {
            get
            {
                if (_client != null && _client.IsConnected)
                    return _client.IsConnected;

                try
                {
                    MakeConnection().Wait();
                }
                catch
                {
                    // تجاهل
                }

                return _client != null && _client.IsConnected;
            }
        }

        private void SendStatusChanged(string msg, params object[] param)
        {
            var handler = StatusChanged;
            if (handler != null)
            {
                try
                {
                    handler.Invoke(this, new FtpStatusArgs(string.Format(msg, param)));
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError(ex, "FtpOperations.SendStatusChanged");
                }
            }
        }

        private void LoadSettings()
        {
            var appDataPath = GetAppDataPath();
            try
            {
                var path = Path.Combine(appDataPath, "ftp.json");
                using var stream = File.OpenRead(path);
                _settings = (FtpSettings)_serializer.ReadObject(stream);
                _settings.Loaded = true;
            }
            catch
            {
                _settings = new FtpSettings
                {
                    Loaded = false,
                    IpAddress = string.Empty,
                    Username = string.Empty,
                    Password = string.Empty,
                    Port = "21"
                };
            }
        }

        private static string GetAppDataPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appDataPath))
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrWhiteSpace(home))
                {
                    appDataPath = Path.Combine(home, ".config");
                }
                else
                {
                    appDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                }
            }
            return Path.Combine(appDataPath, "AuroraAssetEditor");
        }

        public async Task<bool> TestConnection(string ip, string user, string pass, string port)
        {
            _settings.IpAddress = ip;
            _settings.Username = user;
            _settings.Password = pass;
            _settings.Port = port;
            _settings.Loaded = true;

            try
            {
                if (await MakeConnection())
                {
                    SendStatusChanged("Connection test successful!");
                    return true;
                }

                SendStatusChanged("Connection test Failed...");
                return false;
            }
            catch (Exception ex)
            {
                SendStatusChanged("Error: {0}", ex.Message);
                ErrorLogger.LogError(ex, "FtpOperations.TestConnection");
                return false;
            }
        }

        private async Task<bool> MakeConnection()
        {
            try
            {
                int port = int.TryParse(_settings.Port, out int p) ? p : 21;

                _client = new AsyncFtpClient(_settings.IpAddress, _settings.Username, _settings.Password, port)
                {
                    Config = new FtpConfig
                    {
                        EncryptionMode = FtpEncryptionMode.None,
                        ConnectTimeout = 30000
                    }
                };

                SendStatusChanged("Connecting to {0}...", _settings.IpAddress);
                await _client.Connect();

                if (!_client.IsConnected)
                {
                    SendStatusChanged("Connection failed to {0}", _settings.IpAddress);
                    return false;
                }

                SendStatusChanged("Connection to {0} Established...", _settings.IpAddress);
                return true;
            }
            catch (Exception ex)
            {
                SendStatusChanged("Connection error: {0}", ex.Message);
                ErrorLogger.LogError(ex, "FtpOperations.MakeConnection");
                return false;
            }
        }

        public void SaveSettings()
        {
            try
            {
                var appDataPath = GetAppDataPath();
                var path = Path.Combine(appDataPath, "ftp.json");

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using var stream = File.OpenWrite(path);
                _serializer.WriteObject(stream, _settings);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "FtpOperations.SaveSettings");
            }
        }

        public void SaveSettings(string ip, string user, string pass, string port)
        {
            _settings.IpAddress = ip;
            _settings.Username = user;
            _settings.Password = pass;
            _settings.Port = port;
            _settings.Loaded = true;
            SaveSettings();
        }

        public async Task<bool> NavigateToGameDataDir()
        {
            if (_client == null || !_client.IsConnected)
            {
                if (!await MakeConnection())
                {
                    SendStatusChanged("Connection failed to {0}", _settings.IpAddress);
                    return false;
                }
            }

            try
            {
                const string dir = "/Game/Data/GameData/";
                SendStatusChanged("Changing working directory to {0}...", dir);
                await _client.SetWorkingDirectory(dir);
                var currentDir = await _client.GetWorkingDirectory();
                return currentDir.Equals(dir, StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "FtpOperations.NavigateToGameDataDir");
                SendStatusChanged("Error navigating to GameData: {0}", ex.Message);
                return false;
            }
        }

        public async Task<bool> NavigateToAssetDir(string assetName)
        {
            if (!await NavigateToGameDataDir())
                return false;

            try
            {
                var dir = $"/Game/Data/GameData/{assetName}/";
                SendStatusChanged("Changing working directory to {0}...", dir);
                await _client.SetWorkingDirectory(dir);
                var currentDir = await _client.GetWorkingDirectory();
                return currentDir.Equals(dir, StringComparison.InvariantCultureIgnoreCase);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "FtpOperations.NavigateToAssetDir");
                SendStatusChanged("Error navigating to asset dir: {0}", ex.Message);
                return false;
            }
        }

        public async Task<byte[]?> GetAssetData(string file, string assetDir)
        {
            try
            {
                if (!await NavigateToAssetDir(assetDir))
                    return null;

                if (_client == null)
                    return null;

                var items = await _client.GetListing();
                var fileInfo = items.FirstOrDefault(item => item.Name.Equals(file, StringComparison.InvariantCultureIgnoreCase));

                if (fileInfo == null || fileInfo.Size <= 0)
                    return null;

                var data = new byte[fileInfo.Size];
                using var stream = await _client.OpenRead(file);
                var offset = 0;
                while (offset < data.Length)
                {
                    offset += await stream.ReadAsync(data, offset, data.Length - offset);
                }

                return data;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, $"FtpOperations.GetAssetData({file})");
                SendStatusChanged("Error downloading {0}: {1}", file, ex.Message);
                return null;
            }
        }

        public async Task<bool> SendAssetData(string file, string assetDir, byte[] data)
        {
            try
            {
                if (!await NavigateToAssetDir(assetDir))
                    return false;

                if (_client == null)
                    return false;

                using var stream = await _client.OpenWrite(file);
                await stream.WriteAsync(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, $"FtpOperations.SendAssetData({file})");
                SendStatusChanged("Error uploading {0}: {1}", file, ex.Message);
                return false;
            }
        }

        public async Task<bool> DownloadContentDb(string path)
        {
            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    if (!await MakeConnection())
                    {
                        SendStatusChanged("Connection failed to {0}", _settings.IpAddress);
                        return false;
                    }
                }

                const string dir = "/Game/Data/DataBases/";
                SendStatusChanged("Changing working directory to {0}...", dir);
                await _client.SetWorkingDirectory(dir);

                var items = await _client.GetListing();
                var fileInfo = items.FirstOrDefault(item => item.Name.Equals("Content.db", StringComparison.InvariantCultureIgnoreCase));

                if (fileInfo == null || fileInfo.Size <= 0)
                {
                    SendStatusChanged("Content.db not found");
                    return false;
                }

                var data = new byte[fileInfo.Size];
                using var stream = await _client.OpenRead("Content.db");
                var offset = 0;
                while (offset < data.Length)
                {
                    offset += await stream.ReadAsync(data, offset, data.Length - offset);
                }

                await File.WriteAllBytesAsync(path, data);
                SendStatusChanged("Content.db downloaded successfully ({0} bytes)", data.Length);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError(ex, "FtpOperations.DownloadContentDb");
                SendStatusChanged("Error downloading Content.db: {0}", ex.Message);
                return false;
            }
        }

        [DataContract]
        private class FtpSettings
        {
            public bool Loaded;

            [DataMember(Name = "ip")]
            public string? IpAddress { get; set; }

            [DataMember(Name = "user")]
            public string? Username { get; set; }

            [DataMember(Name = "pass")]
            public string? Password { get; set; }

            [DataMember(Name = "port")]
            public string? Port { get; set; }
        }
    }

    // Disabled This Function 
    public class FtpStatusArgs : EventArgs
    {
        public string Message { get; set; }

        public FtpStatusArgs(string message)
        {
            Message = message;
        }
    }
}

