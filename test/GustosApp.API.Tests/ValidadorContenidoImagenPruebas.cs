using System.Buffers.Binary;
using GustosApp.Infraestructure.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace GustosApp.API.Tests;

public sealed class ValidadorContenidoImagenPruebas
{
    [Fact]
    public async Task Validar_ImagenPngReal_AceptaYRestableceLaPosicion()
    {
        await using var contenido = CrearPng();

        var mimeType = await ValidadorContenidoImagen.ValidarAsync(contenido, "imagen.png");

        Assert.Equal("image/png", mimeType);
        Assert.Equal(0, contenido.Position);
    }

    [Fact]
    public async Task Validar_ArchivoDisfrazadoComoImagen_LoRechaza()
    {
        await using var contenido = new MemoryStream("esto no es una imagen"u8.ToArray());

        var accion = () => ValidadorContenidoImagen.ValidarAsync(contenido, "imagen.jpg");

        await Assert.ThrowsAsync<ArgumentException>(accion);
    }

    [Fact]
    public async Task Validar_ExtensionDistintaAlContenido_LaRechaza()
    {
        await using var contenido = CrearPng();

        var excepcion = await Assert.ThrowsAsync<ArgumentException>(
            () => ValidadorContenidoImagen.ValidarAsync(contenido, "imagen.jpg"));

        Assert.Contains("no coincide", excepcion.Message);
        Assert.Equal(0, contenido.Position);
    }

    [Fact]
    public async Task Validar_ImagenCorruptaAunqueTengaCabecera_LaRechaza()
    {
        await using var completa = CrearPng();
        var bytes = completa.ToArray();
        var inicioTipoIdat = bytes.AsSpan().IndexOf("IDAT"u8);
        var longitudIdat = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(inicioTipoIdat - 4, 4));
        bytes.AsSpan(inicioTipoIdat + 4, longitudIdat).Fill(255);
        await using var corrupta = new MemoryStream(bytes);

        var accion = () => ValidadorContenidoImagen.ValidarAsync(corrupta, "imagen.png");

        await Assert.ThrowsAsync<ArgumentException>(accion);
    }

    [Fact]
    public async Task Validar_PngSinChunkFinal_LoRechaza()
    {
        using var contenido = CrearPng();
        contenido.SetLength(contenido.Length - 12); // IEND vacío con cabecera y CRC.

        await Assert.ThrowsAsync<ArgumentException>(
            () => ValidadorContenidoImagen.ValidarAsync(contenido, "imagen.png"));
    }

    [Theory]
    [InlineData(12_001, 2)]
    [InlineData(6_400, 6_400)]
    public async Task Validar_DimensionesOAreaExcesivas_LasRechaza(int ancho, int alto)
    {
        // La identificación de JPEG requiere dimensiones y marcadores, no píxeles.
        using var imagen = new Image<Rgba32>(2, 2);
        using var original = new MemoryStream();
        imagen.SaveAsJpeg(original);
        var bytes = original.ToArray();
        var marcadorSof = bytes.AsSpan().IndexOf(new byte[] { 0xff, 0xc0 });
        Assert.True(marcadorSof >= 0);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(marcadorSof + 5), (ushort)alto);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(marcadorSof + 7), (ushort)ancho);
        using var contenido = new MemoryStream(bytes);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => ValidadorContenidoImagen.ValidarAsync(contenido, "imagen.jpg"));

        Assert.Contains("dimensiones", error.Message);
        Assert.Equal(0, contenido.Position);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task Normalizar_ImagenAnimada_LaRechazaSinExportarElPrimerFrame(string extension)
    {
        using var imagen = new Image<Rgba32>(2, 2, Color.Red);
        using var segundoFrame = new Image<Rgba32>(2, 2, Color.Blue);
        imagen.Frames.AddFrame(segundoFrame.Frames.RootFrame);
        IImageEncoder codificador = extension == "png" ? new PngEncoder() : new WebpEncoder();
        using var contenido = new MemoryStream();
        imagen.Save(contenido, codificador);
        contenido.Position = 0;

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => ValidadorContenidoImagen.NormalizarAsync(contenido, $"imagen.{extension}"));

        Assert.Contains("animadas", error.Message);
        Assert.Equal(0, contenido.Position);
    }

    [Fact]
    public async Task Normalizar_WebpEstatico_ConservaFormatoYPosicionInicial()
    {
        using var imagen = new Image<Rgba32>(2, 3);
        using var contenido = new MemoryStream();
        contenido.Write("prefijo"u8);
        var inicio = contenido.Position;
        imagen.SaveAsWebp(contenido);
        contenido.Position = inicio;

        var (salida, mimeType) = await ValidadorContenidoImagen.NormalizarAsync(contenido, "imagen.webp");
        using (salida)
        {
            using var normalizada = Image.Load(salida);
            Assert.Equal(WebpFormat.Instance, normalizada.Metadata.DecodedImageFormat);
            Assert.Equal(imagen.Size, normalizada.Size);
        }
        Assert.Equal("image/webp", mimeType);
        Assert.Equal(inicio, contenido.Position);
    }

    private static MemoryStream CrearPng()
    {
        using var imagen = new Image<Rgba32>(2, 2);
        var contenido = new MemoryStream();
        imagen.SaveAsPng(contenido);
        contenido.Position = 0;
        return contenido;
    }
}
