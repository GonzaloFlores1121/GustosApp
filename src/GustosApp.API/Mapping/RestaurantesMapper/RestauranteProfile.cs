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
                .ForMember(dest => dest.Latitud, opt => opt.MapFrom(src => src.Latitud))
                .ForMember(dest => dest.Longitud, opt => opt.MapFrom(src => src.Longitud))
                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score))
                .ForMember(dest => dest.NivelCompatibilidad,
                    opt => opt.MapFrom(src => src.NivelCompatibilidad.ToString()))
                .ForMember(dest => dest.OrigenDatosCompatibilidad,
                    opt => opt.MapFrom(src => src.OrigenDatosCompatibilidad.ToString()))
                .ForMember(dest => dest.EstadoDatosCompatibilidad,
                    opt => opt.MapFrom(src =>
                        src.ObtenerEstadoActualDatosCompatibilidad(DateTime.UtcNow).ToString()))
                .ForMember(dest => dest.AdvertenciaCompatibilidad,
                    opt => opt.MapFrom(src => src.ObtenerEstadoActualDatosCompatibilidad(DateTime.UtcNow) == GustosApp.Domain.Common.EstadoDatosCompatibilidadRestaurante.Desactualizado
                        ? "Los datos de compatibilidad están desactualizados. Revisá el menú o consultá al restaurante antes de elegir."
                        : src.NivelCompatibilidad == GustosApp.Domain.Common.NivelCompatibilidadRestaurante.Desconocida
                        ? "No hay datos suficientes para confirmar la compatibilidad con todas las restricciones y condiciones médicas."
                        : src.NivelCompatibilidad == GustosApp.Domain.Common.NivelCompatibilidadRestaurante.Estimada
                            ? src.ObtenerEstadoActualDatosCompatibilidad(DateTime.UtcNow) == GustosApp.Domain.Common.EstadoDatosCompatibilidadRestaurante.Verificado
                                ? "Compatibilidad orientativa basada en datos verificados por el restaurante. Consultá ante restricciones médicas."
                                : "Compatibilidad estimada con datos que todavía no fueron verificados por el restaurante."
                            : "El restaurante presenta una incompatibilidad conocida."))
                .ForMember(dest => dest.GustosQueSirve, opt => opt.MapFrom(src =>
                    src.GustosQueSirve.Select(g => new GustoDto(g.Id, g.Nombre, g.ImagenUrl))))
                .ForMember(dest => dest.RestriccionesQueRespeta, opt => opt.MapFrom(src =>
                    src.RestriccionesQueRespeta.Select(r => new RestriccionResponse(r.Id, r.Nombre))))
                .ForMember(dest => dest.GooglePlaceId, opt => opt.MapFrom(src => src.PlaceId));
        }
    }
}
