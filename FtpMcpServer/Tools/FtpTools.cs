using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.IO;
using FluentFTP.Helpers;
using FtpMcpServer.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Tools
{
    [McpServerToolType]
    public class FtpTools
    {
        private readonly ILogger<FtpTools> _logger;
        private readonly IFluentFtpService _ftpService;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public FtpTools(ILogger<FtpTools> logger, IFluentFtpService ftpService, FileExtensionContentTypeProvider contentTypeProvider)
        {
            _logger = logger;
            _ftpService = ftpService;
            _contentTypeProvider = contentTypeProvider;
        }

        [McpServerTool(Name = "ftp_listDirectory", UseStructuredContent = true, ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Lists entries in an FTP directory. Returns structured JSON describing each item.")]
        public FtpListResult ListDirectory(
            FtpDefaults defaults,
            [Description("Remote path to list (e.g., /pub). Defaults to the server default path or '/'")] string? path = null)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Listing directory {Path} on FTP host {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);
            var items = _ftpService.GetListing(defaults, remotePath);
            var result = new FtpListResult
            {
                Host = GetHostOrThrow(defaults),
                Path = remotePath,
                Port = defaults.Port,
                UseSsl = defaults.UseSsl,
                Passive = defaults.Passive,
                Items = new List<FtpListItem>()
            };

            foreach (var it in items)
            {
                result.Items.Add(new FtpListItem
                {
                    Name = it.Name,
                    IsDirectory = it.Type == FluentFTP.FtpObjectType.Directory,
                    Size = it.Size >= 0 ? it.Size : null,
                    Modified = it.Modified == DateTime.MinValue ? null : it.Modified,
                    Permissions = it.RawPermissions,
                    Raw = it.Input
                });
            }

            _logger.LogInformation("Returning {Count} FTP items for directory {Path} on {Host}:{Port}.", result.Items.Count, remotePath, defaults.Host, defaults.Port);
            return result;
        }

        [McpServerTool(Name = "ftp_downloadFile", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Downloads a file from the FTP server. Returns both an embedded resource (for immediate use) and a resource_link (for clients that prefer to call resources/read).")]
        public CallToolResult DownloadFile(
            FtpDefaults defaults,
            [Description("Remote file path to download (e.g., /pub/file.txt)")] string path)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Downloading FTP file {Path} from {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);

            // Download bytes once
            var bytes = _ftpService.DownloadBytes(defaults, remotePath);
            if (bytes is null)
            {
                // Defensive: FluentFTP returns false + null only if something is very off;
                // throw to surface a proper MCP error rather than a silent 'no result'
                throw new InvalidOperationException($"Download returned no bytes for '{remotePath}'.");
            }

            // Build our MCP resource:// URI for the resource type
            string resourceUri = $"resource://ftp/file?path={Uri.EscapeDataString(remotePath)}";

            // MIME type from file extension
            string mime = _contentTypeProvider.TryGetContentType(remotePath, out var contentType)
                ? contentType!
                : "application/octet-stream";

            // Friendly name for link display
            string name = Path.GetFileName(remotePath);
            if (string.IsNullOrEmpty(name))
            {
                name = remotePath.TrimEnd('/'); // fallback if path had trailing slash
            }

            // Optional size
            long size = bytes.LongLength;

            // Encode as base64 for embedded resource
            string b64 = Convert.ToBase64String(bytes);

            // Return both: embedded resource and a link (best of both worlds for all clients)
            var content = new List<ContentBlock>
            {
                new EmbeddedResourceBlock
                {
                    Resource = new BlobResourceContents
                    {
                        Uri = resourceUri,
                        MimeType = mime,
                        Blob = b64
                    }
                },
                new ResourceLinkBlock
                {
                    Uri = resourceUri,
                    Name = name,
                    Description = $"FTP file {remotePath} on {defaults.Host}:{defaults.Port}",
                    MimeType = mime,
                    Size = size
                }
            };
            return new CallToolResult { Content = content };
        }

        [McpServerTool(Name = "ftp_uploadFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Uploads data as a file to the FTP server. Data is expected to be base64-encoded.")]
        public string UploadFile(
            FtpDefaults defaults,
            [Description("Remote file path to upload to (e.g., /incoming/file.txt)")] string path,
            [Description("Base64-encoded file content to upload")] string dataBase64)
        {
            var bytes = Convert.FromBase64String(dataBase64);
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Uploading {Length} bytes to FTP path {Path} on {Host}:{Port}.", bytes.Length, remotePath, defaults.Host, defaults.Port);
            _ftpService.UploadBytes(defaults, remotePath, bytes);
            return $"Uploaded {bytes.Length} bytes to {BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_writeFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Writes plain text content to a file on the FTP server using the specified encoding (UTF-8 by default).")]
        public string WriteFile(
            FtpDefaults defaults,
            [Description("Remote file path to write to (e.g., /incoming/file.txt)")] string path,
            [Description("Plain text content to write to the file")] string content,
            [Description("Optional text encoding name (e.g., utf-8, iso-8859-1). Defaults to UTF-8.")] string? encoding = null)
        {
            Encoding enc;
            if (string.IsNullOrWhiteSpace(encoding))
            {
                enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            }
            else
            {
                enc = Encoding.GetEncoding(encoding);
            }

            var bytes = enc.GetBytes(content ?? string.Empty);
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Writing {Length} bytes (encoding: {Encoding}) to FTP path {Path} on {Host}:{Port}.", bytes.Length, enc.WebName, remotePath, defaults.Host, defaults.Port);
            _ftpService.UploadBytes(defaults, remotePath, bytes);
            return $"Wrote {bytes.Length} bytes to {BuildUri(defaults, remotePath)} using {enc.WebName} encoding";
        }

        [McpServerTool(Name = "ftp_deleteFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Deletes a file on the FTP server.")]
        public string DeleteFile(
            FtpDefaults defaults,
            [Description("Remote file path to delete (e.g., /pub/file.txt)")] string path)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Deleting FTP file {Path} on {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);
            _ftpService.DeleteFile(defaults, remotePath);
            return $"Deleted {BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_makeDirectory", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Creates a directory on the FTP server.")]
        public string MakeDirectory(
            FtpDefaults defaults,
            [Description("Remote directory path to create (e.g., /pub/newdir)")] string path)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Creating FTP directory {Path} on {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);
            _ftpService.CreateDirectory(defaults, remotePath);
            return $"Created directory {BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_removeDirectory", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Removes a directory on the FTP server (must be empty).")]
        public string RemoveDirectory(
            FtpDefaults defaults,
            [Description("Remote directory path to remove (must be empty)")] string path)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Removing FTP directory {Path} on {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);
            _ftpService.DeleteDirectory(defaults, remotePath);
            return $"Removed directory {BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_rename", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Renames a file or directory on the FTP server.")]
        public string Rename(
            FtpDefaults defaults,
            [Description("Current remote path to rename")] string path,
            [Description("New name (not a full path)")] string newName)
        {
            var remotePath = GetRemotePath(defaults, path);
            var parentDirectory = remotePath.GetFtpDirectoryName();
            if (string.IsNullOrEmpty(parentDirectory) || parentDirectory == ".")
            {
                parentDirectory = "/";
            }

            var destinationPath = parentDirectory.GetFtpPath(newName);
            _logger.LogInformation("Renaming FTP path {Path} to {Destination} on {Host}:{Port}.", remotePath, destinationPath, defaults.Host, defaults.Port);
            _ftpService.Rename(defaults, remotePath, destinationPath);
            return $"Renamed {BuildUri(defaults, remotePath)} to {newName}";
        }

        [McpServerTool(Name = "ftp_getFileSize", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Gets the size (in bytes) of a remote file.")]
        public long GetFileSize(
            FtpDefaults defaults,
            [Description("Remote file path")] string path)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Retrieving file size for FTP path {Path} on {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);
            return _ftpService.GetFileSize(defaults, remotePath);
        }

        [McpServerTool(Name = "ftp_getModifiedTime", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Gets the last modified time (server local time) of a remote file.")]
        public DateTime GetModifiedTime(
            FtpDefaults defaults,
            [Description("Remote file path")] string path)
        {
            var remotePath = GetRemotePath(defaults, path);
            _logger.LogInformation("Retrieving modified time for FTP path {Path} on {Host}:{Port}.", remotePath, defaults.Host, defaults.Port);
            return _ftpService.GetModifiedTime(defaults, remotePath);
        }

        private static string GetRemotePath(FtpDefaults defaults, string? path)
        {
            var candidate = string.IsNullOrWhiteSpace(path) ? defaults.DefaultPath : path;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = "/";
            }

            return RemotePaths.GetFtpPath(candidate);
        }

        private static string GetHostOrThrow(FtpDefaults defaults)
        {
            return defaults.Host ?? throw new ArgumentException("Host is required");
        }

        private static Uri BuildUri(FtpDefaults defaults, string remotePath)
        {
            var host = GetHostOrThrow(defaults);
            var ftpPath = RemotePaths.GetFtpPath(remotePath);
            return new UriBuilder("ftp", host, defaults.Port, ftpPath).Uri;
        }
    }

    public sealed class FtpListResult
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Path { get; set; } = string.Empty;
        public bool UseSsl { get; set; }
        public bool Passive { get; set; }
        public List<FtpListItem> Items { get; set; } = new List<FtpListItem>();
    }

    public sealed class FtpListItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long? Size { get; set; }
        public DateTime? Modified { get; set; }
        public string? Permissions { get; set; }
        public string? Raw { get; set; }
    }
}
