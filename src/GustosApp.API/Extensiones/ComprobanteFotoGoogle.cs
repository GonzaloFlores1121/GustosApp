using GustosApp.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace GustosApp.API.Extensiones;

public sealed class ComprobanteFotoGoogle(IDataProtectionProvider provider) : IComprobanteFotoGoogle
{
    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector("GustosApp.FotoGoogle.v1").ToTimeLimitedDataProtector();
    public string Emitir(string placeId) => _protector.Protect(placeId, TimeSpan.FromMinutes(5));
    public bool Validar(string? comprobante, string placeId)
    {
        if (string.IsNullOrWhiteSpace(comprobante) || comprobante.Length > 4096) return false;
        try { return string.Equals(_protector.Unprotect(comprobante), placeId, StringComparison.Ordinal); }
        catch { return false; }
    }
}
