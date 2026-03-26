using System;
using System.Collections.Generic;
using System.Text;

namespace ReportPackaging.Data.Sew.IService
{
    public interface IBlobStorageService
    {
        Task<string> UploadFileToBlobAsync(string strFileName, string contentType, Stream fileStream);
        Task<bool> DeleteFileToBlobAsync(string strFileName);
    }
}