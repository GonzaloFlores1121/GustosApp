using GustosApp.Domain.Model;

namespace GustosApp.Domain.Common;

public enum FiltroPendientesClasificacion
{
    Todos,
    ErrorMenu,
    SinMenuProcesado,
    SinGustos,
    ClasificacionAutomatica
}

public sealed record ConsultaPendientesClasificacion
{
    public int Pagina { get; }
    public int TamanoPagina { get; }
    public FiltroPendientesClasificacion Filtro { get; }
    public int Desplazamiento => (Pagina - 1) * TamanoPagina;

    public ConsultaPendientesClasificacion(int pagina = 1, int tamanoPagina = 20,
        FiltroPendientesClasificacion filtro = FiltroPendientesClasificacion.Todos)
    {
        if (pagina < 1 || (long)(pagina - 1) * tamanoPagina > int.MaxValue)
            throw new ArgumentException("La página debe ser positiva y su desplazamiento válido.");
        if (tamanoPagina is < 1 or > 100)
            throw new ArgumentException("El tamaño de página debe estar entre 1 y 100.");
        if (!Enum.IsDefined(filtro))
            throw new ArgumentException("El filtro de pendientes no es válido.");
        Pagina = pagina;
        TamanoPagina = tamanoPagina;
        Filtro = filtro;
    }
}

public sealed record PaginaRestaurantesPendientes(
    IReadOnlyCollection<Restaurante> Elementos, int Total, int Pagina, int TamanoPagina);
