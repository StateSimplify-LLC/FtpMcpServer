namespace FtpMcpServer
{
    internal static class MimeHelper
    {
        public static string GetMimeType(string path)
        {
            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            return ext switch
            {
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".csv" => "text/csv",
                ".xml" => "application/xml",
                ".html" or ".htm" => "text/html",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                _ => "application/octet-stream",
            };
        }
    }
}
