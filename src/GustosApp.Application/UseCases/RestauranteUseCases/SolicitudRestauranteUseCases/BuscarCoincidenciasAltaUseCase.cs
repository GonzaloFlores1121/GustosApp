using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed record CoincidenciaAlta(Guid Id, string Nombre, string Direccion, int DistanciaMetros,
    bool Administrado, IReadOnlyList<string> Motivos);

public sealed record RevisionCoincidenciasAlta(bool UbicacionDisponible, IReadOnlyList<CoincidenciaAlta> Candidatos)
{
    public string Revision => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { UbicacionDisponible, Candidatos })));
}

public sealed class BuscarCoincidenciasAltaUseCase(IRestauranteRepository restaurantes)
{
    private const double RadioTierraMetros = 6_371_000;
    private const double Radianes = Math.PI / 180;
    private const double RadioDireccion = 140;
    private const double RadioNombre = 45;
    private const double RadioUbicacion = 15;
    private static readonly HashSet<string> TiposCalle = ["avenida", "av", "calle"];
    private static readonly HashSet<string> PalabrasGenericas =
        ["restaurante", "restaurant", "bar", "cafe", "cafeteria", "pizzeria", "parrilla", "cerveceria",
         "panaderia", "heladeria", "sushi", "pizza", "la", "el", "de", "del", "los", "las"];

    public static bool UbicacionValida(double? latitud, double? longitud) =>
        latitud is >= -90 and <= 90 && longitud is >= -180 and <= 180
        && (latitud != 0 || longitud != 0);

    public async Task<RevisionCoincidenciasAlta> HandleAsync(
        string? nombre, string? direccion, double? latitud, double? longitud, CancellationToken ct,
        Guid? excluirId = null)
    {
        var direccionNormalizada = NormalizarDireccion(direccion);
        if (direccionNormalizada.Length == 0 || !UbicacionValida(latitud, longitud))
            return new(false, []);

        var lat = latitud!.Value;
        var lng = longitud!.Value;
        var radioAngular = (RadioDireccion + 1) / RadioTierraMetros;
        var amplitudLatitud = radioAngular / Radianes;
        var amplitudLongitud = Math.Abs(lat) + amplitudLatitud >= 90
            ? 180
            : Math.Asin(Math.Sin(radioAngular) / Math.Cos(lat * Radianes)) / Radianes;
        var candidatos = await restaurantes.BuscarLocalesEnVentanaAsync(lat, lng, amplitudLatitud, amplitudLongitud, ct);
        var palabrasNombre = PalabrasNombre(nombre);
        var direccionNumerada = direccionNormalizada.Split(' ').Skip(1).Any(p => p.Any(char.IsDigit));
        var encontrados = new List<(CoincidenciaAlta Ficha, bool MismaDireccion)>();
        foreach (var local in candidatos)
        {
            if (local.Id == excluirId) continue;
            var distancia = Distancia(lat, lng, local.Latitud, local.Longitud);
            var mismaDireccion = distancia <= RadioDireccion && direccionNumerada
                && NormalizarDireccion(local.Direccion) == direccionNormalizada;
            var mismaUbicacion = distancia <= RadioUbicacion;
            var nombreSimilar = distancia <= RadioNombre && NombresSimilares(palabrasNombre, PalabrasNombre(local.Nombre));
            if (!mismaDireccion && !mismaUbicacion && !nombreSimilar) continue;

            var motivos = new List<string>();
            if (mismaDireccion) motivos.Add("Misma dirección");
            if (mismaUbicacion) motivos.Add("Ubicación muy cercana");
            if (nombreSimilar) motivos.Add("Nombre similar y ubicación cercana");
            encontrados.Add((new(local.Id, local.Nombre, local.Direccion, (int)Math.Round(distancia),
                local.Administrado, motivos), mismaDireccion));
        }

        return new(true, encontrados.OrderByDescending(c => c.MismaDireccion)
            .ThenBy(c => c.Ficha.DistanciaMetros).ThenBy(c => c.Ficha.Id).Take(5).Select(c => c.Ficha).ToList());
    }

    private static double Distancia(double lat, double lng, double otraLat, double otroLng)
    {
        var diferenciaLat = Math.Sin((otraLat - lat) * Radianes / 2);
        var diferenciaLng = Math.Sin((otroLng - lng) * Radianes / 2);
        var seno = diferenciaLat * diferenciaLat + Math.Cos(lat * Radianes) * Math.Cos(otraLat * Radianes)
            * diferenciaLng * diferenciaLng;
        return 2 * RadioTierraMetros * Math.Asin(Math.Sqrt(Math.Clamp(seno, 0, 1)));
    }

    private static string NormalizarDireccion(string? direccion)
    {
        var tramoInicial = direccion?.Split(',', 2)[0];
        var palabrasIniciales = Normalizar(tramoInicial).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Un sufijo después de una altura suele ser la localidad o el código postal del geocodificador.
        var palabras = palabrasIniciales.Skip(1).Any(p => p.Any(char.IsDigit))
            ? palabrasIniciales
            : Normalizar(direccion).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', palabras.Skip(palabras.Length > 0 && TiposCalle.Contains(palabras[0]) ? 1 : 0));
    }

    private static HashSet<string> PalabrasNombre(string? nombre) => Normalizar(nombre)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(p => p.Length >= 3 && !PalabrasGenericas.Contains(p)).ToHashSet();

    private static bool NombresSimilares(HashSet<string> a, HashSet<string> b)
    {
        var comunes = a.Intersect(b).Count();
        return comunes >= 2 && comunes * 3 >= (a.Count + b.Count - comunes) * 2
            || a.Count == 1 && b.Count == 1 && comunes == 1 && a.Single().Length >= 5;
    }

    internal static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var sinAcentos = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(sinAcentos.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    }
}
