using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace GustosApp.API.Extensiones
{
    public static class FirebaseExtensions
    {
        public static IServiceCollection AgregarFirebaseAuth(this IServiceCollection services,
            IConfiguration config, IHostEnvironment env)
        {
     
            var firebaseLogger = services
            .BuildServiceProvider()
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Firebase");

            // Intentamos obtener JSON desde Azure (producción)
            var firebaseJson = config["FIREBASE_SERVICE_ACCOUNT_JSON"];

            // Project ID con fallback local
            var firebaseProjectId =
                config["FIREBASE_PROJECTID"]
                ?? "gustosapp-5c3c9";

            // Ruta local para desarrollo
            var localFirebasePath = Path.Combine(
               env.ContentRootPath,
                "secrets",
                "firebase-key.json"
            );

            firebaseLogger.LogInformation(
                "🔍 Iniciando configuración de Firebase (Entorno: {Env})",
                env.EnvironmentName
            );

            if (FirebaseApp.DefaultInstance == null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(firebaseJson))
                    {
                        firebaseLogger.LogInformation("🔥 Inicializando Firebase desde JSON (PRODUCCIÓN)");

                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = GoogleCredential.FromJson(firebaseJson)
                        });
                    }
                    else if (File.Exists(localFirebasePath))
                    {
                        firebaseLogger.LogInformation("💻 Inicializando Firebase desde archivo local: {Path}",
                            localFirebasePath);

                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = GoogleCredential.FromFile(localFirebasePath)
                        });
                    }
                    else
                    {
                        firebaseLogger.LogError("❌ No se encontró ninguna credencial de Firebase (ni JSON ni archivo)");
                        throw new Exception("No se pueden cargar credenciales Firebase.");
                    }

                    firebaseLogger.LogInformation("✅ Firebase inicializado correctamente.");
                }
                catch (Exception ex)
                {
                    firebaseLogger.LogError(ex, "❌ Error inicializando Firebase");
                    throw;
                }
            }
            return services;
        }
    }
}
