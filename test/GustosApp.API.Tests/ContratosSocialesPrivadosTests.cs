using System.Text.Json;
using AutoMapper;
using FluentAssertions;
using GustosApp.API.DTO;
using GustosApp.API.Mapping.AmistadMapper;
using GustosApp.API.Mapping.GruposMapper;
using GustosApp.API.Mapping.RestaurantesMapper;
using GustosApp.API.Mapping.UsuariosMapper;
using GustosApp.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace GustosApp.API.Tests;

public sealed class ContratosSocialesPrivadosTests
{
    private readonly IMapper _mapper = new MapperConfiguration(config =>
    {
        config.AddProfile<UsuarioSimpleProfile>();
        config.AddProfile<AmistadProfile>();
        config.AddProfile<MiembroGrupoProfile>();
        config.AddProfile<InvitacionGrupoProfile>();
        config.AddProfile<RestauranteDetalleProfile>();
        config.AddProfile<OpinionRestauranteProfile>();
    }, NullLoggerFactory.Instance).CreateMapper();

    [Fact]
    public void BusquedaYAmistad_NoSerializanCorreosDeTerceros()
    {
        var usuario = CrearUsuario("tercero");
        var simple = _mapper.Map<UsuarioSimpleResponse>(usuario);
        var solicitud = new SolicitudAmistad(usuario.Id, Guid.NewGuid())
        {
            Remitente = usuario,
            Destinatario = CrearUsuario("destinatario"),
        };

        Serializar(simple).ToLowerInvariant().Should().NotContain("email");
        Serializar(_mapper.Map<SolicitudAmistadResponse>(solicitud))
            .ToLowerInvariant().Should().NotContain("email");
    }

    [Fact]
    public void MiembrosEInvitacionesDeGrupo_NoSerializanCorreosDeTerceros()
    {
        var invitado = CrearUsuario("invitado");
        var invitador = CrearUsuario("invitador");
        var grupo = new Grupo("Grupo", invitador.Id);
        var miembro = new MiembroGrupo(grupo.Id, invitado.Id) { Usuario = invitado };
        var invitacion = new InvitacionGrupo(grupo.Id, invitado.Id, invitador.Id)
        {
            Grupo = grupo,
            UsuarioInvitado = invitado,
            UsuarioInvitador = invitador,
        };

        Serializar(_mapper.Map<MiembroGrupoResponse>(miembro))
            .ToLowerInvariant().Should().NotContain("email");
        Serializar(_mapper.Map<InvitacionGrupoResponse>(invitacion))
            .ToLowerInvariant().Should().NotContain("email");
    }

    [Fact]
    public void ContratoDelPropioUsuario_ConservaEmail()
    {
        typeof(UsuarioResponse).GetProperty(nameof(UsuarioResponse.Email)).Should().NotBeNull();
        typeof(UsuarioSimpleResponse).GetProperty("Email").Should().BeNull();
    }

    [Fact]
    public void DetalleRestaurante_ConservaResenaDeAutorConPerfilPrivado_SinExponerEmail()
    {
        var autor = CrearUsuario("autor-privado");
        autor.EsPrivado = true;
        var restaurante = new Restaurante
        {
            Id = Guid.NewGuid(),
            Nombre = "Restaurante",
            Reviews =
            [
                new OpinionRestaurante
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = autor.Id,
                    Usuario = autor,
                    Opinion = "Reseña pública en la ficha",
                    Valoracion = 5,
                    FechaCreacion = DateTime.UtcNow,
                },
            ],
        };

        var detalle = _mapper.Map<RestauranteDetalleDto>(restaurante);

        detalle.ReviewsLocales.Should().ContainSingle()
            .Which.Opinion.Should().Be("Reseña pública en la ficha");
        Serializar(detalle.ReviewsLocales).ToLowerInvariant().Should().NotContain("email");
    }

    private static string Serializar<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static Usuario CrearUsuario(string username) => new()
    {
        Id = Guid.NewGuid(), FirebaseUid = $"uid-{username}", Email = $"{username}@example.test",
        Nombre = username, Apellido = "Prueba", IdUsuario = username,
    };
}
