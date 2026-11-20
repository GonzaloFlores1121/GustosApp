using GustosApp.Application.Interfaces;
using GustosApp.Application.Model;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.GrupoUseCases;

public sealed class BuscarRestaurantesRecomendadosGrupoUseCase : IBuscadorRestaurantesGrupo
{
    private readonly IConstruirPreferenciasGrupoUseCase _construirPreferencias;
    private readonly IServicioRestaurantes _servicioRestaurantes;
    private readonly IRecomendadorRestaurantes _recomendador;

    public BuscarRestaurantesRecomendadosGrupoUseCase(
        IConstruirPreferenciasGrupoUseCase construirPreferencias,
        IServicioRestaurantes servicioRestaurantes,
        IRecomendadorRestaurantes recomendador)
    {
        _construirPreferencias = construirPreferencias;
        _servicioRestaurantes = servicioRestaurantes;
        _recomendador = recomendador;
    }

    public async Task<ResultadoBusquedaRestaurantesGrupo> BuscarAsync(
        string firebaseUid,
        Guid grupoId,
        double? latitud,
        double? longitud,
        int? radioMetros,
        int cantidadMaxima,
        CancellationToken cancellationToken = default)
    {
        var preferencias = await _construirPreferencias.HandleAsync(
            firebaseUid,
            grupoId,
            cancellationToken);

        if (preferencias.Gustos.Count == 0)
        {
            return new ResultadoBusquedaRestaurantesGrupo(
                EstadoBusquedaRestaurantesGrupo.SinPreferencias,
                Array.Empty<Restaurante>());
        }

        var candidatos = await _servicioRestaurantes.BuscarAsync(
            0,
            latitud,
            longitud,
            radioMetros,
            preferencias.Gustos,
            preferencias.Restricciones);

        candidatos = candidatos.Where(r => !r.Desactivado).ToList();
        if (candidatos.Count == 0)
        {
            return new ResultadoBusquedaRestaurantesGrupo(
                EstadoBusquedaRestaurantesGrupo.SinOpciones,
                Array.Empty<Restaurante>());
        }

        try
        {
            var recomendados = await _recomendador.Handle(
                preferencias,
                candidatos,
                Math.Max(1, cantidadMaxima),
                cancellationToken);

            return new ResultadoBusquedaRestaurantesGrupo(
                recomendados.Count == 0
                    ? EstadoBusquedaRestaurantesGrupo.SinOpciones
                    : EstadoBusquedaRestaurantesGrupo.Encontrados,
                recomendados);
        }
        catch (KeyNotFoundException)
        {
            return new ResultadoBusquedaRestaurantesGrupo(
                EstadoBusquedaRestaurantesGrupo.SinOpciones,
                Array.Empty<Restaurante>());
        }
    }
}
