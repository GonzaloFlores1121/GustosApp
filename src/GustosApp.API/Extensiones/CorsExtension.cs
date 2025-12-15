namespace GustosApp.API.Extensiones
{
    public static class CorsExtension
    {
        public const string CorsPolicyName = "GustosAppCors";

        public static IServiceCollection AgregarCors(this IServiceCollection services,
            IConfiguration config, IHostEnvironment env)
        {

            // Detectamos ambiente
            var isDevelopment = env.IsDevelopment();

            // Obtenemos origins del env (solo producción)
            var allowedOriginsString = config["CORS_ALLOWED_ORIGINS"];

            var allowedOrigins = allowedOriginsString?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray() ?? Array.Empty<string>();

            services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicyName, policy =>
                {
                    if (isDevelopment)
                    {
                        // LOCAL DEVELOPMENT
                        policy
                            .WithOrigins(
                                "http://localhost:3000",
                                "http://localhost:5174",
                                "https://lois-membranous-glancingly.ngrok-free.dev"
                            )
                            .AllowCredentials()
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                    else
                    {
                        // PRODUCTION
                        if (allowedOrigins.Length == 0)
                        {
                            throw new Exception("CORS_ALLOWED_ORIGINS no configurado en producción.");
                        }

                        policy
                            .WithOrigins(allowedOrigins)
                            .AllowCredentials()
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                });
            });

            return services;
        }
    }
}
