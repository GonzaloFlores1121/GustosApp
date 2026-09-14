using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using GustosApp.Application.Interfaces;
using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace GustosApp.API.Tests;

public class FabricaApiGustosApp : WebApplicationFactory<Program>
{
    private static readonly object BloqueoFirebase = new();

    public FabricaApiGustosApp()
    {
        lock (BloqueoFirebase)
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromAccessToken("token-pruebas-integracion"),
                    ProjectId = "gustosapp-pruebas-integracion"
                });
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Development");
        constructor.ConfigureLogging(registro =>
        {
            registro.ClearProviders();
            registro.AddConsole();
        });
        constructor.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FIREBASE_PROJECTID"] = "gustosapp-pruebas-integracion",
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=GustosAppTests;User Id=sa;Password=NotARealPassword1!;TrustServerCertificate=True",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["GeminiSettings:ApiKey"] = "clave-pruebas-integracion",
                ["GeminiSettings:BaseUrl"] = "https://example.invalid"
            });
        });
        constructor.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IDataProtectionProvider>();
            servicios.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

            servicios.RemoveAll<ICacheService>();
            servicios.AddSingleton(Mock.Of<ICacheService>());

            servicios.RemoveAll<IUsuarioRepository>();
            servicios.AddSingleton(Mock.Of<IUsuarioRepository>());
        });
    }
}
