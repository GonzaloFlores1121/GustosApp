using Google.Protobuf.WellKnownTypes;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using GustosApp.Domain.Model.@enum;
using GustosApp.Infraestructure.Extrerno.GooglePlacesLegacy;
using GustosApp.Infraestructure.Extrerno.GooglePlacesModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;


namespace GustosApp.Infraestructure.Services
{
    public class ServicioRestaurantes : IServicioRestaurantes
    {
        private readonly GustosDbContext _db;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly IRestauranteRepository _repo;
        private readonly ILogger<ServicioRestaurantes> _logger;
        private readonly IApplicationTransaction _transaccion;

        public ServicioRestaurantes(GustosDbContext db,
        IConfiguration config, HttpClient http, IRestauranteRepository repo,
        ILogger<ServicioRestaurantes>? logger = null, IApplicationTransaction? transaccion = null)
        {
            _db = db;
            _config = config;
            _http = http;
            _repo = repo;
            _logger = logger ?? NullLogger<ServicioRestaurantes>.Instance;
            _transaccion = transaccion ?? new EfApplicationTransaction(db);
        }
        public async Task ActualizarReviewsDesdeGoogleLegacyAsync(Guid restauranteId, string placeId, CancellationToken ct)
        {
            // Se conserva el contrato de sólo reseñas; el transporte usa V2 para no poner claves en URLs.
            if (!await _db.Restaurantes.AnyAsync(r => r.Id == restauranteId && r.PlaceId == placeId, ct)) return;
            var apiKey = _config["GooglePlaces:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return;
            List<PlaceReview>? reviews;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}?languageCode=es");
                request.Headers.Add("X-Goog-Api-Key", apiKey);
                request.Headers.Add("X-Goog-FieldMask", "reviews");
                using var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return;
                reviews = (await response.Content.ReadFromJsonAsync<PlaceDetails>(cancellationToken: ct))?.Reviews;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("Reseñas Google no disponibles para {RestauranteId}. Tipo: {Tipo}", restauranteId, ex.GetType().Name);
                return;
            }
            if (reviews == null || reviews.Count == 0) return;
            var existentes = await _db.OpinionesRestaurantes
                .Where(x => x.RestauranteId == restauranteId && x.EsImportada && x.FuenteExterna == "GooglePlaces")
                .Select(x => new { x.AutorExterno, x.Opinion }).ToListAsync(ct);
            var nuevas = reviews.GroupBy(r => r.AuthorAttribution?.DisplayName, StringComparer.OrdinalIgnoreCase)
                .SelectMany(g => g.DistinctBy(r => r.Text?.Text, StringComparer.OrdinalIgnoreCase))
                .Where(r => !existentes.Any(e => string.Equals(e.AutorExterno, r.AuthorAttribution?.DisplayName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.Opinion, r.Text?.Text, StringComparison.OrdinalIgnoreCase)))
                .Select(r => new OpinionRestaurante { Id = Guid.NewGuid(), RestauranteId = restauranteId,
                    AutorExterno = r.AuthorAttribution?.DisplayName, FuenteExterna = "GooglePlaces",
                    ImagenAutorExterno = r.AuthorAttribution?.PhotoUri, Valoracion = r.Rating, Opinion = r.Text?.Text,
                    EsImportada = true, FechaCreacion = DateTime.UtcNow }).ToList();
            if (nuevas.Count > 0) { await _db.OpinionesRestaurantes.AddRangeAsync(nuevas, ct); await _db.SaveChangesAsync(ct); }
        }

        private static string NormalizarNombre(string nombre)
            => (nombre ?? string.Empty).Trim().ToLowerInvariant();

