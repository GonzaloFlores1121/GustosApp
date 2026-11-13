using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace GustosApp.Application.Common.Imagenes;

/// <summary>Limita los formatos y verifica las imágenes antes de decodificar sus píxeles.</summary>
public static class InspeccionImagen
{
    private static readonly SixLabors.ImageSharp.Configuration Formatos = new(
        new JpegConfigurationModule(), new PngConfigurationModule(),
        new WebpConfigurationModule(), new GifConfigurationModule());

    private static readonly DecoderOptions Identificacion = new()
    {
        Configuration = Formatos,
        SkipMetadata = true,
        MaxFrames = 1
    };

    private static readonly DecoderOptions Decodificacion = new()
    {
        Configuration = Formatos,
        MaxFrames = 1
    };

    /// <summary>Carga una imagen estática y restablece la posición del flujo.</summary>
    public static async Task<Image> CargarEstaticaAsync(Stream contenido, string mimeType,
        bool conservarMetadatos, CancellationToken ct = default)
    {
        var inicio = contenido.Position;
        try
        {
            var formato = await Image.DetectFormatAsync(Identificacion, contenido, ct);
            contenido.Position = inicio;
            ValidarFormatoEstatico(formato, mimeType, contenido, ct);
            contenido.Position = inicio;
            var info = await Image.IdentifyAsync(Identificacion, contenido, ct);
            ValidarDimensiones(info);
            contenido.Position = inicio;
            return await Image.LoadAsync(conservarMetadatos ? Decodificacion : Identificacion, contenido, ct);
        }
        finally
        {
            contenido.Position = inicio;
        }
    }

    /// <summary>Comprueba una imagen estática JPEG o PNG sin conservar metadatos.</summary>
    public static void ValidarComprobante(byte[] bytes, string mimeType)
    {
        using var contenido = new MemoryStream(bytes, writable: false);
        var formato = Image.DetectFormat(Identificacion, contenido);
        contenido.Position = 0;
        ValidarFormatoEstatico(formato, mimeType, contenido, default);
        contenido.Position = 0;
        ValidarDimensiones(Image.Identify(Identificacion, contenido));
        contenido.Position = 0;
        using var imagen = Image.Load(Identificacion, contenido);
    }

    /// <summary>Identifica una foto externa cuyo tipo declarado coincide con su firma.</summary>
    public static async Task<ImageInfo?> IdentificarFotoAsync(Stream contenido, string mimeType, CancellationToken ct)
    {
        var inicio = contenido.Position;
        var formato = await Image.DetectFormatAsync(Identificacion, contenido, ct);
        contenido.Position = inicio;
        return formato.DefaultMimeType == mimeType ? await Image.IdentifyAsync(Identificacion, contenido, ct) : null;
    }

    private static void ValidarFormatoEstatico(IImageFormat formato, string mimeType, Stream contenido,
        CancellationToken ct)
    {
        if (formato.DefaultMimeType != mimeType)
            throw new ArgumentException("La extensión de la imagen no coincide con su contenido real.");

        // ImageSharp 3.x no informa los frames APNG al identificar. Verificamos los
        // chunks de animación y el indicador WebP antes de cargar un solo frame.
        var animada = formato == PngFormat.Instance ? TieneChunksApng(contenido, ct)
            : formato == WebpFormat.Instance && EsWebpAnimado(contenido);
        if (animada)
            throw new ArgumentException("No se permiten imágenes animadas.");
    }

    private static void ValidarDimensiones(ImageInfo info)
    {
        if (info.Width is <= 0 or > 12_000 || info.Height is <= 0 or > 12_000 ||
            (long)info.Width * info.Height > 40_000_000)
            throw new ArgumentException("Las dimensiones de la imagen son demasiado grandes.");
    }

    private static bool TieneChunksApng(Stream contenido, CancellationToken ct)
    {
        contenido.Position += 8; // Firma PNG.
        Span<byte> cabecera = stackalloc byte[8];
        while (contenido.Position < contenido.Length)
        {
            ct.ThrowIfCancellationRequested();
            contenido.ReadExactly(cabecera);
            var tipo = cabecera[4..];
            var longitudConCrc = BinaryPrimitives.ReadUInt32BigEndian(cabecera) + 4L;
            if (longitudConCrc > contenido.Length - contenido.Position)
                throw new EndOfStreamException();
            if (tipo.SequenceEqual("acTL"u8) || tipo.SequenceEqual("fcTL"u8) || tipo.SequenceEqual("fdAT"u8))
                return true;
            if (tipo.SequenceEqual("IEND"u8))
                return false;
            contenido.Position += longitudConCrc;
        }
        throw new EndOfStreamException();
    }

    private static bool EsWebpAnimado(Stream contenido)
    {
        contenido.Position += 12; // Cabecera RIFF/WEBP.
        Span<byte> cabecera = stackalloc byte[9];
        contenido.ReadExactly(cabecera);
        return cabecera[..4].SequenceEqual("VP8X"u8) && (cabecera[8] & 0b0000_0010) != 0;
    }
}
