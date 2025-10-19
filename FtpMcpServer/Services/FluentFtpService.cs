using System;
using System.Collections.Generic;
using System.Net;
using FluentFTP;

namespace FtpMcpServer.Services
{
    internal static class FluentFtpService
    {
        public static IReadOnlyList<FtpListItem> GetListing(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            return client.GetListing(path, FtpListOption.Modify | FtpListOption.Size);
        }

        public static byte[] DownloadBytes(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            if (!client.DownloadBytes(out var bytes, path))
            {
                throw new InvalidOperationException($"Failed to download '{path}' from FTP server.");
            }

            return bytes ?? Array.Empty<byte>();
        }

        public static void UploadBytes(FtpDefaults defaults, string path, byte[] data)
        {
            using var client = CreateAndConnect(defaults);
            var status = client.UploadBytes(data, path, FtpRemoteExists.Overwrite, createRemoteDir: true);
            if (status == FtpStatus.Failed)
            {
                throw new InvalidOperationException($"Failed to upload '{path}' to FTP server.");
            }
        }

        public static void DeleteFile(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            client.DeleteFile(path);
        }

        public static void CreateDirectory(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            client.CreateDirectory(path, force: true);
        }

        public static void DeleteDirectory(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            client.DeleteDirectory(path);
        }

        public static void Rename(FtpDefaults defaults, string sourcePath, string destinationPath)
        {
            using var client = CreateAndConnect(defaults);
            client.Rename(sourcePath, destinationPath);
        }

        public static long GetFileSize(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            return client.GetFileSize(path);
        }

        public static DateTime GetModifiedTime(FtpDefaults defaults, string path)
        {
            using var client = CreateAndConnect(defaults);
            return client.GetModifiedTime(path);
        }

        private static FtpClient CreateAndConnect(FtpDefaults defaults)
        {
            var client = CreateClient(defaults);
            client.Connect();
            return client;
        }

        private static FtpClient CreateClient(FtpDefaults defaults)
        {
            if (string.IsNullOrWhiteSpace(defaults.Host))
            {
                throw new ArgumentException("FTP host must be provided.");
            }

            var client = new FtpClient(defaults.Host, defaults.Port);

            if (!string.IsNullOrEmpty(defaults.Username))
            {
                client.Credentials = new NetworkCredential(defaults.Username, defaults.Password ?? string.Empty);
            }
            else if (!string.IsNullOrEmpty(defaults.Password))
            {
                client.Credentials = new NetworkCredential("anonymous", defaults.Password);
            }

            client.Config.EncryptionMode = defaults.UseSsl ? FtpEncryptionMode.Auto : FtpEncryptionMode.None;
            client.Config.ValidateAnyCertificate = defaults.IgnoreCertErrors;
            client.Config.DataConnectionType = defaults.Passive ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT;

            var timeoutMs = Math.Max(1000, Math.Max(1, defaults.TimeoutSeconds) * 1000);
            client.Config.ConnectTimeout = timeoutMs;
            client.Config.ReadTimeout = timeoutMs;
            client.Config.DataConnectionConnectTimeout = timeoutMs;
            client.Config.DataConnectionReadTimeout = timeoutMs;

            return client;
        }
    }
}
