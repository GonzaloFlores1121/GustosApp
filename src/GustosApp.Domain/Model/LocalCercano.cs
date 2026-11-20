namespace GustosApp.Domain.Model;

public sealed record LocalCercano(Guid Id, string Nombre, string Direccion, double Latitud, double Longitud, bool Administrado);
