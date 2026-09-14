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

            var urlsSubidas = new List<string>();

            try
            {
                if (!string.IsNullOrWhiteSpace(restaurante.ImagenUrl))
                {
                    try { await _firebase.DeleteFileAsync(restaurante.ImagenUrl); } catch { }
                    restaurante.ImagenUrl = null;
                }

                if (!soloBorrar && archivo != null)
                {
                    using var stream = archivo.Stream;
                    var url = await _firebase.UploadFileAsync(stream, archivo.FileName, "restaurantes");
                    urlsSubidas.Add(url);
                    restaurante.ImagenUrl = url;
                }

                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);
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

            var urlsSubidas = new List<string>();

            try
            {
                if (!string.IsNullOrWhiteSpace(restaurante.LogoUrl))
                {
                    try { await _firebase.DeleteFileAsync(restaurante.LogoUrl); } catch { }
                    restaurante.LogoUrl = null;
                }

                if (!soloBorrar && archivo != null)
                {
                    using var stream = archivo.Stream;
                    var url = await _firebase.UploadFileAsync(stream, archivo.FileName, "restaurantes");
                    urlsSubidas.Add(url);
                    restaurante.LogoUrl = url;
                }

                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);
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

            var urlsSubidas = new List<string>();

            try
            {
                var imagenesExistentes = restaurante.Imagenes.Where(i => i.Tipo == tipo).ToList();

                foreach (var img in imagenesExistentes)
                {
                    try { await _firebase.DeleteFileAsync(img.Url); } catch { }
                    restaurante.Imagenes.Remove(img);
                }

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

                        restaurante.Imagenes.Add(entidad);
                    }
                }

                restaurante.ActualizadoUtc = DateTime.UtcNow;
                await _restauranteRepository.SaveChangesAsync(ct);

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
