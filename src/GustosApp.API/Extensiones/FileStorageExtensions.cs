using GustosApp.Application.Interfaces;
using GustosApp.Infraestructure.Services;
using GustosApp.Infraestructure;

namespace GustosApp.API.Extensiones
{
    public static class FileStorageExtensions
    {
        public static IServiceCollection AgregarFileStorage(
        this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<FirebaseStorageService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<FirebaseStorageService>>();
                var firebaseJson = config["FIREBASE_SERVICE_ACCOUNT_JSON"];
                var localPath = Path.Combine(AppContext.BaseDirectory, "secrets/firebase-key.json");

                return new FirebaseStorageService(firebaseJson, localPath, config, logger,
                    sp.GetRequiredService<GustosDbContext>(),
                    sp.GetRequiredService<IServiceScopeFactory>());
            });
            services.AddScoped<IFileStorageService>(sp => sp.GetRequiredService<FirebaseStorageService>());
            services.AddHostedService<EliminacionesArchivoHostedService>();

            return services;
        }
    }
}
