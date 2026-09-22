using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.SolicitudesMapper;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.API.Tests;

public sealed class MapeoSolicitudRestaurantePruebas
{
    [Fact]
    public void Mapear_ReclamoConComprobante_DebeInformarloEnElListado()
    {
        var configuracion = new MapperConfiguration(
            expresion => expresion.AddProfile<SolicitudRestauranteProfile>(),
            NullLoggerFactory.Instance);
        var solicitud = new SolicitudRestaurante
        {
            ComprobanteReclamo = "%PDF-1.4\n%%EOF"u8.ToArray()
        };

        var resultado = configuracion.CreateMapper()
            .Map<SolicitudRestaurantePendienteDto>(solicitud);

        resultado.TieneComprobante.Should().BeTrue();
    }
}
