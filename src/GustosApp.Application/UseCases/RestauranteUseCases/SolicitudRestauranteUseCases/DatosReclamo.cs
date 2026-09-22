using System.Text;
using System.Text.RegularExpressions;

namespace GustosApp.Application.UseCases.RestauranteUseCases.SolicitudRestauranteUseCases;

public sealed record DatosReclamo(string NombreSolicitante, string RelacionRestaurante,
    string TelefonoContacto, bool DeclaraAutorizacion, byte[] Comprobante, string NombreComprobante)
{
    public const int LimiteBytes = 2 * 1024 * 1024;
    public const int LongitudMaximaTelefono = 30;
    private static readonly Regex FormatoTelefono = new(@"^[+()\d\s.\-]+$", RegexOptions.Compiled);

    public string Validar()
    {
        ValidarTexto(NombreSolicitante, 150, "nombre");
        ValidarTexto(RelacionRestaurante, 150, "relación");
        var telefono = Normalizar(TelefonoContacto);
        var cantidadDigitos = telefono.Count(char.IsDigit);
        if (telefono.Length is 0 or > LongitudMaximaTelefono || !FormatoTelefono.IsMatch(telefono)
            || cantidadDigitos is < 6 or > 15 || !DeclaraAutorizacion)
            throw new ArgumentException("Completá nombre, relación y teléfono, y confirmá tu autorización.");
        if (Comprobante is null || Comprobante.Length == 0 || Comprobante.Length > LimiteBytes)
            throw new ArgumentException("Adjuntá un comprobante de hasta 2 MB.");
        var extension = Path.GetExtension(NombreComprobante).ToLowerInvariant();
        if (extension == ".pdf" && EsPdfValido()) return "application/pdf";
        if (extension == ".png" && EsPngValido()) return "image/png";
        if (extension is ".jpg" or ".jpeg" && EsJpegValido()) return "image/jpeg";
        throw new ArgumentException("El comprobante debe ser un archivo PDF, PNG o JPEG válido.");
    }

    public string NombreSolicitanteNormalizado => Normalizar(NombreSolicitante);
    public string RelacionRestauranteNormalizada => Normalizar(RelacionRestaurante);
    public string TelefonoContactoNormalizado => Normalizar(TelefonoContacto);

    private static string Normalizar(string valor) => valor.Trim().Normalize(NormalizationForm.FormC);

    private static void ValidarTexto(string valor, int longitudMaxima, string campo)
    {
        var normalizado = Normalizar(valor ?? string.Empty);
        if (normalizado.Length == 0 || normalizado.Length > longitudMaxima || normalizado.Any(char.IsControl))
            throw new ArgumentException($"El {campo} informado no es válido.");
    }

    private bool EsPdfValido()
    {
        var contenido = Comprobante.AsSpan();
        if (!contenido.StartsWith("%PDF-"u8)) return false;
        var texto = Encoding.ASCII.GetString(Comprobante);
        if (!texto.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal)) return false;
        string[] accionesActivas = ["/JavaScript", "/Launch", "/EmbeddedFile", "/OpenAction"];
        return accionesActivas.All(accion => !texto.Contains(accion, StringComparison.OrdinalIgnoreCase));
    }

    private bool EsPngValido()
    {
        ReadOnlySpan<byte> firma = [137, 80, 78, 71, 13, 10, 26, 10];
        ReadOnlySpan<byte> cierre = [73, 69, 78, 68, 174, 66, 96, 130];
        return Comprobante.AsSpan().StartsWith(firma) && Comprobante.AsSpan().EndsWith(cierre);
    }

    private bool EsJpegValido()
    {
        var contenido = Comprobante.AsSpan();
        return contenido.StartsWith(new byte[] { 255, 216, 255 }) && contenido.EndsWith(new byte[] { 255, 217 });
    }
}
