using GustosApp.Application.Common.Exceptions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases;

public sealed class ConfirmarVigenciaRestauranteUseCase(
    IRestauranteRepository restaurantes, TimeProvider reloj)
{
    public async Task<Restaurante> ConfirmarOfertaAsync(
        Guid restauranteId, int versionEsperada, Guid? propietarioId,
        bool administradorDeImportados, CancellationToken ct = default)
    {
        var restaurante = await ObtenerAutorizadoAsync(restauranteId, propietarioId,
            administradorDeImportados, ct);
        if (restaurante.GustosQueSirve.Count == 0 && restaurante.MenuProcesado != true)
            throw new InvalidOperationException("Primero cargá gustos o un menú publicado para confirmar la oferta.");
        restaurante.ConfirmarOfertaVigente(versionEsperada, reloj.GetUtcNow().UtcDateTime);
        await restaurantes.UpdateAsync(restaurante, ct);
        await restaurantes.SaveChangesAsync(ct);
        return restaurante;
    }

    public async Task<Restaurante> ConfirmarRestriccionesAsync(
        Guid restauranteId, int versionEsperada, Guid? propietarioId,
        bool administradorDeImportados, CancellationToken ct = default)
    {
        var restaurante = await ObtenerAutorizadoAsync(restauranteId, propietarioId,
            administradorDeImportados, ct);
        restaurante.ConfirmarRestriccionesDeclaradas(versionEsperada, reloj.GetUtcNow().UtcDateTime);
        await restaurantes.UpdateAsync(restaurante, ct);
        await restaurantes.SaveChangesAsync(ct);
        return restaurante;
    }

    private async Task<Restaurante> ObtenerAutorizadoAsync(Guid restauranteId,
        Guid? propietarioId, bool administradorDeImportados, CancellationToken ct)
    {
        var restaurante = await restaurantes.GetByIdAsync(restauranteId, ct)
            ?? throw new KeyNotFoundException("Restaurante no encontrado.");
        if (administradorDeImportados)
        {
            if (restaurante.DuenoId.HasValue || !string.IsNullOrWhiteSpace(restaurante.PropietarioUid))
                throw new AccesoProhibidoException("La administración de importados no puede confirmar un restaurante con propietario.");
        }
        else if (!propietarioId.HasValue || restaurante.DuenoId != propietarioId)
        {
            throw new AccesoProhibidoException("No tenés permisos para confirmar este restaurante.");
        }
        return restaurante;
    }
}
