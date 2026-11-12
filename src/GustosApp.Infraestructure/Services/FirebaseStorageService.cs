using System;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using GustosApp.Application.Interfaces;
using GustosApp.Infraestructure.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using System.Net;

namespace GustosApp.Infraestructure.Services
{
    public interface IFirebaseObjectClient
    {
        Task UploadAsync(string bucket, string objectName, string mimeType, Stream content);
        Task DeleteAsync(string bucket, string objectName);
    }

    internal sealed class GoogleFirebaseObjectClient(StorageClient client) : IFirebaseObjectClient
    {
        public async Task UploadAsync(string bucket, string objectName, string mimeType, Stream content) =>
            await client.UploadObjectAsync(bucket, objectName, mimeType, content);

        public async Task DeleteAsync(string bucket, string objectName) =>
            await client.DeleteObjectAsync(bucket, objectName);
    }

    public class FirebaseStorageService : IFileStorageService
    {
        private static readonly Regex ScopedFolder = new(@"^(?:perfil|restaurantes|opiniones|solicitudes|restaurantes/menus-borrador)/[0-9a-f]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex GeneratedName = new(@"^[0-9a-f]{32}_[0-9]{14}\.(?:jpg|jpeg|png|webp)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly IFirebaseObjectClient _storageClient;
        private readonly string _bucketName;
        private readonly ILogger<FirebaseStorageService> _logger;
        private readonly GustosDbContext? _db;
        private readonly IServiceScopeFactory? _scopeFactory;

        public FirebaseStorageService(
            string? firebaseJson,
            string localFilePath,
            IConfiguration config,
            ILogger<FirebaseStorageService> logger,
            GustosDbContext? db = null,
            IServiceScopeFactory? scopeFactory = null)
        {
            _logger = logger;
            _db = db;
            _scopeFactory = scopeFactory;

            GoogleCredential credential;

            if (!string.IsNullOrEmpty(firebaseJson))
            {
                credential = GoogleCredential.FromJson(firebaseJson);
                _logger.LogInformation("🔥 Firebase cargado desde JSON (PROD)");
            }
            else if (File.Exists(localFilePath))
            {
                credential = GoogleCredential.FromFile(localFilePath);
                _logger.LogInformation("💻 Firebase cargado desde archivo local");
            }
            else
            {
                _logger.LogError("❌ No se encontró ni JSON ni archivo físico de Firebase");
                throw new InvalidOperationException("Credenciales de Firebase no disponibles.");
            }

            _storageClient = new GoogleFirebaseObjectClient(StorageClient.Create(credential));

            _bucketName = config["Firebase:StorageBucket"]
                ?? throw new InvalidOperationException("Falta Firebase:StorageBucket");
        }

        public FirebaseStorageService(IFirebaseObjectClient storageClient, string bucketName,
            GustosDbContext db, IServiceScopeFactory scopeFactory, ILogger<FirebaseStorageService> logger)
        {
            _storageClient = storageClient;
            _bucketName = bucketName;
            _db = db;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }


        


        public async Task<string> UploadFileAsync(Stream stream, string fileName, string? folder = null)
        {
            if (folder is null || !ScopedFolder.IsMatch(folder))
                throw new ArgumentException("La carpeta de destino no pertenece a un recurso válido.", nameof(folder));
            var (normalized, mimeType) = await ValidadorContenidoImagen.NormalizarAsync(stream, fileName);
            using (normalized)
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                // Nombre único (GUID + timestamp)
                var uniqueName = $"{Guid.NewGuid():N}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
                var objectName = $"{folder}/{uniqueName}";

                await _storageClient.UploadAsync(_bucketName, objectName, mimeType, normalized);

                return $"https://firebasestorage.googleapis.com/v0/b/{_bucketName}/o/{Uri.EscapeDataString(objectName)}?alt=media";
            }
        }

        public Task DeleteFileAsync(string url) =>
            throw new InvalidOperationException("La eliminación requiere identificar el recurso propietario.");

        public async Task DeleteFileAsync(string url, string expectedFolder)
        {
            await TryDeleteOwnedAsync(url, expectedFolder, enqueueOnError: true);
        }

        public async Task ScheduleCleanupAsync(string url, string expectedFolder)
        {
            if (!TryExtractOwnedObjectName(url, _bucketName, expectedFolder, out _))
            {
                _logger.LogWarning("Se rechazó programar limpieza de archivo externo o fuera del recurso {Folder}.", expectedFolder);
                return;
            }
            await EnqueueAsync(url, expectedFolder, "PersistenciaAmbigua");
        }

