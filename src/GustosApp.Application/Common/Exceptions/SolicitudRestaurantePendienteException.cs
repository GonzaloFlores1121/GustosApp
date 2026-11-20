namespace GustosApp.Application.Common.Exceptions;

public sealed class SolicitudRestaurantePendienteException() :
    InvalidOperationException("Ya tenés una solicitud de restaurante pendiente. Consultá el trámite en curso.");

public sealed class SolicitudRestauranteConcurrenteException(Exception causa) :
    InvalidOperationException("Ya existe una solicitud pendiente para este usuario.", causa);
