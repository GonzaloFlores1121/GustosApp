using System.Text.Json;
using System.Text.RegularExpressions;

namespace GustosApp.Domain.Common;

public enum EstadoAperturaRestaurante { Desconocido, Abierto, Cerrado }

/// <summary>Semana habitual informada, en Argentina. No presupone excepciones por feriados.</summary>
public static class HorariosRestaurante
{
    public const string ZonaHoraria = "America/Argentina/Buenos_Aires";
    private static readonly TimeZoneInfo ZonaArgentina = TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria);
    private static readonly string[] Dias = ["domingo", "lunes", "martes", "miercoles", "jueves", "viernes", "sabado"];

    public static EstadoAperturaRestaurante Evaluar(string? json, DateTimeOffset momento)
    {
        if (string.IsNullOrWhiteSpace(json)) return EstadoAperturaRestaurante.Desconocido;
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Array) return EstadoAperturaRestaurante.Desconocido;
            var dias = new Dictionary<int, List<Turno>?>();
            foreach (var entrada in documento.RootElement.EnumerateArray())
            {
                if (entrada.ValueKind != JsonValueKind.Object || !entrada.TryGetProperty("dia", out var dia)
                    || dia.ValueKind != JsonValueKind.String) return EstadoAperturaRestaurante.Desconocido;
                var indice = Array.IndexOf(Dias, dia.GetString()!.Trim().ToLowerInvariant().Replace("é", "e").Replace("á", "a"));
                if (indice < 0) return EstadoAperturaRestaurante.Desconocido;
                var turnos = LeerDia(entrada);
                if (!dias.TryGetValue(indice, out var anteriores)) dias[indice] = turnos;
                else if (anteriores is null || turnos is null || anteriores.Count == 0 || turnos.Count == 0)
                    dias[indice] = anteriores?.Count == 0 && turnos?.Count == 0 ? [] : null;
                else anteriores.AddRange(turnos);
            }
            var local = TimeZoneInfo.ConvertTime(momento, ZonaArgentina);
            var hoy = (int)local.DayOfWeek;
            var minutos = local.Hour * 60 + local.Minute;
            dias.TryGetValue(hoy, out var actuales);
            dias.TryGetValue((hoy + 6) % 7, out var previos);
            if (actuales?.Any(t => minutos >= t.Desde && (t.Hasta < t.Desde || minutos < t.Hasta)) == true
                || previos?.Any(t => t.Hasta < t.Desde && minutos < t.Hasta) == true)
                return EstadoAperturaRestaurante.Abierto;
            return actuales is not null && previos is not null
                ? EstadoAperturaRestaurante.Cerrado : EstadoAperturaRestaurante.Desconocido;
        }
        catch (JsonException) { return EstadoAperturaRestaurante.Desconocido; }
    }

    private static List<Turno>? LeerDia(JsonElement entrada)
    {
        if (!LeerBandera(entrada, "cerrado", out var cerrado) || !LeerBandera(entrada, "todoElDia", out var todoElDia)
            || (cerrado && todoElDia)) return null;
        if (cerrado) return [];
        if (todoElDia) return [new(0, 1440)];
        var desde = LeerHora(entrada, "desde", false);
        var hasta = LeerHora(entrada, "hasta", true);
        return desde.HasValue && hasta.HasValue && desde != hasta ? [new(desde.Value, hasta.Value)] : null;
    }

    private static bool LeerBandera(JsonElement entrada, string nombre, out bool valor)
    {
        valor = false;
        if (!entrada.TryGetProperty(nombre, out var propiedad)) return true;
        if (propiedad.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        valor = propiedad.GetBoolean();
        return true;
    }

    private static int? LeerHora(JsonElement entrada, string nombre, bool cierre)
    {
        if (!entrada.TryGetProperty(nombre, out var hora) || hora.ValueKind != JsonValueKind.String) return null;
        var texto = hora.GetString()!;
        if (!Regex.IsMatch(texto, @"^\d{1,2}:\d{2}$")) return null;
        var partes = texto.Split(':');
        if (!int.TryParse(partes[0], out var horas) || !int.TryParse(partes[1], out var minutos)) return null;
        if (cierre && horas == 24 && minutos == 0) return 1440;
        return horas < 24 && minutos < 60 ? horas * 60 + minutos : null;
    }

    private sealed record Turno(int Desde, int Hasta);
}
