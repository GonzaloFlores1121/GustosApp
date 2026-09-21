using System.ComponentModel.DataAnnotations;

namespace GustosApp.API.DTO;

public sealed class ReclamarRestauranteDto
{
    [Required, MaxLength(150)] public string NombreSolicitante { get; set; } = "";
    [Required, MaxLength(150)] public string RelacionRestaurante { get; set; } = "";
    [Required, MaxLength(40)] public string TelefonoContacto { get; set; } = "";
    public bool DeclaraAutorizacion { get; set; }
    [Required] public IFormFile Comprobante { get; set; } = default!;
}
