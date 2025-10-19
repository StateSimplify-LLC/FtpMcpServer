using System;
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Resources
{
    [McpServerResourceType]
    public class FtpResourceType
    {
        // We define a resource template that reads a file from FTP based on query parameters.
        // Path is taken from the 'path' query argument. For slashes in path, the client should URL-encode them as %2F.
        [McpServerResource(
            UriTemplate = "resource://ftp/file{?host,port,path,username,password,useSsl,passive,ignoreCertErrors,timeoutSeconds}",
            Name = "ftp_file",
            MimeType = "application/octet-stream")]
        [Description("Reads a file from an FTP server and returns it as a resource.")]
        public static ResourceContents Read(
            RequestContext<ReadResourceRequestParams> requestContext,
            FtpMcpServer.FtpDefaults defaults,
            [Description("FTP host name or IP address")] string? host = null,
            [Description("Remote path to the file (URL-encoded if it includes slashes)")] string? path = null,
            [Description("Server port, defaults to 21")] int? port = null,
            [Description("Username if authentication is required")] string? username = null,
            [Description("Password for the specified username")] string? password = null,
            [Description("Use explicit FTPS (TLS). Defaults to false")] bool? useSsl = null,
            [Description("Use passive mode. Defaults to true")] bool? passive = null,
            [Description("Ignore TLS certificate errors (INSECURE). Defaults to false")] bool? ignoreCertErrors = null,
            [Description("Timeout in seconds, defaults to 30")] int? timeoutSeconds = null)
        {
            var info = new FtpMcpServer.FtpConnectionInfo
            {
                Host = host ?? defaults.Host ?? throw new ArgumentException("Host is required"),
                Port = port ?? defaults.Port,
                Path = path ?? defaults.DefaultPath ?? "/",
                Username = username ?? defaults.Username,
                Password = password ?? defaults.Password,
                UseSsl = useSsl ?? defaults.UseSsl,
                Passive = passive ?? defaults.Passive,
                IgnoreCertErrors = ignoreCertErrors ?? defaults.IgnoreCertErrors,
                TimeoutSeconds = timeoutSeconds ?? defaults.TimeoutSeconds
            };

            byte[] bytes = FtpMcpServer.FtpClientHelper.Download(info);
            string b64 = Convert.ToBase64String(bytes);
            string mime = FtpMcpServer.MimeHelper.GetMimeType(info.Path);

            return new BlobResourceContents
            {
                Uri = requestContext.Params?.Uri ?? info.BuildUri().ToString(),
                MimeType = mime,
                Blob = b64
            };
        }
    }
}
