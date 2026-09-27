namespace GustosApp.Application.Configuration;

public sealed class EnlacesAplicacionOptions
{
    public const string Seccion = "EnlacesAplicacion";

    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
    public string? EmailAdministracion { get; set; }

    public string CrearUrl(string ruta)
    {
        var baseUrl = FrontendBaseUrl.TrimEnd('/');
        var rutaNormalizada = ruta.StartsWith('/') ? ruta : $"/{ruta}";
        return $"{baseUrl}{rutaNormalizada}";
    }
}
