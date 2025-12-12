using GustosApp.Application.Interfaces;
using GustosApp.Infraestructure.Services;

namespace GustosApp.API.Extensiones
{
    public static class FileStorageExtensions
    {
        public static IServiceCollection AgregarFileStorage(
        this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<IFileStorageService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<FirebaseStorageService>>();
                var firebaseJson = config["FIREBASE_SERVICE_ACCOUNT_JSON"];
                var localPath = Path.Combine(AppContext.BaseDirectory, "secrets/firebase-key.json");

                return new FirebaseStorageService(firebaseJson, localPath, config, logger);
            });

            return services;
        }
    }
}
