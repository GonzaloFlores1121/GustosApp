using GustosApp.Domain.Model.@enum;
using Microsoft.AspNetCore.Authorization;

namespace GustosApp.API.Middleware;

public sealed record RolVigenteRequirement(RolUsuario Rol) : IAuthorizationRequirement;
