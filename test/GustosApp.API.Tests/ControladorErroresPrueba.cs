using Microsoft.AspNetCore.Mvc;

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
}
