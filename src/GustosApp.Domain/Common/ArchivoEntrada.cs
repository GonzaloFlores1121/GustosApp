using System.IO;

namespace GustosApp.Domain.Common
{
    public class ArchivoEntrada
    {
        public Stream Stream { get; set; }
        public string FileName { get; set; }

        public ArchivoEntrada(Stream stream, string fileName)
        {
            Stream = stream;
            FileName = fileName;
        }
    }
}