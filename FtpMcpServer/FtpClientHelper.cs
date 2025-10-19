using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;

namespace FtpMcpServer
{
    internal sealed class FtpConnectionInfo
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string Path { get; set; } = "/";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseSsl { get; set; }
        public bool Passive { get; set; } = true;
        public bool IgnoreCertErrors { get; set; }
        public int TimeoutSeconds { get; set; } = 30;

        public Uri BuildUri()
        {
            // Ensure path is rooted and properly escaped
            string p = Path;
            if (string.IsNullOrEmpty(p)) p = "/";
            if (!p.StartsWith("/", StringComparison.Ordinal)) p = "/" + p;
            // We assume p is not already URI-escaped. Escape segments but keep '/'.
            string[] segments = p.Split(new[] { '/' }, StringSplitOptions.None);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0) continue;
                segments[i] = Uri.EscapeDataString(segments[i]);
            }
            string escaped = string.Join("/", segments);
            string uriStr = $"ftp://{Host}:{Port}{escaped}";
            return new Uri(uriStr, UriKind.Absolute);
        }
    }

    internal static class FtpClientHelper
    {
        public static FtpWebRequest CreateRequest(FtpConnectionInfo info, string method)
        {
            if (string.IsNullOrWhiteSpace(info.Host))
                throw new ArgumentException("FTP host must be provided.");

            var uri = info.BuildUri();
            var req = (FtpWebRequest)WebRequest.Create(uri);
            req.Method = method;
            req.Credentials = (info.Username != null) ? new NetworkCredential(info.Username, info.Password ?? string.Empty) : CredentialCache.DefaultNetworkCredentials;
            req.EnableSsl = info.UseSsl;
            req.UsePassive = info.Passive;
            req.UseBinary = true;
            req.KeepAlive = false;
            req.ReadWriteTimeout = Math.Max(1000, info.TimeoutSeconds * 1000);
            req.Timeout = Math.Max(1000, info.TimeoutSeconds * 1000);

            if (info.UseSsl && info.IgnoreCertErrors)
            {
                // This is global, so we restore original if multiple calls changed it.
                ServicePointManager.ServerCertificateValidationCallback = IgnoreValidationCallback;
            }

            return req;
        }

        private static bool IgnoreValidationCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;

        public static byte[] Download(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.DownloadFile);
            using var resp = (FtpWebResponse)req.GetResponse();
            using var stream = resp.GetResponseStream();
            if (stream == null) return Array.Empty<byte>();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public static void Upload(FtpConnectionInfo info, byte[] data)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.UploadFile);
            req.ContentLength = data.Length;
            using (var reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
            }
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        public static void Delete(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.DeleteFile);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        public static void MakeDirectory(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.MakeDirectory);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        public static void RemoveDirectory(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.RemoveDirectory);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        public static void Rename(FtpConnectionInfo info, string newName)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.Rename);
            req.RenameTo = newName;
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        public static long GetFileSize(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.GetFileSize);
            using var resp = (FtpWebResponse)req.GetResponse();
            return resp.ContentLength;
        }

        public static DateTime GetModifiedTime(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.GetDateTimestamp);
            using var resp = (FtpWebResponse)req.GetResponse();
            return resp.LastModified;
        }

        public static string PrintWorkingDirectory(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.PrintWorkingDirectory);
            using var resp = (FtpWebResponse)req.GetResponse();
            using var stream = resp.GetResponseStream();
            if (stream == null) return "/";
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        }

        public static List<FtpItem> ListDirectoryDetails(FtpConnectionInfo info)
        {
            var req = CreateRequest(info, WebRequestMethods.Ftp.ListDirectoryDetails);
            var items = new List<FtpItem>();
            using var resp = (FtpWebResponse)req.GetResponse();
            using var stream = resp.GetResponseStream();
            if (stream == null) return items;
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (TryParseUnixListLine(line, out var unixItem))
                    items.Add(unixItem);
                else if (TryParseWindowsListLine(line, out var winItem))
                    items.Add(winItem);
                else
                    items.Add(new FtpItem { Name = line.Trim(), IsDirectory = false, Size = null, Modified = null, Raw = line });
            }
            return items;
        }

        private static bool TryParseUnixListLine(string line, out FtpItem item)
        {
            // Example: drwxr-xr-x  2 user group     4096 Jan 10 10:00 folder
            //          -rw-r--r--  1 user group     1234 Jan 20  2023 file.txt
            item = new FtpItem { Raw = line };
            if (string.IsNullOrWhiteSpace(line) || line.Length < 10) return false;

            string perms = line.Substring(0, 10);
            if (!(perms[0] == 'd' || perms[0] == '-')) return false;

            item.Permissions = perms;
            item.IsDirectory = perms[0] == 'd';

            // Split by whitespace after permissions
            string remainder = line.Substring(10).Trim();
            var parts = SplitByWhitespace(remainder, 7); // perms + (links) (owner) (group) (size) (month) (day) (time/year) name
            if (parts.Count < 7) return false;

            // Identify fields in a tolerant way
            int idx = 0;
            // parts[0] = link count
            if (!long.TryParse(parts[idx++], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                // Sometimes link count missing; shift index back
                idx--;
            }
            // owner
            if (idx < parts.Count) idx++;
            // group
            if (idx < parts.Count) idx++;
            // size
            long sizeValue = 0;
            if (idx < parts.Count && long.TryParse(parts[idx], NumberStyles.Integer, CultureInfo.InvariantCulture, out sizeValue))
            {
                item.Size = sizeValue;
                idx++;
            }

            // date/time fields
            if (idx + 2 < parts.Count)
            {
                string month = parts[idx++];
                string day = parts[idx++];
                string timeOrYear = parts[idx++];

                string dateStr = $"{month} {day} {timeOrYear}";
                DateTime mod;
                if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out mod))
                {
                    item.Modified = mod;
                }
            }

            // The remainder is the file/folder name (including spaces). Obtain from original line.
            // To be robust, find last occurrence of ' time/year ' and take the substring after it.
            try
            {
                // Find name by taking part after the third whitespace-delimited date field
                string[] tokens = remainder.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 7)
                {
                    // reconstruct name by removing first N tokens
                    int removeCount = idx; // how many tokens consumed
                    var remaining = new List<string>();
                    string[] all = remainder.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (i >= removeCount)
                            remaining.Add(all[i]);
                    }
                    item.Name = string.Join(" ", remaining).Trim();
                }
                else
                {
                    item.Name = parts[parts.Count - 1];
                }
            }
            catch
            {
                // Fallback: last token
                item.Name = parts[parts.Count - 1];
            }

            return !string.IsNullOrWhiteSpace(item.Name);
        }

        private static bool TryParseWindowsListLine(string line, out FtpItem item)
        {
            // Example: 01-10-23  02:14PM       <DIR>          folder
            //          01-10-23  02:14PM                 1234 file.txt
            item = new FtpItem { Raw = line };
            if (string.IsNullOrWhiteSpace(line)) return false;

            var parts = SplitByWhitespace(line, 4);
            if (parts.Count < 4) return false;

            DateTime dt;
            if (!DateTime.TryParse(parts[0] + " " + parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                return false;

            item.Modified = dt;
            string dirOrSize = parts[2];
            if (dirOrSize.Equals("<DIR>", StringComparison.OrdinalIgnoreCase))
            {
                item.IsDirectory = true;
                item.Size = null;
            }
            else
            {
                item.IsDirectory = false;
                long sizeVal;
                if (long.TryParse(dirOrSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out sizeVal))
                    item.Size = sizeVal;
            }

            // Name is remainder after first three tokens
            int firstNameIndex = IndexOfNthWhitespaceSeparated(line, 3);
            if (firstNameIndex >= 0 && firstNameIndex < line.Length)
            {
                item.Name = line.Substring(firstNameIndex).Trim();
            }
            else
            {
                item.Name = parts[3];
            }
            return !string.IsNullOrWhiteSpace(item.Name);
        }

        private static int IndexOfNthWhitespaceSeparated(string s, int tokensToSkip)
        {
            int i = 0;
            int tokensSeen = 0;

            while (i < s.Length)
            {
                // skip whitespace
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                if (i >= s.Length) break;

                // consume token
                while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
                tokensSeen++;

                if (tokensSeen >= tokensToSkip)
                {
                    while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                    return i < s.Length ? i : -1;
                }
            }

            return -1;
        }

        private static List<string> SplitByWhitespace(string input, int maxParts)
        {
            var list = new List<string>();
            int i = 0;
            while (i < input.Length && list.Count < maxParts - 1)
            {
                // Skip leading whitespace
                while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
                if (i >= input.Length) break;

                int start = i;
                while (i < input.Length && !char.IsWhiteSpace(input[i])) i++;
                list.Add(input.Substring(start, i - start));
            }

            if (i < input.Length)
            {
                // Add remainder as last part
                int start = i;
                while (start < input.Length && char.IsWhiteSpace(input[start])) start++;
                if (start < input.Length) list.Add(input.Substring(start));
            }

            return list;
        }
    }

    internal sealed class FtpItem
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long? Size { get; set; }
        public DateTime? Modified { get; set; }
        public string? Permissions { get; set; }
        public string? Raw { get; set; }
    }

    internal static class MimeHelper
    {
        public static string GetMimeType(string path)
        {
            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            switch (ext)
            {
                case ".txt": return "text/plain";
                case ".json": return "application/json";
                case ".csv": return "text/csv";
                case ".xml": return "application/xml";
                case ".html":
                case ".htm": return "text/html";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".pdf": return "application/pdf";
                case ".zip": return "application/zip";
                default: return "application/octet-stream";
            }
        }
    }
}
