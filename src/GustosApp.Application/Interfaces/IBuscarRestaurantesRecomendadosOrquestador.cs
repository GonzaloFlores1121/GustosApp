using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Interfaces
{
    public interface IBuscarRestaurantesRecomendadosOrquestador
    {
        Task<List<Restaurante>> HandleAsync(
        string firebaseUid,
        List<string>? gustos,
        string? amigoUsername,
        double? lat,
        double? lng,
        int? radius,
        int top,
        double rating,
        CancellationToken ct);
    }
}