        public async Task<List<Restaurante>> BuscarAsync(
     double rating,
     string? tipo,
     string? plato,
     double? lat = null,
     double? lng = null,
     int? radioMetros = null)
        {
            IQueryable<Restaurante> query = _db.Restaurantes.Where(r => !r.Desactivado)
                .AsNoTracking()
                .Include(r => r.GustosQueSirve)
                    .ThenInclude(g => g.Tags)
                .Include(r => r.RestriccionesQueRespeta)
                .Include(r => r.Platos);

            // 1️⃣ FILTRAR POR TIPO DIRECTO EN SQL (si se puede mejorar después)
            if (!string.IsNullOrWhiteSpace(tipo))
            {
                var t = tipo.Trim().ToLower();
                query = query.Where(r =>
                    r.TypesJson != null &&
                    EF.Functions.Like(r.TypesJson.ToLower(), $"%\"{t}\"%"));
            }

            // 2️⃣ FILTRO GEOGRÁFICO USANDO SQL
            if (lat.HasValue && lng.HasValue && radioMetros.HasValue && radioMetros.Value > 0)
            {
                double latVal = lat.Value;
                double lngVal = lng.Value;

                double degLat = radioMetros.Value / 111_000.0;
                double degLng = radioMetros.Value / (111_000.0 * Math.Cos(latVal * Math.PI / 180.0));

                double minLat = latVal - degLat;
                double maxLat = latVal + degLat;
                double minLng = lngVal - degLng;
                double maxLng = lngVal + degLng;

                query = query.Where(r =>
                    r.Latitud >= minLat && r.Latitud <= maxLat &&
                    r.Longitud >= minLng && r.Longitud <= maxLng &&
                    (rating <= 0 || r.Rating >= rating));

                // Orden + Take también en SQL
                query = query.OrderBy(r =>
                    Math.Abs(r.Latitud - latVal) + Math.Abs(r.Longitud - lngVal))
                    .Take(200);
            }
            else
            {
                query = query.Where(r => rating <= 0 || r.Rating >= rating)
                    .OrderBy(r => r.NombreNormalizado)
                    .Take(1000);
            }

            return await query.ToListAsync();
        }



        /* public async Task<Restaurante> CrearAsync(string propietarioUid, CrearRestauranteDto dto)
         {
             var nombreNorm = NormalizarNombre(dto.Nombre);
             var nombreEnUso = await _db.Restaurantes.AsNoTracking()
                 .AnyAsync(r => r.NombreNormalizado == nombreNorm);
             if (nombreEnUso)
                 throw new ArgumentException("El nombre ya está en uso.");

             var primaryType = string.IsNullOrWhiteSpace(dto.PrimaryType)
                 ? "restaurant"
                 : dto.PrimaryType!.Trim();

             var typesList = (dto.Types ?? new())
                 .Where(t => !string.IsNullOrWhiteSpace(t))
                 .Select(t => t.Trim())
                 .Distinct()
                 .ToList();

             var platosParsed = (dto.Platos ?? new())
                 .Select(s =>
                 {
                     if (!System.Enum.TryParse<PlatoComida>(s, true, out var p))
                         throw new ArgumentException($"Plato inválido: {s}");
                     return p;
                 })
                 .Distinct()
                 .ToList();

             var ahora = DateTime.UtcNow;

             if (dto.Valoracion is null)
             {
                 dto.Valoracion = 0;
             }

             var (latOpt, lngOpt) = dto.Coordenadas;
             if (latOpt is null || lngOpt is null)
                 throw new ArgumentException("Lat/Lng requeridos en el cuerpo (usa 'lat' y 'lng' o 'latitud' y 'longitud')."); 

             var entidad = new Restaurante
                 {
                     Id = Guid.NewGuid(),
                     PropietarioUid = propietarioUid,
                     Nombre = dto.Nombre.Trim(),
                     NombreNormalizado = nombreNorm,
                     Direccion = dto.Direccion.Trim(),
                     Latitud = latOpt.Value,
                     Longitud = lngOpt.Value,
                     HorariosJson = dto.HorariosComoJson ?? "{}",
                     CreadoUtc = ahora,
                     ActualizadoUtc = ahora,
                     PrimaryType = primaryType,
                     TypesJson = JsonSerializer.Serialize(typesList),
                     ImagenUrl = string.IsNullOrWhiteSpace(dto.ImagenUrl) ? null : dto.ImagenUrl!.Trim(),
                     OrigenImagen = GustosApp.Domain.Common.OrigenImagenRestaurante.Propietario,
                     Valoracion = (decimal?)dto.Valoracion,
                     Rating= dto.Valoracion,
                     CantidadResenas = 0
             };

             foreach (var p in platosParsed)
                 entidad.Platos.Add(new RestaurantePlato { RestauranteId = entidad.Id, Plato = p });

             _db.Restaurantes.Add(entidad);
             await _db.SaveChangesAsync();
             await _db.Entry(entidad).Collection(x => x.Platos).LoadAsync();

             return entidad;
         }

         */
        public async Task<Restaurante?> ObtenerAsync(Guid id)
        {
            var r = await _db.Restaurantes

         .Include(r => r.Reviews)
          .ThenInclude(o => o.Usuario)
         .Include(r => r.Reviews)
           .ThenInclude(o => o.Fotos)

         .Include(r => r.Imagenes)

         .Include(r => r.Menu)
         .Include(r=>r.RestriccionesQueRespeta)
         .Include(r => r.GustosQueSirve)
         // ================================
         // PLATOS (si los usás en otra parte)
         // ================================
         //.Include(r => r.Platos)

         .FirstOrDefaultAsync(r => r.Id == id);

            return r;
        }


