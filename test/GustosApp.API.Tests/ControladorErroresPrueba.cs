using Microsoft.AspNetCore.Mvc;
using GustosApp.Application.Common.Exceptions;

namespace GustosApp.API.Tests;

[ApiController]
[Route("pruebas/errores")]
public sealed class ControladorErroresPrueba : ControllerBase
{
    [HttpGet("interno")]
    public IActionResult LanzarErrorInterno()
    {
        throw new Exception(
            "detalle-interno-sensible",
            new Exception("causa-interna-sensible"));
    }

    [HttpGet("prohibido")]
    public IActionResult LanzarAccesoProhibido()
    {
        throw new AccesoProhibidoException("No tenés permisos para acceder a este recurso.");
    }
}
