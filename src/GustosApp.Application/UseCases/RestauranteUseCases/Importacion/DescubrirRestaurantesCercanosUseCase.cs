using GustosApp.Application.Interfaces;

namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public sealed class DescubrirRestaurantesCercanosUseCase
{
    public const int RadioMaximoMetros = 50_000;
    public const int CantidadMaximaResultados = 20;

    private readonly IBuscadorRestaurantesExternos _buscador;
    private readonly ImportarRestaurantesUseCase _importador;

    public DescubrirRestaurantesCercanosUseCase(
        IBuscadorRestaurantesExternos buscador,
        ImportarRestaurantesUseCase importador)
    {
        _buscador = buscador;
        _importador = importador;
    }

    public async Task<ResultadoDescubrimientoRestaurantes> HandleAsync(
        SolicitudDescubrimientoRestaurantes solicitud,
        CancellationToken ct = default)
    {
        Validar(solicitud);

        var restaurantes = await _buscador.BuscarCercanosAsync(solicitud, ct);
        var vistaPrevia = await _importador.HandleAsync(restaurantes, confirmar: false, ct);
        return new ResultadoDescubrimientoRestaurantes(restaurantes, vistaPrevia);
    }

    private static void Validar(SolicitudDescubrimientoRestaurantes solicitud)
    {
        if (solicitud.Latitud is < -90 or > 90)
            throw new ArgumentException("La latitud debe estar entre -90 y 90.");
        if (solicitud.Longitud is < -180 or > 180)
            throw new ArgumentException("La longitud debe estar entre -180 y 180.");
        if (solicitud.RadioMetros is < 1 or > RadioMaximoMetros)
            throw new ArgumentException($"El radio debe estar entre 1 y {RadioMaximoMetros} metros.");
        if (solicitud.CantidadMaxima is < 1 or > CantidadMaximaResultados)
            throw new ArgumentException($"La cantidad máxima debe estar entre 1 y {CantidadMaximaResultados}.");
    }
}
