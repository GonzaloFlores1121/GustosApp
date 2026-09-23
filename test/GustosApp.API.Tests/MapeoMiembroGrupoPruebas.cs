using System.Text.Json;
using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.GruposMapper;
using GustosApp.Domain.Model;
using GustosApp.Infraestructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.API.Tests;

public sealed class MapeoMiembroGrupoPruebas
{
    [Fact]
    public void PropiedadRenombrada_ConservaColumnaSqlExistente()
    {
        var opciones = new DbContextOptionsBuilder<GustosDbContext>()
            .UseSqlServer("Server=localhost;Database=GustosAppMapeoPruebas;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var contexto = new GustosDbContext(opciones);

        var entidad = contexto.Model.FindEntityType(typeof(MiembroGrupo));
        entidad.Should().NotBeNull();
        var propiedad = entidad!.FindProperty(nameof(MiembroGrupo.ParticipaEnRecomendacion));
        propiedad.Should().NotBeNull();
        var tabla = StoreObjectIdentifier.Table(entidad.GetTableName()!, entidad.GetSchema());

        propiedad!.GetColumnName(tabla).Should().Be("afectarRecomendacion");
        entidad.FindProperty("afectarRecomendacion").Should().BeNull();
    }

    [Fact]
    public void RespuestaMiembro_ConservaCampoJsonExistente()
    {
        var configuracion = new MapperConfiguration(
            opciones => opciones.AddProfile<MiembroGrupoProfile>(),
            NullLoggerFactory.Instance);
        var mapper = configuracion.CreateMapper();
        var usuario = new Usuario("uid", "prueba@example.com", "Usuario", "Prueba", "usuario-prueba");
        var miembro = new MiembroGrupo(Guid.NewGuid(), usuario.Id)
        {
            Usuario = usuario,
            ParticipaEnRecomendacion = false
        };

        var respuesta = mapper.Map<MiembroGrupoResponse>(miembro);
        respuesta.afectarRecomendacion.Should().BeFalse();

        var json = JsonSerializer.Serialize(respuesta, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("afectarRecomendacion").GetBoolean().Should().BeFalse();
        documento.RootElement.TryGetProperty("participaEnRecomendacion", out _).Should().BeFalse();
    }
}
