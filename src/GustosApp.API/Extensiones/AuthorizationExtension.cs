using GustosApp.API.Middleware;

namespace GustosApp.API.Extensiones
{
    public static class AuthorizationExtension
    {
        public static IServiceCollection AgregarAutorizacion(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new RolVigenteRequirement(GustosApp.Domain.Model.@enum.RolUsuario.Admin));
                });

                options.AddPolicy("DuenoRestaurante", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new RolVigenteRequirement(GustosApp.Domain.Model.@enum.RolUsuario.DuenoRestaurante));
                });

                options.AddPolicy("PendienteRestaurante", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new RolVigenteRequirement(GustosApp.Domain.Model.@enum.RolUsuario.PendienteRestaurante));
                });
            });

            services.AddAuthorization(opt =>
            {
                opt.AddPolicy("RegistroIncompleto", p =>
                    p.Requirements.Add(new RegistroIncompletoRequirement()));
            });

            return services;
        }
    }
}
