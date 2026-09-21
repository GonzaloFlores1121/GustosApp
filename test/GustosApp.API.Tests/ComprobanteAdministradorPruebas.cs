using System.Security.Claims;
using GustosApp.API.Controllers;
using GustosApp.Domain.Interfaces;
using GustosApp.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GustosApp.API.Tests;

public class ComprobanteAdministradorPruebas
{
    [Fact]
    public async Task Administrador_PuedeDescargarComprobanteDeOtroSolicitante()
    {
        var solicitud = new SolicitudRestaurante { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(), TipoComprobante = "application/pdf", ComprobanteReclamo = "%PDF-prueba"u8.ToArray() };
        var solicitudes = new Mock<ISolicitudRestauranteRepository>();
        solicitudes.Setup(r => r.GetByIdAsync(solicitud.Id, default)).ReturnsAsync(solicitud);
        var autorizacion = new Mock<IAuthorizationService>();
        autorizacion.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "Admin")).ReturnsAsync(AuthorizationResult.Success());
        var controlador = new ComprobantesReclamoController(solicitudes.Object, Mock.Of<IUsuarioRepository>(), autorizacion.Object)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var archivo = Assert.IsType<FileContentResult>(await controlador.Descargar(solicitud.Id, default));
        Assert.Equal(solicitud.ComprobanteReclamo, archivo.FileContents);
        Assert.Equal("comprobante.pdf", archivo.FileDownloadName);
        Assert.Equal("no-store", controlador.Response.Headers.CacheControl);
    }
}
