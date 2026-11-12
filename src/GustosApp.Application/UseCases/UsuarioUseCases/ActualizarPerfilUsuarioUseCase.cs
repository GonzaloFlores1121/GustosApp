using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Common;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging;

namespace GustosApp.Application.UseCases.UsuarioUseCases
{
    public class ActualizarPerfilUsuarioUseCase
    {
        private readonly IUsuarioRepository _usuarios;
        private readonly IFileStorageService _files;
        private readonly ILogger<ActualizarPerfilUsuarioUseCase>? _logger;

        public ActualizarPerfilUsuarioUseCase(
            IUsuarioRepository usuarios,
            IFileStorageService files,
            ILogger<ActualizarPerfilUsuarioUseCase>? logger = null)
        {
            _usuarios = usuarios;
            _files = files;
            _logger = logger;
        }

        public async Task<Usuario> HandleAsync(
            string firebaseUid, string? nombre, string? apellido,
            FileUpload? fotoPerfil, bool? esPrivado,
            CancellationToken ct = default)
        {
            var usuario = await _usuarios.GetByFirebaseUidAsync(firebaseUid, ct);
            if (usuario == null)
                throw new Exception("Usuario no encontrado");

            if (!string.IsNullOrWhiteSpace(nombre))
                usuario.Nombre = nombre;

            if (!string.IsNullOrWhiteSpace(apellido))
                usuario.Apellido = apellido;

            if (esPrivado.HasValue)
                usuario.EsPrivado = esPrivado.Value;

            var fotoAnterior = usuario.FotoPerfilUrl;
            var carpeta = $"perfil/{usuario.Id:N}";
            string? fotoNueva = null;
            try
            {
                if (fotoPerfil != null)
                {
                    fotoNueva = await _files.UploadFileAsync(
                        fotoPerfil.Content,
                        fotoPerfil.FileName,
                        carpeta
                    );

                    usuario.FotoPerfilUrl = fotoNueva;
                }

                await _usuarios.SaveChangesAsync(ct);
            }
            catch
            {
                usuario.FotoPerfilUrl = fotoAnterior;
                if (!string.IsNullOrWhiteSpace(fotoNueva))
                {
                    // El almacenamiento sólo elimina si la URL no quedó referenciada,
                    // incluso cuando el commit fue exitoso pero la respuesta falló.
                    try { await _files.ScheduleCleanupAsync(fotoNueva, carpeta); }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "No se pudo programar la limpieza de la foto nueva de {UsuarioId}.", usuario.Id);
                    }
                }
                throw;
            }

            if (!string.IsNullOrWhiteSpace(fotoNueva) &&
                !string.IsNullOrWhiteSpace(fotoAnterior) &&
                !string.Equals(fotoNueva, fotoAnterior, StringComparison.Ordinal))
            {
                try { await _files.DeleteFileAsync(fotoAnterior, carpeta); }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Falló la eliminación de la foto anterior del usuario {UsuarioId}.", usuario.Id);
                }
            }

            return usuario;
        }
    }
}
