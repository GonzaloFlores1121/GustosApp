using AutoMapper;
using GustosApp.API.DTO;
using GustosApp.Domain.Model;

namespace GustosApp.API.Mapping.RestaurantesMapper
{
    public class RestauranteProfile : Profile
    {
        public RestauranteProfile()
        {
            CreateMap<Restaurante, RestauranteDTO>()
                .ForMember(dest => dest.ImagenUrl, opt => opt.MapFrom(src => GustosApp.Domain.Common.ImagenRestaurantePublica.Url(src.Id, src.ImagenUrl)))
                .ForMember(dest => dest.Latitud, opt => opt.MapFrom(src => src.Latitud))
                .ForMember(dest => dest.Longitud, opt => opt.MapFrom(src => src.Longitud))
                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score))
                .ForMember(dest => dest.PuntuacionPrincipal, opt => opt.MapFrom(src => src.ObtenerPuntuacionPrincipal()))
                .ForMember(dest => dest.FuentePuntuacionPrincipal, opt => opt.MapFrom(src => src.ObtenerFuentePuntuacionPrincipal()))
                .ForMember(dest => dest.ValoracionLocal, opt => opt.MapFrom(src => src.ObtenerValoracionLocal()))
                .ForMember(dest => dest.CantidadResenasLocales, opt => opt.MapFrom(src => src.ObtenerCantidadResenasLocales()))
                .ForMember(dest => dest.RatingGoogle, opt => opt.MapFrom(src => src.Rating.HasValue && src.Rating.Value > 0 ? src.Rating : null))
                .ForMember(dest => dest.CantidadResenasGoogle, opt => opt.MapFrom(src => src.CantidadResenas))
                .ForMember(dest => dest.NivelCompatibilidad,
                    opt => opt.MapFrom(src => src.NivelCompatibilidad.ToString()))
                .ForMember(dest => dest.EstadoOferta,
                    opt => opt.MapFrom(src => src.ObtenerEstadoOferta(DateTime.UtcNow).ToString()))
                .ForMember(dest => dest.EstadoRestricciones,
                    opt => opt.MapFrom(src => src.ObtenerEstadoRestricciones(DateTime.UtcNow).ToString()))
                .ForMember(dest => dest.OrigenDatosCompatibilidad,
                    opt => opt.MapFrom(src => src.OrigenDatosCompatibilidad.ToString()))
                .ForMember(dest => dest.EstadoDatosCompatibilidad,
                    opt => opt.MapFrom(src =>
                        src.ObtenerEstadoActualDatosCompatibilidad(DateTime.UtcNow).ToString()))
                .ForMember(dest => dest.AdvertenciaCompatibilidad,
                    opt => opt.MapFrom(src => ObtenerAdvertencia(src)))
                .ForMember(dest => dest.GustosQueSirve, opt => opt.MapFrom(src =>
                    src.GustosQueSirve.Select(g => new GustoDto(g.Id, g.Nombre, g.ImagenUrl))))
                .ForMember(dest => dest.RestriccionesQueRespeta, opt => opt.MapFrom(src =>
                    src.RestriccionesQueRespeta.Select(r => new RestriccionResponse(r.Id, r.Nombre))))
                .ForMember(dest => dest.GooglePlaceId, opt => opt.MapFrom(src => src.PlaceId));
        }

        private static string ObtenerAdvertencia(Restaurante restaurante)
        {
            var advertencia = restaurante.NivelCompatibilidad switch
            {
                GustosApp.Domain.Common.NivelCompatibilidadRestaurante.Estimada =>
                    "Evaluación orientativa. Las declaraciones generales no confirman un plato concreto; consultá ingredientes y preparación.",
                GustosApp.Domain.Common.NivelCompatibilidadRestaurante.Incompatible =>
                    "Las opciones registradas presentan conflictos conocidos para esta búsqueda.",
                _ => "No hay datos suficientes para confirmar opciones para todas las preferencias de esta búsqueda."
            };
            if (restaurante.ObtenerEstadoOferta(DateTime.UtcNow) ==
                GustosApp.Domain.Common.EstadoVigenciaInformacion.Desactualizada)
                advertencia += " Oferta sin confirmar recientemente.";
            else if (!restaurante.FechaRevisionOfertaUtc.HasValue)
                advertencia += " Oferta sin fecha de revisión.";
            if (restaurante.RestriccionesQueRespeta.Any(r =>
                    restaurante.ObtenerEstadoDeclaracion(r.Id, DateTime.UtcNow) !=
                    GustosApp.Domain.Common.EstadoVigenciaInformacion.Vigente))
                advertencia += " Hay declaraciones pendientes de confirmar.";
            return advertencia;
        }
    }
}
