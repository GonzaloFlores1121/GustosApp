namespace GustosApp.Application.UseCases.RestauranteUseCases.Importacion;

public enum AccionImportacionRestaurante
{
    Crear,
    Actualizar,
    SinCambios,
    DuplicadoEnLote,
    IgnorarPorAntiguedad,
    DescartarSinOfertaGastronomica,
    RequiereRevision
}

public sealed record ItemImportacionRestaurante(
    string PlaceId,
    string Nombre,
    AccionImportacionRestaurante Accion,
    string? Motivo = null);

public sealed record ResultadoImportacionRestaurantes(
    bool Confirmada,
    int Total,
    int Creados,
    int Actualizados,
    int SinCambios,
    int Omitidos,
    IReadOnlyCollection<ItemImportacionRestaurante> Items);
