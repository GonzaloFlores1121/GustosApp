using System.Buffers.Binary;
using GustosApp.Infraestructure.Files;
using SixLabors.ImageSharp;
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

    private static MemoryStream CrearPng()
    {
        using var imagen = new Image<Rgba32>(2, 2);
        var contenido = new MemoryStream();
        imagen.SaveAsPng(contenido);
        contenido.Position = 0;
        return contenido;
    }
}
