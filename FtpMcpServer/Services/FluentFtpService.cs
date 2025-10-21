using System;
using System.Collections.Generic;
using System.Net;
using FluentFTP;
using Microsoft.Extensions.Logging;

namespace FtpMcpServer.Services
{
    public sealed class FluentFtpService : IFluentFtpService
    {
        private readonly ILogger<FluentFtpService> _logger;

        public FluentFtpService(ILogger<FluentFtpService> logger)
        {
            _logger = logger;
        }

        public IReadOnlyList<FtpListItem> GetListing(FtpDefaults defaults, string path)
        {
            return Execute(defaults, path, client =>
            {
                _logger.LogInformation("Listing FTP directory {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                var listing = client.GetListing(path, FtpListOption.Modify | FtpListOption.Size);
                _logger.LogInformation(
                    "Found {Count} items while listing {Path} on {Host}:{Port}.",
                    listing.Length,
                    path,
                    defaults.Host,
                    defaults.Port);
                return listing;
            });
        }

        public byte[] DownloadBytes(FtpDefaults defaults, string path)
        {
            return Execute(defaults, path, client =>
            {
                _logger.LogInformation("Downloading FTP file {Path} from {Host}:{Port}.", path, defaults.Host, defaults.Port);
                if (!client.DownloadBytes(out var bytes, path))
                {
                    throw new InvalidOperationException($"Failed to download '{path}' from FTP server.");
                }

                var length = bytes?.Length ?? 0;
                _logger.LogInformation("Downloaded {Length} bytes from {Path} on {Host}:{Port}.", length, path, defaults.Host, defaults.Port);
                return bytes ?? Array.Empty<byte>();
            });
        }

        public void UploadBytes(FtpDefaults defaults, string path, byte[] data)
        {
            Execute(defaults, path, client =>
            {
                _logger.LogInformation("Uploading {Length} bytes to FTP path {Path} on {Host}:{Port}.", data.Length, path, defaults.Host, defaults.Port);
                var status = client.UploadBytes(data, path, FtpRemoteExists.Overwrite, createRemoteDir: true);
                if (status == FtpStatus.Failed)
                {
                    throw new InvalidOperationException($"Failed to upload '{path}' to FTP server.");
                }

                _logger.LogInformation("Successfully uploaded data to {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                return true;
            });
        }

        public void DeleteFile(FtpDefaults defaults, string path)
        {
            Execute(defaults, path, client =>
            {
                _logger.LogInformation("Deleting FTP file {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                client.DeleteFile(path);
                _logger.LogInformation("Deleted FTP file {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                return true;
            });
        }

        public void CreateDirectory(FtpDefaults defaults, string path)
        {
            Execute(defaults, path, client =>
            {
                _logger.LogInformation("Creating FTP directory {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                client.CreateDirectory(path, force: true);
                _logger.LogInformation("Created FTP directory {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                return true;
            });
        }

        public void DeleteDirectory(FtpDefaults defaults, string path)
        {
            Execute(defaults, path, client =>
            {
                _logger.LogInformation("Deleting FTP directory {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                client.DeleteDirectory(path);
                _logger.LogInformation("Deleted FTP directory {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                return true;
            });
        }

        public void Rename(FtpDefaults defaults, string sourcePath, string destinationPath)
        {
            Execute(defaults, sourcePath, client =>
            {
                _logger.LogInformation("Renaming FTP path {SourcePath} to {DestinationPath} on {Host}:{Port}.", sourcePath, destinationPath, defaults.Host, defaults.Port);
                client.Rename(sourcePath, destinationPath);
                _logger.LogInformation("Renamed FTP path {SourcePath} to {DestinationPath} on {Host}:{Port}.", sourcePath, destinationPath, defaults.Host, defaults.Port);
                return true;
            });
        }

        public long GetFileSize(FtpDefaults defaults, string path)
        {
            return Execute(defaults, path, client =>
            {
                _logger.LogInformation("Retrieving size for FTP file {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                var size = client.GetFileSize(path);
                _logger.LogInformation("FTP file {Path} on {Host}:{Port} is {Size} bytes.", path, defaults.Host, defaults.Port, size);
                return size;
            });
        }

        public DateTime GetModifiedTime(FtpDefaults defaults, string path)
        {
            return Execute(defaults, path, client =>
            {
                _logger.LogInformation("Retrieving modified time for FTP file {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                var modified = client.GetModifiedTime(path);
                _logger.LogInformation("FTP file {Path} on {Host}:{Port} was last modified at {Modified}.", path, defaults.Host, defaults.Port, modified);
                return modified;
            });
        }

        private T Execute<T>(FtpDefaults defaults, string path, Func<FtpClient, T> action)
        {
            try
            {
                using var client = CreateAndConnect(defaults);
                return action(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FTP operation failed for path {Path} on {Host}:{Port}.", path, defaults.Host, defaults.Port);
                throw;
            }
        }

        private FtpClient CreateAndConnect(FtpDefaults defaults)
        {
            var client = CreateClient(defaults);
            _logger.LogDebug("Connecting to FTP server {Host}:{Port} (SSL: {UseSsl}, Passive: {Passive}).", defaults.Host, defaults.Port, defaults.UseSsl, defaults.Passive);
            client.Connect();
            _logger.LogDebug("Connected to FTP server {Host}:{Port}.", defaults.Host, defaults.Port);
            return client;
        }

        private FtpClient CreateClient(FtpDefaults defaults)
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

            // Encryption / certificate validation
            client.Config.EncryptionMode = defaults.UseSsl ? FtpEncryptionMode.Auto : FtpEncryptionMode.None;
            client.Config.ValidateAnyCertificate = defaults.IgnoreCertErrors;

            // Passive / Active
            client.Config.DataConnectionType = defaults.Passive ? FtpDataConnectionType.PASV : FtpDataConnectionType.PORT;

            // Transfers: prefer binary; ensure zero-byte files are allowed
            client.Config.UploadDataType = FtpDataType.Binary;
            client.Config.DownloadDataType = FtpDataType.Binary;
            client.Config.DownloadZeroByteFiles = true;

            // Timeouts
            var timeoutMs = Math.Max(1000, Math.Max(1, defaults.TimeoutSeconds) * 1000);
            client.Config.ConnectTimeout = timeoutMs;
            client.Config.ReadTimeout = timeoutMs;
            client.Config.DataConnectionConnectTimeout = timeoutMs;
            client.Config.DataConnectionReadTimeout = timeoutMs;

            return client;
        }


    }
}
