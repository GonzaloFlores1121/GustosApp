namespace GustosApp.Application.Interfaces;

public interface IComprobanteFotoGoogle
{
    string Emitir(string placeId);
    bool Validar(string? comprobante, string placeId);
}
