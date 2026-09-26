using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.UseCases.RestauranteUseCases.OpinionesRestaurantes
{
    public class CrearOpinionRestauranteUseCase
    {
        private readonly IFileStorageService _fileStorage;
        private readonly IOpinionRestauranteRepository _repoOpinionRest;
        private readonly IUsuarioRepository _repoUsuario;
        private readonly IRestauranteRepository _repoRestaurante;
        private readonly IApplicationTransaction _transaction;

        public CrearOpinionRestauranteUseCase(IFileStorageService fileStorage,
            IOpinionRestauranteRepository repoOpinionRest,
            IUsuarioRepository repoUsuario,
            IRestauranteRepository repoRestaurante,
            IApplicationTransaction? transaction = null)
        {
            _fileStorage = fileStorage;
            _repoOpinionRest = repoOpinionRest;
            _repoUsuario = repoUsuario;
            _repoRestaurante = repoRestaurante;
            _transaction = transaction ?? ImmediateApplicationTransaction.Instance;
        }

        public async Task HandleAsync(string firebaseUid, Guid restauranteId,
     double valoracionUsuario, string opinion, string titulo,
     List<FileUpload>? Imagenes, string? motivoVisita,
     DateTime fechaVisita, CancellationToken cancellationToken)
        {
            var usuario = await _repoUsuario.GetByFirebaseUidAsync(firebaseUid, cancellationToken)
                ?? throw new UnauthorizedAccessException("Usuario no encontrado");

            if (await _repoRestaurante.GetByIdAsync(restauranteId, cancellationToken) is null)
                throw new KeyNotFoundException("Restaurante no encontrado.");

            if (valoracionUsuario < 1 || valoracionUsuario > 5)
                throw new ArgumentException("La valoracion debe estar entre 1 y 5");

            var existe = await _repoOpinionRest.ExisteValoracionAsync(usuario.Id, restauranteId, cancellationToken);
            if (existe)
                throw new ArgumentException("El usuario ya valoro a este restaurante");

       
            if (Imagenes == null || !Imagenes.Any())
                throw new ArgumentException("Debes subir al menos una imagen para crear una opinión.");

            var opinionRestaurante = new OpinionRestaurante
            {
                Id = Guid.NewGuid(),
                RestauranteId = restauranteId,
                UsuarioId = usuario.Id,
                Valoracion = valoracionUsuario,
                Opinion = opinion,
                Titulo = titulo,
                FechaVisita = fechaVisita,
                MotivoVisita = motivoVisita,
                FechaCreacion = DateTime.UtcNow,
                EsImportada = false
            };

       
            var urlsSubidas = new List<string>();
            try
            {
                foreach (var file in Imagenes)
                {
                    var url = await _fileStorage.UploadFileAsync(
                        file.Content,
                        file.FileName,
                        "opiniones"
                    );

                    urlsSubidas.Add(url);
                    opinionRestaurante.Fotos.Add(new OpinionFoto { Id = Guid.NewGuid(), Url = url });
                }

                await _transaction.ExecuteAsync(async transactionCt =>
                {
                    await _repoOpinionRest.CrearAsync(opinionRestaurante, transactionCt);
                    await _repoUsuario.MarcarRestauranteComoVisitadoAsync(
                        usuario.Id,
                        restauranteId,
                        transactionCt);
                    return true;
                }, cancellationToken);
            }
            catch
            {
                foreach (var url in urlsSubidas)
                {
                    try { await _fileStorage.DeleteFileAsync(url); } catch { }
                }
                throw;
            }
        }

    }
    }


