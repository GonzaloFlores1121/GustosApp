using GustosApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Application.record
{
    public sealed record ActividadResenas(DateOnly Fecha, int Cantidad);
    public sealed record ResumenResenasLocales(int Total, double? Promedio, int[] Distribucion, ActividadResenas[] Actividad);

    public record RestauranteMetricasRecord(
        Guid RestauranteId,
        RestauranteEstadisticas Estadisticas,
        List<UsuarioRestauranteFavorito> TotalFavoritos,
        ResumenResenasLocales? ResenasLocales = null
    );
}
