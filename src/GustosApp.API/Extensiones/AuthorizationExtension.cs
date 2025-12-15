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
                    policy.RequireClaim("rol", "Admin");
                });

                options.AddPolicy("DuenoRestaurante", policy =>
                {
                    policy.RequireClaim("rol", "DuenoRestaurante");
                });

                options.AddPolicy("PendienteRestaurante", policy =>
                {
                    policy.RequireClaim("rol", "PendienteRestaurante");
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
