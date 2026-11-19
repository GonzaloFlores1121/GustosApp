using System;
using System.Collections.Generic;

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Common;
using System.Text.Json;

using System.Threading.Tasks;


namespace GustosApp.Domain.Model
{
    public class Restaurante
    {
        public Guid Id { get; set; }

        // deprecated pero mantenido por compatibilidad
         public string PropietarioUid { get; set; } = string.Empty;

        //Nuevo
        public Guid? DuenoId { get; set; }
        public Usuario? Dueno { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string NombreNormalizado { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public double Latitud { get; set; }
        public double Longitud { get; set; }
    
        public string HorariosJson { get; set; } = "{}";
        public DateTime CreadoUtc { get; set; }
        public DateTime ActualizadoUtc { get; set; }

        public string PlaceId { get; set; } 
        
        public double? Rating { get; set; }
        public int? CantidadResenas { get; set; }
        public string? Categoria { get; set; }

        public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaUltimoIntentoActualizacionExternaUtc { get; private set; }
        public DateTime? FechaUltimaActualizacionExternaExitosaUtc { get; private set; }
        public EstadoActualizacionExternaRestaurante EstadoUltimaActualizacionExterna { get; private set; } =
            EstadoActualizacionExternaRestaurante.Nunca;
        public string? ErrorUltimaActualizacionExterna { get; private set; }

        public string? WebUrl { get; set; }
        public string? EmbeddingVector { get; set; }

        // Menú (OCR)
        public bool? MenuProcesado { get; set; }
        public string? MenuError { get; set; }

        public RestauranteEstadisticas? Estadisticas { get; set; }

        public RestauranteMenu? Menu { get; set; }


        public ICollection<OpinionRestaurante> Reviews { get; set; } = new List<OpinionRestaurante>();
        public ICollection<Gusto> GustosQueSirve { get; set; } = new List<Gusto>();
        public ICollection<Restriccion> RestriccionesQueRespeta { get; set; } = new List<Restriccion>();

        public double? ObtenerValoracionLocal()
        {
            var locales = Reviews?
                .Where(r => !r.EsImportada && r.UsuarioId.HasValue)
                .Select(r => r.Valoracion)
                .ToList() ?? new List<double>();

            if (locales.Count > 0)
                return locales.Average();

            return Valoracion is > 0 ? (double)Valoracion.Value : null;
        }

        public int ObtenerCantidadResenasLocales() =>
            Reviews?.Count(r => !r.EsImportada && r.UsuarioId.HasValue) ?? 0;

        public double? ObtenerPuntuacionPrincipal() =>
            ObtenerValoracionLocal() ?? (Rating is > 0 ? Rating : null);

        public string? ObtenerFuentePuntuacionPrincipal() =>
            ObtenerValoracionLocal().HasValue
                ? "GustosApp"
                : Rating is > 0 ? "Google" : null;

       
        // ====== V2 ======

        public string PrimaryType { get; set; } = "restaurant";
        public string TypesJson { get; set; } = "[]";

        public string? ImagenUrl { get; set; }
        public OrigenImagenRestaurante OrigenImagen { get; set; } = OrigenImagenRestaurante.Desconocida;
        public decimal? Valoracion { get; set; }                  
        public ICollection<RestaurantePlato> Platos { get; set; } = new List<RestaurantePlato>();
        public double Score { get; set; }
        public NivelCompatibilidadRestaurante NivelCompatibilidad { get; set; } =
            NivelCompatibilidadRestaurante.Desconocida;
        public OrigenDatosCompatibilidadRestaurante OrigenDatosCompatibilidad { get; private set; } =
            OrigenDatosCompatibilidadRestaurante.Estimacion;
        public EstadoDatosCompatibilidadRestaurante EstadoDatosCompatibilidad { get; private set; } =
            EstadoDatosCompatibilidadRestaurante.Estimado;
        public DateTime? FechaObtencionDatosCompatibilidadUtc { get; private set; }
        public DateTime? FechaUltimaVerificacionDatosCompatibilidadUtc { get; private set; }
        public DateTime? FechaRevisionOfertaUtc { get; private set; }
        public DateTime? FechaRevisionRestriccionesUtc { get; private set; }
        public string VigenciaGustosJson { get; private set; } = "{}";
        public string VigenciaRestriccionesJson { get; private set; } = "{}";
        public int VersionOferta { get; private set; }
        public int VersionRestricciones { get; private set; }


        public EstadoVigenciaInformacion ObtenerEstadoOferta(DateTime ahoraUtc) =>
            GustosQueSirve.Count > 0 || (MenuProcesado == true && !string.IsNullOrWhiteSpace(Menu?.Json))
                ? EstadoVigenciaInformacion.Vigente : EstadoVigenciaInformacion.SinFecha;

        public EstadoVigenciaInformacion ObtenerEstadoRestricciones(DateTime ahoraUtc) =>
            RestriccionesQueRespeta.Count > 0
                ? EstadoVigenciaInformacion.Vigente : EstadoVigenciaInformacion.SinFecha;

        public EstadoVigenciaInformacion ObtenerEstadoDeclaracion(Guid restriccionId, DateTime ahoraUtc) =>
            RestriccionesQueRespeta.Any(r => r.Id == restriccionId)
                ? EstadoVigenciaInformacion.Vigente : EstadoVigenciaInformacion.SinFecha;

        public DateTime? ObtenerFechaRevisionGusto(Guid gustoId) =>
            LeerVigencias(VigenciaGustosJson).GetValueOrDefault(gustoId);

        public DateTime? ObtenerFechaRevisionDeclaracion(Guid restriccionId) =>
            LeerVigencias(VigenciaRestriccionesJson).GetValueOrDefault(restriccionId);

        public void RegistrarCambioOferta(IEnumerable<Guid> idsAnteriores, DateTime fechaUtc)
        {
            var nuevos = GustosQueSirve.Select(g => g.Id).ToHashSet();
            var anteriores = idsAnteriores.ToHashSet();
            if (nuevos.SetEquals(anteriores)) return;
            var fechas = LeerVigencias(VigenciaGustosJson);
            foreach (var id in anteriores.Except(nuevos)) fechas.Remove(id);
            foreach (var id in nuevos.Except(anteriores)) fechas[id] = NormalizarFechaUtc(fechaUtc);
            VigenciaGustosJson = JsonSerializer.Serialize(fechas);
            FechaRevisionOfertaUtc = null;
            VersionOferta++;
        }

        public void RegistrarCambioRestricciones(IEnumerable<Guid> idsAnteriores, DateTime fechaUtc)
        {
            var nuevos = RestriccionesQueRespeta.Select(r => r.Id).ToHashSet();
            var anteriores = idsAnteriores.ToHashSet();
            if (nuevos.SetEquals(anteriores)) return;
            var fechas = LeerVigencias(VigenciaRestriccionesJson);
            foreach (var id in anteriores.Except(nuevos)) fechas.Remove(id);
            foreach (var id in nuevos.Except(anteriores)) fechas[id] = NormalizarFechaUtc(fechaUtc);
            VigenciaRestriccionesJson = JsonSerializer.Serialize(fechas);
            FechaRevisionRestriccionesUtc = null;
            VersionRestricciones++;
        }

        public void RegistrarCambioMenuPublicado()
        {
            FechaRevisionOfertaUtc = null;
            VersionOferta++;
        }

        public void ConfirmarOfertaVigente(int versionEsperada, DateTime fechaUtc)
        {
            if (versionEsperada != VersionOferta)
                throw new InvalidOperationException("La oferta cambió; recargá la ficha antes de confirmarla.");
            var fecha = NormalizarFechaUtc(fechaUtc);
            FechaRevisionOfertaUtc = fecha;
            VigenciaGustosJson = JsonSerializer.Serialize(
                GustosQueSirve.ToDictionary(g => g.Id, _ => fecha));
            VersionOferta++;
        }

        public void ConfirmarRestriccionesDeclaradas(int versionEsperada, DateTime fechaUtc)
        {
            if (versionEsperada != VersionRestricciones)
                throw new InvalidOperationException("Las declaraciones cambiaron; recargá la ficha antes de confirmarlas.");
            var fecha = NormalizarFechaUtc(fechaUtc);
            FechaRevisionRestriccionesUtc = fecha;
            VigenciaRestriccionesJson = JsonSerializer.Serialize(
                RestriccionesQueRespeta.ToDictionary(r => r.Id, _ => fecha));
            VersionRestricciones++;
        }

        private static Dictionary<Guid, DateTime> LeerVigencias(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<Guid, DateTime>>(json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        // Imágenes
     
        public string? LogoUrl { get; set; }
        public OrigenImagenRestaurante OrigenLogo { get; set; } = OrigenImagenRestaurante.Desconocida;
        public ICollection<RestauranteImagen> Imagenes { get; set; } = new List<RestauranteImagen>();

        public void SetGustos(IEnumerable<Gusto> nuevosGustos)
        {
            if (nuevosGustos == null)
                nuevosGustos = Enumerable.Empty<Gusto>();

            // Remover los que ya no están
            var idsNuevos = nuevosGustos.Select(g => g.Id).ToHashSet();

            var viejos = GustosQueSirve
                .Where(g => !idsNuevos.Contains(g.Id))
                .ToList();

            foreach (var r in viejos)
                GustosQueSirve.Remove(r);

            // Agregar los nuevos que no estaban
            var idsViejos = GustosQueSirve.Select(g => g.Id).ToHashSet();

            foreach (var g in nuevosGustos)
                if (!idsViejos.Contains(g.Id))
                    GustosQueSirve.Add(g);
        }

        public void SetRestricciones(IEnumerable<Restriccion> nuevasRestricciones)
        {
            if (nuevasRestricciones == null)
                nuevasRestricciones = Enumerable.Empty<Restriccion>();

            var idsNuevas = nuevasRestricciones.Select(r => r.Id).ToHashSet();

            // Remover las que ya no van
            var viejas = RestriccionesQueRespeta
                .Where(r => !idsNuevas.Contains(r.Id))
                .ToList();

            foreach (var r in viejas)
                RestriccionesQueRespeta.Remove(r);

            // Agregar nuevas
            var idsViejos = RestriccionesQueRespeta.Select(r => r.Id).ToHashSet();

            foreach (var r in nuevasRestricciones)
                if (!idsViejos.Contains(r.Id))
                    RestriccionesQueRespeta.Add(r);
        }

        public void RegistrarActualizacionExternaExitosa(DateTime fechaUtc)
        {
            var fecha = NormalizarFechaUtc(fechaUtc);
            FechaUltimoIntentoActualizacionExternaUtc = fecha;
            FechaUltimaActualizacionExternaExitosaUtc = fecha;
            EstadoUltimaActualizacionExterna = EstadoActualizacionExternaRestaurante.Exitosa;
            ErrorUltimaActualizacionExterna = null;
        }

        public void RegistrarErrorActualizacionExterna(DateTime fechaUtc, string detalleTecnico)
        {
            FechaUltimoIntentoActualizacionExternaUtc = NormalizarFechaUtc(fechaUtc);
            EstadoUltimaActualizacionExterna = EstadoActualizacionExternaRestaurante.Error;
            ErrorUltimaActualizacionExterna = string.IsNullOrWhiteSpace(detalleTecnico)
                ? "Error externo sin detalle."
                : detalleTecnico.Trim()[..Math.Min(detalleTecnico.Trim().Length, 2000)];
        }

        public void RegistrarDatosCompatibilidadEstimados(
            OrigenDatosCompatibilidadRestaurante origen,
            DateTime fechaObtencionUtc)
        {
            OrigenDatosCompatibilidad = origen;
            EstadoDatosCompatibilidad = EstadoDatosCompatibilidadRestaurante.Estimado;
            FechaObtencionDatosCompatibilidadUtc = NormalizarFechaUtc(fechaObtencionUtc);
            FechaUltimaVerificacionDatosCompatibilidadUtc = null;
        }

        public void RegistrarVerificacionDatosCompatibilidad(
            OrigenDatosCompatibilidadRestaurante origen,
            DateTime fechaVerificacionUtc)
        {
            var fechaUtc = NormalizarFechaUtc(fechaVerificacionUtc);

            OrigenDatosCompatibilidad = origen;
            EstadoDatosCompatibilidad = EstadoDatosCompatibilidadRestaurante.Verificado;
            FechaObtencionDatosCompatibilidadUtc ??= fechaUtc;
            FechaUltimaVerificacionDatosCompatibilidadUtc = fechaUtc;
        }

        public EstadoDatosCompatibilidadRestaurante ObtenerEstadoActualDatosCompatibilidad(
            DateTime fechaReferenciaUtc)
        {
            return EstadoDatosCompatibilidad;
        }

        private static DateTime NormalizarFechaUtc(DateTime fecha)
        {
            return fecha.Kind switch
            {
                DateTimeKind.Utc => fecha,
                DateTimeKind.Local => fecha.ToUniversalTime(),
                _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
            };
        }



    }

}
