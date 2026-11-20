using AutoMapper;
using GustosApp.API.DTO;
using GustosApp.Domain.Model;

namespace GustosApp.API.Mapping.RestaurantesMapper
{
    public class RestauranteListadoProfile : Profile
    {
        public RestauranteListadoProfile()
        {
            CreateMap<Restaurante, RestauranteListadoDto>()
                .ForMember(dest => dest.ImagenUrl, opt => opt.MapFrom(src => GustosApp.Domain.Common.ImagenRestaurantePublica.Url(src.Id, src.ImagenUrl)))
                .ForMember(dest => dest.PrimaryType, opt => opt.MapFrom(src => src.Categoria));
        }
    }
}
