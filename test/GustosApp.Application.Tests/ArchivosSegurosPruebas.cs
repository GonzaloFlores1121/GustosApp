using GustosApp.Infraestructure.Files;
using GustosApp.Infraestructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace GustosApp.Application.Tests;

public sealed class ArchivosSegurosPruebas
{
    private static readonly Guid Propietario = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Bucket = "gustos.appspot.com";
    private const string Nombre = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa_20260930124530.jpg";

    private static string Url(string bucket, string objeto) =>
        $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{Uri.EscapeDataString(objeto)}?alt=media";

    [Fact]
    public void SoloSeBorraObjetoGeneradoEnBucketYRecursoEsperados()
    {
        var carpeta = $"perfil/{Propietario:N}";
        var url = Url(Bucket, $"{carpeta}/{Nombre}");
        Assert.True(FirebaseStorageService.TryExtractOwnedObjectName(url, Bucket, carpeta, out var objeto));
        Assert.Equal($"{carpeta}/{Nombre}", objeto);

        Assert.False(FirebaseStorageService.TryExtractOwnedObjectName(url, Bucket,
            $"perfil/{Guid.NewGuid():N}", out _));
        Assert.False(FirebaseStorageService.TryExtractOwnedObjectName(Url("ajeno.appspot.com", $"{carpeta}/{Nombre}"),
            Bucket, carpeta, out _));
        Assert.False(FirebaseStorageService.TryExtractOwnedObjectName(
            $"https://evil.example/v0/b/{Bucket}/o/{Uri.EscapeDataString($"{carpeta}/{Nombre}")}?alt=media",
            Bucket, carpeta, out _));
        Assert.False(FirebaseStorageService.TryExtractOwnedObjectName(Url(Bucket, $"perfil/{Nombre}"),
            Bucket, carpeta, out _));
        Assert.False(FirebaseStorageService.TryExtractOwnedObjectName(Url(Bucket, $"{carpeta}/manual.jpg"),
            Bucket, carpeta, out _));
        Assert.False(FirebaseStorageService.TryExtractOwnedObjectName(url + "&otra=1", Bucket, carpeta, out _));
    }

    [Fact]
    public async Task NormalizacionEliminaExifYConservaOrientacion()
    {
        using var original = new Image<Rgba32>(2, 3);
        original.Metadata.ExifProfile = new ExifProfile();
        original.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        original.Metadata.ExifProfile.SetValue(ExifTag.Software, "GPS PRIVADO");
        using var entrada = new MemoryStream();
        await original.SaveAsync(entrada, new JpegEncoder());
        entrada.Position = 0;

        var (salida, tipo) = await ValidadorContenidoImagen.NormalizarAsync(entrada, "foto.jpg");
        using (salida)
        using (var normalizada = await Image.LoadAsync(salida))
        {
            Assert.Equal("image/jpeg", tipo);
            Assert.Equal(3, normalizada.Width);
            Assert.Equal(2, normalizada.Height);
            Assert.Null(normalizada.Metadata.ExifProfile);
        }
    }

    [Fact]
    public async Task NormalizacionPngConservaTransparencia()
    {
        using var original = new Image<Rgba32>(1, 1);
        original[0, 0] = new Rgba32(255, 0, 0, 0);
        using var entrada = new MemoryStream();
        await original.SaveAsync(entrada, new PngEncoder());
        entrada.Position = 0;
        var (salida, tipo) = await ValidadorContenidoImagen.NormalizarAsync(entrada, "imagen.png");
        using (salida)
        using (var normalizada = await Image.LoadAsync<Rgba32>(salida))
        {
            Assert.Equal("image/png", tipo);
            Assert.Equal(0, normalizada[0, 0].A);
        }
    }
}
