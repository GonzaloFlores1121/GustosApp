using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using SixLabors.ImageSharp;
using System.Globalization;

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
        if (extension == ".png" && EsImagenValida("PNG")) return "image/png";
        if (extension is ".jpg" or ".jpeg" && EsImagenValida("JPEG")) return "image/jpeg";
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
        // Los nombres PDF admiten escapes hexadecimales (#NN). Normalizarlos evita
        // aceptar, por ejemplo, /J#61vaScript como una acción inofensiva.
        var nombresNormalizados = Regex.Replace(texto, @"#([0-9a-fA-F]{2})", match =>
            ((char)byte.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
        string[] accionesActivas = ["/JavaScript", "/JS", "/Launch", "/EmbeddedFile", "/OpenAction",
            "/AA", "/GoToR", "/SubmitForm", "/ImportData", "/RichMedia", "/AcroForm", "/XFA", "/Filespec"];
        if (accionesActivas.Any(accion => nombresNormalizados.Contains(accion, StringComparison.OrdinalIgnoreCase))) return false;
        try
        {
            using var stream = new MemoryStream(Comprobante, writable: false);
            using var documento = PdfDocument.Open(stream, ParsingOptions.LenientParsingOff);
            if (documento.IsEncrypted || documento.NumberOfPages is < 1 or > 20) return false;
            for (var pagina = 1; pagina <= documento.NumberOfPages; pagina++)
                _ = documento.GetPage(pagina);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private bool EsImagenValida(string formatoEsperado)
    {
        try
        {
            using var imagen = Image.Load(Comprobante);
            return imagen.Metadata.DecodedImageFormat?.Name == formatoEsperado &&
                   imagen.Frames.Count == 1 && imagen.Width <= 12_000 && imagen.Height <= 12_000 &&
                   (long)imagen.Width * imagen.Height <= 40_000_000;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