        private async Task<bool> TryDeleteOwnedAsync(string url, string expectedFolder, bool enqueueOnError)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            if (!TryExtractOwnedObjectName(url, _bucketName, expectedFolder, out var objectName))
            {
                _logger.LogWarning("Se conservó un archivo externo, heredado o fuera del recurso {Folder}.", expectedFolder);
                return false;
            }
            // La procedencia no basta: una URL de solicitud puede seguir publicada
            // en el menú de un restaurante aprobado. Si la consulta falla, no borrar.
            try
            {
                if (_db is null || await ArchivoEstaReferenciadoAsync(_db, url))
                {
                    _logger.LogInformation("Se conservó un archivo aún referenciado o sin verificación de referencias.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo verificar la ausencia de referencias del recurso {Folder}; no se borrará.", expectedFolder);
                if (enqueueOnError) await EnqueueAsync(url, expectedFolder, ex.GetType().Name);
                return false;
            }

            try
            {
                await _storageClient.DeleteAsync(_bucketName, objectName);
                return true;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound || ex.Error?.Code == 404)
            {
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falló el borrado de un archivo del recurso {Folder}; se reintentará.", expectedFolder);
                if (enqueueOnError) await EnqueueAsync(url, expectedFolder, ex.GetType().Name);
                return false;
            }
        }

        private async Task EnqueueAsync(string url, string folder, string error)
        {
            if (_scopeFactory is null) return;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var queueDb = scope.ServiceProvider.GetRequiredService<GustosDbContext>();
                if (await queueDb.EliminacionesArchivoPendientes.AnyAsync(x => x.Url == url && x.CarpetaEsperada == folder))
                    return;
                queueDb.EliminacionesArchivoPendientes.Add(new() { Url = url, CarpetaEsperada = folder, UltimoError = error });
                await queueDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el borrado pendiente del recurso {Folder}.", folder);
            }
        }

        public async Task ProcesarPendientesAsync(CancellationToken ct)
        {
            if (_db is null) return;
            var pendientes = await _db.EliminacionesArchivoPendientes
                .Where(x => x.ProximoIntentoUtc <= DateTime.UtcNow)
                .OrderBy(x => x.ProximoIntentoUtc).Take(50).ToListAsync(ct);
            foreach (var pendiente in pendientes)
            {
                try
                {
                    if (await TryDeleteOwnedAsync(pendiente.Url, pendiente.CarpetaEsperada, enqueueOnError: false))
                        _db.EliminacionesArchivoPendientes.Remove(pendiente);
                    else
                    {
                        pendiente.Intentos++;
                        pendiente.ProximoIntentoUtc = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(pendiente.Intentos, 6))));
                    }
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Se pospuso un borrado pendiente; se conservará el archivo.");
                    _db.ChangeTracker.Clear();
                    break;
                }
            }
        }

        public static bool TryExtractOwnedObjectName(string url, string bucket, string expectedFolder, out string objectName)
        {
            objectName = string.Empty;
            if (!ScopedFolder.IsMatch(expectedFolder) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, "firebasestorage.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
                uri.Query != "?alt=media")
                return false;

            var prefix = $"/v0/b/{Uri.EscapeDataString(bucket)}/o/";
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var encoded = uri.AbsolutePath[prefix.Length..];
            var decoded = Uri.UnescapeDataString(encoded);
            if (!string.Equals(Uri.EscapeDataString(decoded), encoded, StringComparison.Ordinal)) return false;
            if (!decoded.StartsWith(expectedFolder + "/", StringComparison.Ordinal)) return false;
            if (!GeneratedName.IsMatch(decoded[(expectedFolder.Length + 1)..])) return false;
            objectName = decoded;
            return true;
        }

        public static async Task<bool> ArchivoEstaReferenciadoAsync(GustosDbContext db, string url) =>
            await db.Usuarios.AsNoTracking().AnyAsync(x => x.FotoPerfilUrl == url) ||
            await db.Restaurantes.AsNoTracking().AnyAsync(x => x.ImagenUrl == url || x.LogoUrl == url) ||
            await db.RestauranteImagenes.AsNoTracking().AnyAsync(x => x.Url == url) ||
            await db.RestauranteMenus.AsNoTracking().AnyAsync(x => x.BorradorImagenFuenteUrl == url || x.ImagenFuentePublicadaUrl == url) ||
            await db.SolicitudRestauranteImagenes.AsNoTracking().AnyAsync(x => x.Url == url) ||
            await db.OpinionesFotos.AsNoTracking().AnyAsync(x => x.Url == url);

    }
}
