using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> UploadFileAsync(Stream stream, string nombreArchivo, string? carpeta = null);

        Task DeleteFileAsync(string url);

        Task DeleteFileAsync(string url, string expectedFolder);

        Task ScheduleCleanupAsync(string url, string expectedFolder);
    }
}
