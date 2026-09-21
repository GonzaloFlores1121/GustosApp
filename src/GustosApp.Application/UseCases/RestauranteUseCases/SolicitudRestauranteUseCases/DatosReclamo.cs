namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed record DatosReclamo(string NombreSolicitante, string RelacionRestaurante,
    string TelefonoContacto, bool DeclaraAutorizacion, byte[] Comprobante, string NombreComprobante)
{
    public const int LimiteBytes = 2 * 1024 * 1024;

    public string Validar()
    {
        if (string.IsNullOrWhiteSpace(NombreSolicitante) || NombreSolicitante.Trim().Length > 150
            || string.IsNullOrWhiteSpace(RelacionRestaurante) || RelacionRestaurante.Trim().Length > 150
            || string.IsNullOrWhiteSpace(TelefonoContacto) || TelefonoContacto.Trim().Length > 40
            || TelefonoContacto.Count(char.IsDigit) < 6 || !DeclaraAutorizacion)
            throw new ArgumentException("Completá nombre, relación y teléfono, y confirmá tu autorización.");
        if (Comprobante is null || Comprobante.Length == 0 || Comprobante.Length > LimiteBytes)
            throw new ArgumentException("Adjuntá un comprobante de hasta 2 MB.");
        var extension = Path.GetExtension(NombreComprobante).ToLowerInvariant();
        if (extension == ".pdf" && Comprobante.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (extension == ".png" && Comprobante.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (extension is ".jpg" or ".jpeg" && Comprobante.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        throw new ArgumentException("El comprobante debe ser un archivo PDF, PNG o JPEG válido.");
    }
}
