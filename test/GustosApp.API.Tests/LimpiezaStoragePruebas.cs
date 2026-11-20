using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.API.Tests;

public sealed class LimpiezaStoragePruebas
{
    private const string Bucket = "test.appspot.com";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly string Folder = $"perfil/{OwnerId:N}";
    private static readonly string ObjectName = $"{Folder}/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa_20260930124530.jpg";
    private static readonly string Url = $"https://firebasestorage.googleapis.com/v0/b/{Bucket}/o/{Uri.EscapeDataString(ObjectName)}?alt=media";

    [Fact]
    public async Task NoBorraObjetosFueraDelPropietarioNiDelBucket()
    {
        using var provider = CrearProvider();
        using var scope = provider.CreateScope();
        var fake = new FakeClient();
        var sut = CrearServicio(fake, scope, provider);

        await sut.DeleteFileAsync(Url, $"perfil/{Guid.NewGuid():N}");
        await sut.DeleteFileAsync(Url.Replace(Bucket, "otro.appspot.com"), Folder);

        Assert.Empty(fake.Deleted);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<GustosDbContext>().EliminacionesArchivoPendientes);
    }

    [Fact]
    public async Task LimpiezaDiferidaEsIdempotenteYEsperaHastaQueLaFotoDejeDeEstarReferenciada()
    {
        Assert.True(FirebaseStorageService.TryExtractOwnedObjectName(Url, Bucket, Folder, out _));
        using var provider = CrearProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        var user = new Usuario("uid", "usuario@test.com", "Ana", "Pérez", "ana")
            { Id = OwnerId, FotoPerfilUrl = Url };
        db.Usuarios.Add(user);
        await db.SaveChangesAsync();
        var fake = new FakeClient();
        var sut = CrearServicio(fake, scope, provider);

        await sut.ScheduleCleanupAsync(Url, Folder);
        await sut.ScheduleCleanupAsync(Url, Folder);
        Assert.Single(db.EliminacionesArchivoPendientes);

        await sut.ProcesarPendientesAsync(default);
        Assert.Empty(fake.Deleted);
        Assert.Single(db.EliminacionesArchivoPendientes);

        user.FotoPerfilUrl = null;
        db.EliminacionesArchivoPendientes.Single().ProximoIntentoUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await sut.ProcesarPendientesAsync(default);

        Assert.Single(fake.Deleted);
        Assert.Equal(ObjectName, fake.Deleted[0]);
        Assert.Empty(db.EliminacionesArchivoPendientes);
    }

    [Fact]
    public async Task ErrorDeStorageQuedaPendienteYSeReintenta()
    {
        Assert.True(FirebaseStorageService.TryExtractOwnedObjectName(Url, Bucket, Folder, out _));
        using var provider = CrearProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
        var fake = new FakeClient { FailNextDelete = true };
        var sut = CrearServicio(fake, scope, provider);

        await sut.DeleteFileAsync(Url, Folder);
        Assert.Single(db.EliminacionesArchivoPendientes);
        Assert.Empty(fake.Deleted);

        await sut.ProcesarPendientesAsync(default);
        Assert.Single(fake.Deleted);
        Assert.Empty(db.EliminacionesArchivoPendientes);
    }

    private static ServiceProvider CrearProvider()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<GustosDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private static FirebaseStorageService CrearServicio(FakeClient fake, IServiceScope scope, IServiceProvider provider) =>
        new(fake, Bucket, scope.ServiceProvider.GetRequiredService<GustosDbContext>(),
            provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<FirebaseStorageService>.Instance);

    private sealed class FakeClient : IFirebaseObjectClient
    {
        public bool FailNextDelete { get; set; }
        public List<string> Deleted { get; } = [];
        public Task UploadAsync(string bucket, string objectName, string mimeType, Stream content) => Task.CompletedTask;
        public Task DeleteAsync(string bucket, string objectName)
        {
            if (FailNextDelete)
            {
                FailNextDelete = false;
                throw new IOException("Storage no disponible");
            }
            Deleted.Add(objectName);
            return Task.CompletedTask;
        }
    }
}
