using GustosApp.Application.Common.Imagenes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace GustosApp.Infraestructure.Files;

public static class ValidadorContenidoImagen
{
    public const long LimiteBytesPredeterminado = 5_000_000;
    private static readonly IReadOnlyDictionary<string, string> FormatosPermitidos =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public static async Task<(MemoryStream Contenido, string MimeType)> NormalizarAsync(
        Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        var mimeType = ValidarEntrada(contenido, nombreArchivo, LimiteBytesPredeterminado);
        using var image = await CargarAsync(contenido, mimeType, conservarMetadatos: true, ct);
        image.Mutate(context => context.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.CicpProfile = null;
        var normalized = new MemoryStream();
        try
        {
            switch (mimeType)
            {
                case "image/jpeg":
                    await image.SaveAsync(normalized, new JpegEncoder { Quality = 88, SkipMetadata = true }, ct);
                    break;
                case "image/png":
                    await image.SaveAsync(normalized, new PngEncoder { SkipMetadata = true }, ct);
                    break;
                case "image/webp":
                    await image.SaveAsync(normalized, new WebpEncoder { Quality = 88, SkipMetadata = true }, ct);
                    break;
            }
            if (normalized.Length > LimiteBytesPredeterminado)
                throw new ArgumentException("La imagen procesada supera 5 MB. Elegí una imagen más pequeña.");
            normalized.Position = 0;
            return (normalized, mimeType);
        }
        catch
        {
            await normalized.DisposeAsync();
            throw;
        }
    }

    public static async Task<string> ValidarAsync(
        Stream contenido,
        string nombreArchivo,
        long limiteBytes = LimiteBytesPredeterminado,
        CancellationToken ct = default)
    {
        var mimeType = ValidarEntrada(contenido, nombreArchivo, limiteBytes);
        using var imagen = await CargarAsync(contenido, mimeType, conservarMetadatos: false, ct);
        return mimeType;
    }

    private static string ValidarEntrada(Stream contenido, string nombreArchivo, long limiteBytes)
    {
        if (contenido is null || !contenido.CanRead || !contenido.CanSeek)
            throw new ArgumentException("No se pudo leer la imagen.");

        var longitud = contenido.Length - contenido.Position;
        if (longitud <= 0 || longitud > limiteBytes)
            throw new ArgumentException($"La imagen debe tener contenido y no superar {limiteBytes / 1_000_000} MB.");

        var extension = Path.GetExtension(nombreArchivo);
        if (!FormatosPermitidos.TryGetValue(extension, out var mimeType))
            throw new ArgumentException("El formato de imagen no está permitido. Usá JPG, PNG o WEBP.");

        return mimeType;
    }

    private static async Task<Image> CargarAsync(Stream contenido, string mimeType,
        bool conservarMetadatos, CancellationToken ct)
    {
        try
        {
            return await InspeccionImagen.CargarEstaticaAsync(contenido, mimeType, conservarMetadatos, ct);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException
                                   or NotSupportedException or EndOfStreamException or OverflowException)
        {
            throw new ArgumentException("El archivo no contiene una imagen válida y completa.", ex);
        }
    }
}
