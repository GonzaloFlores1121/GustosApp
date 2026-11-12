using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace GustosApp.Infraestructure.Files;

public static class ValidadorContenidoImagen
{
    public const long LimiteBytesPredeterminado = 5_000_000;
    private const int DimensionMaxima = 12_000;
    private const long PixelesMaximos = 40_000_000;

    private static readonly IReadOnlyDictionary<string, (string Formato, string MimeType)> FormatosPermitidos =
        new Dictionary<string, (string Formato, string MimeType)>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ("JPEG", "image/jpeg"),
            [".jpeg"] = ("JPEG", "image/jpeg"),
            [".png"] = ("PNG", "image/png"),
            [".webp"] = ("WEBP", "image/webp")
        };

    public static async Task<(MemoryStream Contenido, string MimeType)> NormalizarAsync(
        Stream contenido, string nombreArchivo, CancellationToken ct = default)
    {
        var mimeType = await ValidarAsync(contenido, nombreArchivo, ct: ct);
        using var image = await Image.LoadAsync(contenido, ct);
        image.Mutate(context => context.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.CicpProfile = null;
        var normalized = new MemoryStream();
        try
        {
            switch (Path.GetExtension(nombreArchivo).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    await image.SaveAsync(normalized, new JpegEncoder { Quality = 88, SkipMetadata = true }, ct);
                    break;
                case ".png":
                    await image.SaveAsync(normalized, new PngEncoder { SkipMetadata = true }, ct);
                    break;
                case ".webp":
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
        if (contenido is null || !contenido.CanRead || !contenido.CanSeek)
            throw new ArgumentException("No se pudo leer la imagen.");

        var posicionInicial = contenido.Position;
        var longitud = contenido.Length - posicionInicial;
        if (longitud <= 0 || longitud > limiteBytes)
            throw new ArgumentException($"La imagen debe tener contenido y no superar {limiteBytes / 1_000_000} MB.");

        var extension = Path.GetExtension(nombreArchivo);
        if (!FormatosPermitidos.TryGetValue(extension, out var esperado))
            throw new ArgumentException("El formato de imagen no está permitido. Usá JPG, PNG o WEBP.");

        try
        {
            var informacion = await Image.IdentifyAsync(contenido, ct);
            var formatoDetectado = informacion?.Metadata.DecodedImageFormat?.Name;
            if (informacion is null || !string.Equals(formatoDetectado, esperado.Formato, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La extensión de la imagen no coincide con su contenido real.");

            var cantidadPixeles = checked((long)informacion.Width * informacion.Height);
            if (informacion.Width > DimensionMaxima || informacion.Height > DimensionMaxima || cantidadPixeles > PixelesMaximos)
                throw new ArgumentException("Las dimensiones de la imagen son demasiado grandes.");

            contenido.Position = posicionInicial;
            using var imagen = await Image.LoadAsync(contenido, ct);
            if (imagen.Frames.Count != 1)
                throw new ArgumentException("No se permiten imágenes animadas.");

            return esperado.MimeType;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException
                                   or NotSupportedException or EndOfStreamException or OverflowException)
        {
            throw new ArgumentException("El archivo no contiene una imagen válida y completa.", ex);
        }
        finally
        {
            contenido.Position = posicionInicial;
        }
    }
}
