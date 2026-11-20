using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GustosApp.Application.Common.Exceptions;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Domain.Common;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class ActualizarImagenesRestauranteUseCase : IActualizarImagenesRestauranteUseCase
    {
        private readonly IRestauranteRepository _restauranteRepository;
        private readonly IFileStorageService _firebase;
        private readonly ILogger<ActualizarImagenesRestauranteUseCase>? _logger;
        private readonly IApplicationTransaction _transaccion;

        public ActualizarImagenesRestauranteUseCase(IRestauranteRepository restauranteRepository, IFileStorageService firebase,
            ILogger<ActualizarImagenesRestauranteUseCase>? logger = null, IApplicationTransaction? transaccion = null)
        {
            _restauranteRepository = restauranteRepository;
            _firebase = firebase;
            _logger = logger;
            _transaccion = transaccion ?? ImmediateApplicationTransaction.Instance;
        }

        public async Task<string?> ActualizarImagenDestacadaAsync(Guid id, Guid usuarioId, ArchivoEntrada? archivo, bool soloBorrar, CancellationToken ct)
        {
            var restaurante = await _restauranteRepository.GetRestauranteConImagenesAsync(id, ct)
                ?? throw new NotFoundException("Restaurante no encontrado.");
            ValidarPropietario(restaurante, usuarioId);

            if (!soloBorrar && archivo is null)
                return restaurante.ImagenUrl;

            var urlsSubidas = new List<string>();
            var urlAnterior = restaurante.ImagenUrl;
            var origenAnterior = restaurante.OrigenImagen;

            try
            {
                string? urlNueva = null;
                if (!soloBorrar && archivo != null)
                {
                    using var stream = archivo.Stream;
                    urlNueva = await _firebase.UploadFileAsync(stream, archivo.FileName, $"restaurantes/{id:N}");
                    urlsSubidas.Add(urlNueva);
                }

                restaurante.ImagenUrl = urlNueva;
                restaurante.OrigenImagen = OrigenImagenRestaurante.Propietario;
                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);

                if (!string.IsNullOrWhiteSpace(urlAnterior) && urlAnterior != urlNueva)
                {
                    await LimpiarAnteriorAsync(id, urlAnterior);
                }

                return restaurante.ImagenUrl;
            }
            catch (Exception)
            {
                restaurante.ImagenUrl = urlAnterior;
                restaurante.OrigenImagen = origenAnterior;
                foreach (var url in urlsSubidas)
                {
                    try { await _firebase.ScheduleCleanupAsync(url, $"restaurantes/{id:N}"); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Falló limpieza de imagen nueva de {RestauranteId}.", id); }
                }
                throw;
            }
        }

        public async Task<string?> ActualizarLogoAsync(Guid id, Guid usuarioId, ArchivoEntrada? archivo, bool soloBorrar, CancellationToken ct)
        {
            var restaurante = await _restauranteRepository.GetRestauranteConImagenesAsync(id, ct)
                ?? throw new NotFoundException("Restaurante no encontrado.");
            ValidarPropietario(restaurante, usuarioId);

            if (!soloBorrar && archivo is null)
                return restaurante.LogoUrl;

            var urlsSubidas = new List<string>();
            var urlAnterior = restaurante.LogoUrl;
            var origenAnterior = restaurante.OrigenLogo;

            try
            {
                string? urlNueva = null;
                if (!soloBorrar && archivo != null)
                {
                    using var stream = archivo.Stream;
                    urlNueva = await _firebase.UploadFileAsync(stream, archivo.FileName, $"restaurantes/{id:N}");
                    urlsSubidas.Add(urlNueva);
                }

                restaurante.LogoUrl = urlNueva;
                restaurante.OrigenLogo = OrigenImagenRestaurante.Propietario;
                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);

                if (!string.IsNullOrWhiteSpace(urlAnterior) && urlAnterior != urlNueva)
                {
                    await LimpiarAnteriorAsync(id, urlAnterior);
                }

                return restaurante.LogoUrl;
            }
            catch (Exception)
            {
                restaurante.LogoUrl = urlAnterior;
                restaurante.OrigenLogo = origenAnterior;
                foreach (var url in urlsSubidas)
                {
                    try { await _firebase.ScheduleCleanupAsync(url, $"restaurantes/{id:N}"); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Falló limpieza de logo nuevo de {RestauranteId}.", id); }
                }
                throw;
            }
        }

        public async Task<List<string>> ActualizarImagenesColeccionAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo, IList<ArchivoEntrada>? archivos, bool soloBorrar, CancellationToken ct)
        {
            if (tipo == TipoImagenRestaurante.Interior || tipo == TipoImagenRestaurante.Comida)
            {
                if (!soloBorrar && archivos?.Count > 0)
                    return (await AgregarImagenesAsync(id, usuarioId, tipo, archivos, ct)).Select(i => i.Url).ToList();
                var actual = await _restauranteRepository.GetRestauranteConImagenesAsync(id, ct)
                    ?? throw new NotFoundException("Restaurante no encontrado.");
                ValidarPropietario(actual, usuarioId);
                if (soloBorrar) throw new ArgumentException("Elegí la foto que querés eliminar por su identificador.");
                return actual.Imagenes.Where(i => i.Tipo == tipo).OrderBy(i => i.Orden).Select(i => i.Url).ToList();
            }

            var restaurante = await _restauranteRepository.GetRestauranteConImagenesAsync(id, ct)
                ?? throw new NotFoundException("Restaurante no encontrado.");
            ValidarPropietario(restaurante, usuarioId);

            if (!soloBorrar && (archivos is null || archivos.Count == 0))
            {
                return restaurante.Imagenes
                    .Where(i => i.Tipo == tipo)
                    .OrderBy(i => i.Orden)
                    .Select(i => i.Url)
                    .ToList();
            }

            var urlsSubidas = new List<string>();

            try
            {
                var imagenesExistentes = restaurante.Imagenes.Where(i => i.Tipo == tipo).ToList();
                var imagenesNuevas = new List<RestauranteImagen>();
                if (!soloBorrar && archivos != null && archivos.Count > 0)
                {
                    var orden = 0;
                    foreach (var archivo in archivos)
                    {
                        using var stream = archivo.Stream;
                        var url = await _firebase.UploadFileAsync(stream, archivo.FileName, $"restaurantes/{id:N}");
                        urlsSubidas.Add(url);

                        var entidad = new RestauranteImagen
                        {
                            // La imagen se agrega a una ficha ya seguida por EF. Mantener la clave
                            // temporal permite que el proveedor la reconozca como una fila nueva.
                            Id = Guid.Empty,
                            RestauranteId = id,
                            Tipo = tipo,
                            Url = url,
                            Origen = OrigenImagenRestaurante.Propietario,
                            Orden = orden++,
                            FechaCreacionUtc = DateTime.UtcNow
                        };
                        imagenesNuevas.Add(entidad);
                    }
                }

                foreach (var imagen in imagenesExistentes)
                    restaurante.Imagenes.Remove(imagen);
                foreach (var imagen in imagenesNuevas)
                    restaurante.Imagenes.Add(imagen);

                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);

                foreach (var imagen in imagenesExistentes)
                {
                    await LimpiarAnteriorAsync(id, imagen.Url);
                }

                var urls = restaurante.Imagenes
                    .Where(i => i.Tipo == tipo)
                    .OrderBy(i => i.Orden)
                    .Select(i => i.Url)
                    .ToList();

                return urls;
            }
            catch (Exception)
            {
                foreach (var url in urlsSubidas)
                {
                    try { await _firebase.ScheduleCleanupAsync(url, $"restaurantes/{id:N}"); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Falló limpieza de galería nueva de {RestauranteId}.", id); }
                }
                throw;
            }
        }

        // Los contratos nuevos operan con IDs del servidor. La URL nunca autoriza una eliminación.
        public Task<List<RestauranteImagen>> AgregarImagenesAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo,
            IList<ArchivoEntrada> archivos, CancellationToken ct) => MutarColeccionAsync(id, usuarioId, tipo, null, archivos, ct);

        public Task<List<RestauranteImagen>> ModificarImagenAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo,
            Guid imagenId, ArchivoEntrada? archivo, CancellationToken ct) =>
            MutarColeccionAsync(id, usuarioId, tipo, imagenId, archivo is null ? [] : [archivo], ct);

        private async Task<List<RestauranteImagen>> MutarColeccionAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo,
            Guid? imagenId, IList<ArchivoEntrada> archivos, CancellationToken ct)
        {
            var subidas = new List<string>();
            var anteriores = new List<string>();
            Restaurante? restaurante = null;
            List<RestauranteImagen>? snapshot = null;
            try
            {
                // Serializa lectura de cupo y escritura, incluso entre distintas instancias de la API.
                var resultado = await _transaccion.ExecuteAsync(async token =>
                {
                    restaurante = await _restauranteRepository.GetRestauranteConImagenesAsync(id, token)
                        ?? throw new NotFoundException("Restaurante no encontrado.");
                    ValidarPropietario(restaurante, usuarioId);
                    if (tipo != TipoImagenRestaurante.Interior && tipo != TipoImagenRestaurante.Comida)
                        throw new ArgumentException("La categoría de fotos no es válida.");
                    snapshot = restaurante.Imagenes.ToList();
                    var coleccion = snapshot.Where(i => i.Tipo == tipo).ToList();
                    var anterior = imagenId.HasValue
                        ? coleccion.SingleOrDefault(i => i.Id == imagenId.Value)
                            ?? throw new NotFoundException("Esta foto ya no está disponible. Actualizá la galería.")
                        : null;
                    if (archivos.Count > 5 || (anterior is null && (archivos.Count == 0 || coleccion.Count + archivos.Count > 5)))
                        throw new ArgumentException("Podés guardar hasta 5 fotos por categoría, contando las existentes.");
                    if (imagenId.HasValue && archivos.Count > 1)
                        throw new ArgumentException("Elegí una sola foto para reemplazar.");
                    var nuevas = new List<RestauranteImagen>();
                    var orden = coleccion.Select(i => i.Orden ?? 0).DefaultIfEmpty(-1).Max() + 1;
                    foreach (var archivo in archivos)
                    {
                        var url = await _firebase.UploadFileAsync(archivo.Stream, archivo.FileName, $"restaurantes/{id:N}");
                        subidas.Add(url);
                        nuevas.Add(new RestauranteImagen { Id = Guid.Empty, RestauranteId = id, Tipo = tipo,
                            Url = url, Origen = OrigenImagenRestaurante.Propietario, Orden = anterior?.Orden ?? orden++ });
                    }
                    if (anterior != null)
                    {
                        restaurante.Imagenes.Remove(anterior);
                        anteriores.Add(anterior.Url);
                    }
                    foreach (var nueva in nuevas) restaurante.Imagenes.Add(nueva);
                    restaurante.ActualizadoUtc = DateTime.UtcNow;
                    await _restauranteRepository.SaveChangesAsync(token);
                    return restaurante.Imagenes.Where(i => i.Tipo == tipo).OrderBy(i => i.Orden).ThenBy(i => i.Id).ToList();
                }, ct);
                // La persistencia ya terminó: un fallo de limpieza no convierte en fallido el guardado.
                foreach (var url in anteriores) await LimpiarAnteriorAsync(id, url);
                return resultado;
            }
            catch
            {
                if (restaurante != null && snapshot != null)
                {
                    restaurante.Imagenes.Clear();
                    foreach (var imagen in snapshot) restaurante.Imagenes.Add(imagen);
                }
                foreach (var url in subidas)
                {
                    try { await _firebase.ScheduleCleanupAsync(url, $"restaurantes/{id:N}"); }
                    catch (Exception ex) { _logger?.LogWarning(ex, "Falló programación de limpieza de imagen de {RestauranteId}.", id); }
                }
                throw;
            }
            finally { foreach (var archivo in archivos) archivo.Stream.Dispose(); }
        }

        private async Task LimpiarAnteriorAsync(Guid id, string url)
        {
            try { await _firebase.DeleteFileAsync(url, $"restaurantes/{id:N}"); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Limpieza pendiente tras guardar imagen de {RestauranteId}.", id);
                try { await _firebase.ScheduleCleanupAsync(url, $"restaurantes/{id:N}"); }
                catch (Exception pendiente) { _logger?.LogWarning(pendiente, "No se pudo programar limpieza de {RestauranteId}.", id); }
            }
        }

        private static void ValidarPropietario(Restaurante restaurante, Guid usuarioId)
        {
            if (restaurante.DuenoId != usuarioId)
                throw new AccesoProhibidoException("No tenés permisos para actualizar las imágenes de este restaurante.");
        }
    }
}
