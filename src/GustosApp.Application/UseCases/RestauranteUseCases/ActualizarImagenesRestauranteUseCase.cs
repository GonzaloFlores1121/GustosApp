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

namespace GustosApp.Application.UseCases.RestauranteUseCases
{
    public class ActualizarImagenesRestauranteUseCase : IActualizarImagenesRestauranteUseCase
    {
        private readonly IRestauranteRepository _restauranteRepository;
        private readonly IFileStorageService _firebase;

        public ActualizarImagenesRestauranteUseCase(IRestauranteRepository restauranteRepository, IFileStorageService firebase)
        {
            _restauranteRepository = restauranteRepository;
            _firebase = firebase;
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

            try
            {
                string? urlNueva = null;
                if (!soloBorrar && archivo != null)
                {
                    using var stream = archivo.Stream;
                    urlNueva = await _firebase.UploadFileAsync(stream, archivo.FileName, "restaurantes");
                    urlsSubidas.Add(urlNueva);
                }

                restaurante.ImagenUrl = urlNueva;
                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);

                if (!string.IsNullOrWhiteSpace(urlAnterior) && urlAnterior != urlNueva)
                {
                    try { await _firebase.DeleteFileAsync(urlAnterior); } catch { }
                }

                return restaurante.ImagenUrl;
            }
            catch (Exception)
            {
                foreach (var url in urlsSubidas)
                {
                    try { await _firebase.DeleteFileAsync(url); } catch { }
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

            try
            {
                string? urlNueva = null;
                if (!soloBorrar && archivo != null)
                {
                    using var stream = archivo.Stream;
                    urlNueva = await _firebase.UploadFileAsync(stream, archivo.FileName, "restaurantes");
                    urlsSubidas.Add(urlNueva);
                }

                restaurante.LogoUrl = urlNueva;
                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);

                if (!string.IsNullOrWhiteSpace(urlAnterior) && urlAnterior != urlNueva)
                {
                    try { await _firebase.DeleteFileAsync(urlAnterior); } catch { }
                }

                return restaurante.LogoUrl;
            }
            catch (Exception)
            {
                foreach (var url in urlsSubidas)
                {
                    try { await _firebase.DeleteFileAsync(url); } catch { }
                }
                throw;
            }
        }

        public async Task<List<string>> ActualizarImagenesColeccionAsync(Guid id, Guid usuarioId, TipoImagenRestaurante tipo, IList<ArchivoEntrada>? archivos, bool soloBorrar, CancellationToken ct)
        {
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
                        var url = await _firebase.UploadFileAsync(stream, archivo.FileName, "restaurantes");
                        urlsSubidas.Add(url);

                        var entidad = new RestauranteImagen
                        {
                            RestauranteId = id,
                            Tipo = tipo,
                            Url = url,
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
                    try { await _firebase.DeleteFileAsync(imagen.Url); } catch { }
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
                    try { await _firebase.DeleteFileAsync(url); } catch { }
                }
                throw;
            }
        }

        private static void ValidarPropietario(Restaurante restaurante, Guid usuarioId)
        {
            if (restaurante.DuenoId != usuarioId)
                throw new AccesoProhibidoException("No tenés permisos para actualizar las imágenes de este restaurante.");
        }
    }
}
