using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using FtpMcpServer.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Tools
{
    [McpServerToolType]
    public class FtpTools
    {
        [McpServerTool(Name = "ftp_listDirectory", UseStructuredContent = true, ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Lists entries in an FTP directory. Returns structured JSON describing each item.")]
        public static FtpListResult ListDirectory(
            FtpDefaults defaults,
            [Description("Remote path to list (e.g., /pub). Defaults to the server default path or '/'")] string? path = null)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            var items = FluentFtpService.GetListing(defaults, remotePath);
            var result = new FtpListResult
            {
                Host = FtpPathHelper.GetHostOrThrow(defaults),
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
                    Raw = it.RawListing
                });
            }

            return result;
        }

        [McpServerTool(Name = "ftp_downloadFile", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Downloads an FTP file and returns it as an embedded MCP resource (base64-encoded).")]
        public static ContentBlock DownloadFile(
            FtpDefaults defaults,
            [Description("Remote file path to download (e.g., /pub/file.txt)")] string path)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            var bytes = FluentFtpService.DownloadBytes(defaults, remotePath);
            string b64 = Convert.ToBase64String(bytes);
            string uri = FtpPathHelper.BuildUri(defaults, remotePath).ToString();
            string mime = MimeHelper.GetMimeType(remotePath);

            return new EmbeddedResourceBlock
            {
                Resource = new BlobResourceContents
                {
                    Uri = uri,
                    MimeType = mime,
                    Blob = b64
                }
            };
        }

        [McpServerTool(Name = "ftp_uploadFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Uploads data as a file to the FTP server. Data is expected to be base64-encoded.")]
        public static string UploadFile(
            FtpDefaults defaults,
            [Description("Remote file path to upload to (e.g., /incoming/file.txt)")] string path,
            [Description("Base64-encoded file content to upload")] string dataBase64)
        {
            var bytes = Convert.FromBase64String(dataBase64);
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            FluentFtpService.UploadBytes(defaults, remotePath, bytes);
            return $"Uploaded {bytes.Length} bytes to {FtpPathHelper.BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_writeFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Writes plain text content to a file on the FTP server using the specified encoding (UTF-8 by default).")]
        public static string WriteFile(
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
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            FluentFtpService.UploadBytes(defaults, remotePath, bytes);
            return $"Wrote {bytes.Length} bytes to {FtpPathHelper.BuildUri(defaults, remotePath)} using {enc.WebName} encoding";
        }

        [McpServerTool(Name = "ftp_deleteFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Deletes a file on the FTP server.")]
        public static string DeleteFile(
            FtpDefaults defaults,
            [Description("Remote file path to delete (e.g., /pub/file.txt)")] string path)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            FluentFtpService.DeleteFile(defaults, remotePath);
            return $"Deleted {FtpPathHelper.BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_makeDirectory", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Creates a directory on the FTP server.")]
        public static string MakeDirectory(
            FtpDefaults defaults,
            [Description("Remote directory path to create (e.g., /pub/newdir)")] string path)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            FluentFtpService.CreateDirectory(defaults, remotePath);
            return $"Created directory {FtpPathHelper.BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_removeDirectory", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Removes a directory on the FTP server (must be empty).")]
        public static string RemoveDirectory(
            FtpDefaults defaults,
            [Description("Remote directory path to remove (must be empty)")] string path)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            FluentFtpService.DeleteDirectory(defaults, remotePath);
            return $"Removed directory {FtpPathHelper.BuildUri(defaults, remotePath)}";
        }

        [McpServerTool(Name = "ftp_rename", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Renames a file or directory on the FTP server.")]
        public static string Rename(
            FtpDefaults defaults,
            [Description("Current remote path to rename")] string path,
            [Description("New name (not a full path)")] string newName)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            var destinationPath = FtpPathHelper.CombineDirectoryAndName(remotePath, newName);
            FluentFtpService.Rename(defaults, remotePath, destinationPath);
            return $"Renamed {FtpPathHelper.BuildUri(defaults, remotePath)} to {newName}";
        }

        [McpServerTool(Name = "ftp_getFileSize", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Gets the size (in bytes) of a remote file.")]
        public static long GetFileSize(
            FtpDefaults defaults,
            [Description("Remote file path")] string path)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            return FluentFtpService.GetFileSize(defaults, remotePath);
        }

        [McpServerTool(Name = "ftp_getModifiedTime", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Gets the last modified time (server local time) of a remote file.")]
        public static DateTime GetModifiedTime(
            FtpDefaults defaults,
            [Description("Remote file path")] string path)
        {
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);
            return FluentFtpService.GetModifiedTime(defaults, remotePath);
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