        public async Task<Restaurante?> ObtenerPorPropietarioAsync(Guid DuenoID)
        {
            var r = await _db.Restaurantes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DuenoId == DuenoID);
            return r is null ? null : r;
        }

        public async Task<IReadOnlyList<Restaurante>> ListarCercanosAsync(
            double lat, double lng, int radioMetros,
            string? tipo = null,
            IEnumerable<string>? platos = null)
        {
            double degLat = radioMetros / 111_000.0;
            double degLng = radioMetros / (111_000.0 * Math.Cos(lat * Math.PI / 180.0));

            var minLat = lat - degLat;
            var maxLat = lat + degLat;
            var minLng = lng - degLng;
            var maxLng = lng + degLng;

            var q = _db.Restaurantes.AsNoTracking().Where(r => !r.Desactivado)
                .Include(r => r.Platos)
                .Where(r => r.Latitud >= minLat && r.Latitud <= maxLat
                         && r.Longitud >= minLng && r.Longitud <= maxLng);

            if (!string.IsNullOrWhiteSpace(tipo))
            {
                var t = tipo.Trim();
                q = q.Where(r => r.PrimaryType == t || r.TypesJson.Contains($"\"{t}\""));
            }

            if (platos is not null)
            {
                var set = new HashSet<PlatoComida>(
                    platos.Select(p => System.Enum.TryParse<PlatoComida>(p, true, out var v) ? v : (PlatoComida?)null)
                          .Where(v => v.HasValue)
                          .Select(v => v!.Value));
                if (set.Count > 0)
                {
                    q = q.Where(r => r.Platos.Any(p => set.Contains(p.Plato)));
                }
            }

            var lista = await q
                .OrderBy(r => Math.Abs(r.Latitud - lat) + Math.Abs(r.Longitud - lng))
                .Take(200)
                .ToListAsync();

            return lista.ToList();
        }


        public async Task<bool> EliminarAsync(Guid id, string solicitanteUid, bool esAdmin)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("El borrado físico está deshabilitado. Usá la desactivación administrativa con motivo.");
        }

        public async Task<Restaurante> ObtenerResenasDesdeGooglePlaces(string placeId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(placeId))
                throw new ArgumentException("placeId requerido", nameof(placeId));
            return await ActualizarDesdeGoogleAsync(placeId, null, ct)
                ?? throw new KeyNotFoundException("La ficha ya no está disponible.");
        }

        public async Task<ResultadoActualizacionExternaRestaurante> ActualizarImportadoDesdeGooglePlacesAsync(Guid restauranteId, CancellationToken ct)
        {
            var inicial = await _db.Restaurantes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == restauranteId, ct);
            if (!EsImportadoSinPropietario(inicial)) return ResultadoActualizacionExternaRestaurante.Omitida;
            var resultado = await ActualizarDesdeGoogleAsync(inicial!.PlaceId, restauranteId, ct);
            return resultado is null ? ResultadoActualizacionExternaRestaurante.Omitida
                : resultado.EstadoUltimaActualizacionExterna == Domain.Common.EstadoActualizacionExternaRestaurante.Exitosa
                    ? ResultadoActualizacionExternaRestaurante.Exitosa : ResultadoActualizacionExternaRestaurante.Fallida;
        }

        private static bool EsImportadoSinPropietario(Restaurante? restaurante) => restaurante != null &&
            !string.IsNullOrWhiteSpace(restaurante.PlaceId) && !restaurante.DuenoId.HasValue &&
            string.IsNullOrWhiteSpace(restaurante.PropietarioUid);

        private async Task<Restaurante?> ActualizarDesdeGoogleAsync(string placeId, Guid? soloImportadoId, CancellationToken ct)
        {
            // La llamada de red no retiene bloqueos ni una copia antigua de datos editables.
            PlaceDetails? details = null;
            string? error = null;
            var apiKey = _config["GooglePlaces:ApiKey"];
            try
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("Configuración externa ausente.");
                const string fieldMask = "id,displayName,primaryType,types,priceLevel,rating,userRatingCount," +
                    "formattedAddress,internationalPhoneNumber,websiteUri,location,photos.name,currentOpeningHours,reviews";
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}?languageCode=es");
                req.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);
                req.Headers.TryAddWithoutValidation("X-Goog-FieldMask", fieldMask);
                using var resp = await _http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) error = $"GooglePlaces HTTP {(int)resp.StatusCode}";
                else
                {
                    details = await resp.Content.ReadFromJsonAsync<PlaceDetails>(cancellationToken: ct);
                    if (details is null || (details.DisplayName is null && details.FormattedAddress is null &&
                        !details.Rating.HasValue && !details.UserRatingCount.HasValue && details.Location is null &&
                        details.PrimaryType is null && details.Photos?.Count is not > 0 && details.Reviews?.Count is not > 0))
                        error = "GooglePlaces RespuestaVacia";
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Mensajes de proveedores/URLs/cuerpos pueden contener credenciales.
                error = $"GooglePlaces {ex.GetType().Name}";
            }

            Restaurante? actual = null;
            Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues? originales = null;
            var restaurantesPrevios = _db.ChangeTracker.Entries<Restaurante>().Select(e => e.Entity).ToHashSet();
            var opinionesPrevias = _db.ChangeTracker.Entries<OpinionRestaurante>().Select(e => e.Entity).ToHashSet();
            try
            {
                return await _transaccion.ExecuteAsync(async token =>
                {
                    actual = soloImportadoId.HasValue
                        ? await _db.Restaurantes.SingleOrDefaultAsync(r => r.Id == soloImportadoId.Value, token)
                        : await _db.Restaurantes.SingleOrDefaultAsync(r => r.PlaceId == placeId, token);
                    if (actual != null)
                    {
                        // También refrescar una entidad que el scope ya tuviera seguida.
                        await _db.Entry(actual).ReloadAsync(token);
                        if (_db.Entry(actual).State == EntityState.Detached) actual = null;
                    }
                    if (soloImportadoId.HasValue && (!EsImportadoSinPropietario(actual) || actual!.PlaceId != placeId))
                        return null;
                    if (actual != null) originales = _db.Entry(actual).CurrentValues.Clone();
                    return error != null
                        ? await RegistrarFalloActualizacionExternaAsync(actual, placeId, error, token)
                        : await AplicarDetallesGoogleAsync(actual, placeId, details!, apiKey!, token);
                }, ct);
            }
            catch
            {
                // Un Save/commit fallido no debe reaparecer al guardar el siguiente registro del lote.
                var nuevasFichas = _db.ChangeTracker.Entries<Restaurante>()
                    .Where(e => e.Entity.PlaceId == placeId && !restaurantesPrevios.Contains(e.Entity)).ToList();
                var ids = nuevasFichas.Select(e => e.Entity.Id).ToHashSet();
                if (actual != null) ids.Add(actual.Id);
                foreach (var entry in _db.ChangeTracker.Entries<OpinionRestaurante>()
                    .Where(e => ids.Contains(e.Entity.RestauranteId) && !opinionesPrevias.Contains(e.Entity)).ToList())
                {
                    entry.Entity.Restaurante?.Reviews.Remove(entry.Entity);
                    entry.State = EntityState.Detached;
                }
                foreach (var entry in nuevasFichas) entry.State = EntityState.Detached;
                if (actual != null && originales != null)
                {
                    _db.Entry(actual).CurrentValues.SetValues(originales);
                    _db.Entry(actual).OriginalValues.SetValues(originales);
                    _db.Entry(actual).State = EntityState.Unchanged;
                }
                throw;
            }
        }

        private async Task<Restaurante> AplicarDetallesGoogleAsync(
            Restaurante? existente,
            string placeId,
            PlaceDetails details,
            string apiKey,
            CancellationToken ct)
        {

            if (existente is null)
            {
                existente = new Restaurante
                {
                    Id = Guid.NewGuid(),
                    PropietarioUid = string.Empty,
                    Nombre = details.DisplayName?.Text ?? "(sin nombre)",
                    NombreNormalizado = (details.DisplayName?.Text ?? string.Empty).Trim().ToLowerInvariant(),
                    Direccion = details.FormattedAddress ?? string.Empty,
                    Latitud = details.Location?.Latitude ?? 0,
                    Longitud = details.Location?.Longitude ?? 0,
                    PlaceId = placeId,
                    Rating = details.Rating,
                    CantidadResenas = details.UserRatingCount,
                    Categoria = details.PrimaryType ?? "restaurant",
                    UltimaActualizacion = DateTime.UtcNow,
                    ImagenUrl = details.Photos?.Count > 0 ? GustosApp.Domain.Common.ImagenRestaurantePublica.GoogleActual : null,
                    OrigenImagen = GustosApp.Domain.Common.OrigenImagenRestaurante.Google,
                    Reviews = new List<OpinionRestaurante>()
                };

                await _repo.AddAsync(existente, ct);
            }
            else
            {
                var administradoPorPropietario = existente.DuenoId.HasValue ||
                    !string.IsNullOrWhiteSpace(existente.PropietarioUid);

                // Una vez reclamada la ficha, los datos editables quedan bajo control del propietario.
                // Google continúa siendo la fuente de métricas y opiniones externas.
                if (!administradoPorPropietario)
                {
                    if (!string.IsNullOrWhiteSpace(details.DisplayName?.Text))
                    {
                        existente.Nombre = details.DisplayName.Text;
                        existente.NombreNormalizado = NormalizarNombre(existente.Nombre);
                    }
                    if (!string.IsNullOrWhiteSpace(details.FormattedAddress)) existente.Direccion = details.FormattedAddress;
                    existente.Latitud = details.Location?.Latitude ?? existente.Latitud;
                    existente.Longitud = details.Location?.Longitude ?? existente.Longitud;
                    if (!string.IsNullOrWhiteSpace(details.PrimaryType)) existente.Categoria = details.PrimaryType;
                    if (existente.OrigenImagen == GustosApp.Domain.Common.OrigenImagenRestaurante.Google)
                        existente.ImagenUrl = details.Photos?.Count > 0 ? GustosApp.Domain.Common.ImagenRestaurantePublica.GoogleActual : null;
                    existente.ActualizadoUtc = DateTime.UtcNow;
                }

                existente.Rating = details.Rating ?? existente.Rating;
                existente.CantidadResenas = details.UserRatingCount ?? existente.CantidadResenas;
                existente.UltimaActualizacion = DateTime.UtcNow;

                // La ficha se recargó con seguimiento dentro de la transacción:
                // sólo se escriben los campos efectivamente modificados.
            }

            existente.RegistrarActualizacionExternaExitosa(DateTime.UtcNow);

            // Procesar opiniones desde Google
            if (details.Reviews is { Count: > 0 })
            {
                var opiniones = details.Reviews.Select(r => new OpinionRestaurante
                {
                    Id = Guid.NewGuid(),
                    RestauranteId = existente.Id,
                    UsuarioId = null, // no aplica para Google
                    AutorExterno = r.AuthorAttribution?.DisplayName ?? "Anónimo",
                    FuenteExterna = "GooglePlaces",
                    ImagenAutorExterno = r.AuthorAttribution?.PhotoUri,
                    Valoracion = r.Rating,
                    Titulo = "Opinión desde Google",
                    Opinion = r.Text?.Text ?? "",
                    EsImportada = true,
                    FechaCreacion = DateTime.UtcNow
                }).ToList();

                // Evitar duplicados (comparando texto + autor)
                var existentes = await _db.OpinionesRestaurantes
                    .Where(x => x.RestauranteId == existente.Id && x.EsImportada && x.FuenteExterna == "GooglePlaces")
                    .Select(x => new { x.AutorExterno, x.Opinion })
                    .ToListAsync(ct);

                var nuevas = opiniones
                    .GroupBy(o => o.AutorExterno, StringComparer.OrdinalIgnoreCase)
                    .SelectMany(grupo => grupo.DistinctBy(o => o.Opinion, StringComparer.OrdinalIgnoreCase))
                    .Where(o => !existentes.Any(e =>
                        string.Equals(e.AutorExterno, o.AutorExterno, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.Opinion, o.Opinion, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (nuevas.Any())
                {
                    await _db.OpinionesRestaurantes.AddRangeAsync(nuevas, ct);
                }
            }

            await _repo.SaveChangesAsync(ct);
            return existente;
        }

        private async Task<Restaurante> RegistrarFalloActualizacionExternaAsync(
            Restaurante? existente,
            string placeId,
            string detalle,
            CancellationToken ct)
        {
            _logger.LogWarning(
                "No se pudo actualizar el restaurante {PlaceId} desde Google Places: {Detalle}",
                placeId,
                detalle);

            if (existente is null)
            {
                var sinDatos = new Restaurante { PlaceId = placeId, Nombre = "(sin datos)" };
                sinDatos.RegistrarErrorActualizacionExterna(DateTime.UtcNow, detalle);
                return sinDatos;
            }

            existente.RegistrarErrorActualizacionExterna(DateTime.UtcNow, detalle);
            await _repo.SaveChangesAsync(ct);
            return existente;
        }


        public async Task<List<Restaurante>> BuscarAsync(
            double rating,
            double? lat,
            double? lng,
            int? radioMetros,
            List<string> gustos,
            List<string> restricciones)
        {
            if (lat.HasValue != lng.HasValue)
                throw new ArgumentException("La latitud y la longitud deben informarse juntas.");
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
                throw new ArgumentOutOfRangeException(nameof(lat), "Las coordenadas no son válidas.");
            if (radioMetros is <= 0 or > 50_000)
                throw new ArgumentOutOfRangeException(nameof(radioMetros), "El radio debe estar entre 1 y 50000 metros.");

            var baseQuery = _db.Restaurantes.AsNoTracking().Where(r => !r.Desactivado);

            // Los gustos y restricciones se evalúan en el recomendador. Filtrarlos aquí
            // impediría usar restaurantes con compatibilidad todavía desconocida como respaldo.

            baseQuery = baseQuery.Where(r => rating <= 0 ||
                (r.Valoracion.HasValue && r.Valoracion.Value > 0 && r.Valoracion.Value >= (decimal)rating) ||
                ((!r.Valoracion.HasValue || r.Valoracion.Value <= 0) &&
                    r.Rating.HasValue && r.Rating.Value >= rating));

            if (lat.HasValue && lng.HasValue && radioMetros is > 0)
            {
                double latVal = lat.Value;
                double lngVal = lng.Value;

                double degLat = radioMetros.Value / 111_000.0;
                double degLng = radioMetros.Value / (111_000.0 * Math.Cos(latVal * Math.PI / 180.0));

                double minLat = latVal - degLat;
                double maxLat = latVal + degLat;
                double minLng = lngVal - degLng;
                double maxLng = lngVal + degLng;

                baseQuery = baseQuery.Where(r =>
                    r.Latitud >= minLat && r.Latitud <= maxLat &&
                    r.Longitud >= minLng && r.Longitud <= maxLng);
            }
            else
            {
                baseQuery = baseQuery
                    .OrderBy(r => r.NombreNormalizado);
            }

            var candidatos = await baseQuery
                .Include(r => r.GustosQueSirve)
                    .ThenInclude(g => g.Tags)
                .Include(r => r.RestriccionesQueRespeta)
                .Include(r => r.Reviews)
                .ToListAsync();

            if (lat.HasValue && lng.HasValue && radioMetros.HasValue)
            {
                return candidatos
                    .Select(r => new { Restaurante = r, Distancia = CalcularDistanciaMetros(lat.Value, lng.Value, r.Latitud, r.Longitud) })
                    .Where(x => x.Distancia <= radioMetros.Value + 0.5)
                    .OrderBy(x => x.Distancia)
                    .Select(x => x.Restaurante)
                    .ToList();
            }

            return candidatos;
        }

        private static double CalcularDistanciaMetros(double lat1, double lng1, double lat2, double lng2)
        {
            const double radioTierra = 6_371_000d;
            var dLat = (lat2 - lat1) * Math.PI / 180d;
            var dLng = (lng2 - lng1) * Math.PI / 180d;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return radioTierra * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
