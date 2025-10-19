using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Tools
{
    [McpServerToolType]
    public class FtpTools
    {
        private static FtpConnectionInfo BuildInfo(
            FtpDefaults defaults,
            string? path)
        {
            return new FtpConnectionInfo
            {
                Host = defaults.Host ?? throw new ArgumentException("Host is required"),
                Port = defaults.Port,
                Path = path ?? defaults.DefaultPath ?? "/",
                Username = defaults.Username,
                Password = defaults.Password,
                UseSsl = defaults.UseSsl,
                Passive = defaults.Passive,
                IgnoreCertErrors = defaults.IgnoreCertErrors,
                TimeoutSeconds = defaults.TimeoutSeconds
            };
        }

        [McpServerTool(Name = "ftp_listDirectory", UseStructuredContent = true, ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Lists entries in an FTP directory. Returns structured JSON describing each item.")]
        public static FtpListResult ListDirectory(
            FtpDefaults defaults,
            [Description("Remote path to list (e.g., /pub). Defaults to the server default path or '/'")] string? path = null)
        {
            var info = BuildInfo(defaults, path);
            var items = FtpClientHelper.ListDirectoryDetails(info);
            var result = new FtpListResult
            {
                Host = info.Host,
                Path = info.Path,
                Port = info.Port,
                UseSsl = info.UseSsl,
                Passive = info.Passive,
                Items = new List<FtpListItem>()
            };

            foreach (var it in items)
            {
                result.Items.Add(new FtpListItem
                {
                    Name = it.Name,
                    IsDirectory = it.IsDirectory,
                    Size = it.Size,
                    Modified = it.Modified,
                    Permissions = it.Permissions,
                    Raw = it.Raw
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
            var info = BuildInfo(defaults, path);
            var bytes = FtpClientHelper.Download(info);
            string b64 = Convert.ToBase64String(bytes);
            string uri = info.BuildUri().ToString();
            string mime = MimeHelper.GetMimeType(info.Path);

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
            var info = BuildInfo(defaults, path);
            var bytes = Convert.FromBase64String(dataBase64);
            FtpClientHelper.Upload(info, bytes);
            return $"Uploaded {bytes.Length} bytes to {info.BuildUri()}";
        }

        [McpServerTool(Name = "ftp_deleteFile", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Deletes a file on the FTP server.")]
        public static string DeleteFile(
            FtpDefaults defaults,
            [Description("Remote file path to delete (e.g., /pub/file.txt)")] string path)
        {
            var info = BuildInfo(defaults, path);
            FtpClientHelper.Delete(info);
            return $"Deleted {info.BuildUri()}";
        }

        [McpServerTool(Name = "ftp_makeDirectory", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Creates a directory on the FTP server.")]
        public static string MakeDirectory(
            FtpDefaults defaults,
            [Description("Remote directory path to create (e.g., /pub/newdir)")] string path)
        {
            var info = BuildInfo(defaults, path);
            FtpClientHelper.MakeDirectory(info);
            return $"Created directory {info.BuildUri()}";
        }

        [McpServerTool(Name = "ftp_removeDirectory", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Removes a directory on the FTP server (must be empty).")]
        public static string RemoveDirectory(
            FtpDefaults defaults,
            [Description("Remote directory path to remove (must be empty)")] string path)
        {
            var info = BuildInfo(defaults, path);
            FtpClientHelper.RemoveDirectory(info);
            return $"Removed directory {info.BuildUri()}";
        }

        [McpServerTool(Name = "ftp_rename", Destructive = true, OpenWorld = true, Idempotent = true)]
        [Description("Renames a file or directory on the FTP server.")]
        public static string Rename(
            FtpDefaults defaults,
            [Description("Current remote path to rename")] string path,
            [Description("New name (not a full path)")] string newName)
        {
            var info = BuildInfo(defaults, path);
            FtpClientHelper.Rename(info, newName);
            return $"Renamed {info.BuildUri()} to {newName}";
        }

        [McpServerTool(Name = "ftp_getFileSize", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Gets the size (in bytes) of a remote file.")]
        public static long GetFileSize(
            FtpDefaults defaults,
            [Description("Remote file path")] string path)
        {
            var info = BuildInfo(defaults, path);
            return FtpClientHelper.GetFileSize(info);
        }

        [McpServerTool(Name = "ftp_getModifiedTime", ReadOnly = true, OpenWorld = true, Idempotent = true)]
        [Description("Gets the last modified time (server local time) of a remote file.")]
        public static DateTime GetModifiedTime(
            FtpDefaults defaults,
            [Description("Remote file path")] string path)
        {
            var info = BuildInfo(defaults, path);
            return FtpClientHelper.GetModifiedTime(info);
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