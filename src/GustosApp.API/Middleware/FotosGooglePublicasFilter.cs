using System.Text.Json;
using System.Text.Json.Nodes;
using GustosApp.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace GustosApp.API.Middleware;

/// <summary>Última frontera JSON para referencias históricas: nunca publica URLs Google con claves.</summary>
public sealed class FotosGooglePublicasFilter(IOptions<JsonOptions> options) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: not null } resultado) return;
        var id = Guid.TryParse(context.RouteData.Values.GetValueOrDefault("id")?.ToString(), out var routeId) ? routeId : Guid.Empty;
        var node = JsonSerializer.SerializeToNode(resultado.Value, options.Value.JsonSerializerOptions);
        resultado.Value = Normalizar(node, id);
        resultado.DeclaredType = typeof(JsonNode);
    }
    public void OnResultExecuted(ResultExecutedContext context) { }

    public static JsonNode? Normalizar(JsonNode? node, Guid restauranteId)
    {
        if (node is JsonObject obj)
        {
            string? Leer(string name) => obj.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value?.ToString();
            if (Guid.TryParse(Leer("restauranteId"), out var explicitId)) restauranteId = explicitId;
            else if (Leer("nombre") != null && (Leer("imagenUrl") != null || Leer("imagenDestacada") != null || Leer("direccion") != null)
                && Guid.TryParse(Leer("id"), out var propioId)) restauranteId = propioId;
            foreach (var key in obj.Select(p => p.Key).ToList()) obj[key] = Normalizar(obj[key]?.DeepClone(), restauranteId);
        }
        else if (node is JsonArray array)
            for (var i = 0; i < array.Count; i++) array[i] = Normalizar(array[i]?.DeepClone(), restauranteId);
        else if (node is JsonValue value && value.TryGetValue<string>(out var text) && ImagenRestaurantePublica.EsGoogle(text))
            return JsonValue.Create(ImagenRestaurantePublica.Url(restauranteId, text));
        return node;
    }
}
