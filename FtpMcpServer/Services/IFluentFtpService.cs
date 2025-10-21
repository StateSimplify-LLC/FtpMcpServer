using System;
using System.Collections.Generic;
using FluentFTP;

namespace FtpMcpServer.Services
{
    public interface IFluentFtpService
    {
        IReadOnlyList<FtpListItem> GetListing(FtpDefaults defaults, string path);

        byte[] DownloadBytes(FtpDefaults defaults, string path);

        void UploadBytes(FtpDefaults defaults, string path, byte[] data);

        void DeleteFile(FtpDefaults defaults, string path);

        void CreateDirectory(FtpDefaults defaults, string path);

        void DeleteDirectory(FtpDefaults defaults, string path);

        void Rename(FtpDefaults defaults, string sourcePath, string destinationPath);

        long GetFileSize(FtpDefaults defaults, string path);

        DateTime GetModifiedTime(FtpDefaults defaults, string path);
    }
}
