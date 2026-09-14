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

public class GustosAppApiFactory : WebApplicationFactory<Program>
{
    private static readonly object FirebaseLock = new();

    public GustosAppApiFactory()
    {
        lock (FirebaseLock)
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromAccessToken("integration-test-token"),
                    ProjectId = "gustosapp-integration-tests"
                });
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FIREBASE_PROJECTID"] = "gustosapp-integration-tests",
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=GustosAppTests;User Id=sa;Password=NotARealPassword1!;TrustServerCertificate=True",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["GeminiSettings:ApiKey"] = "integration-test-key",
                ["GeminiSettings:BaseUrl"] = "https://example.invalid"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

            services.RemoveAll<ICacheService>();
            services.AddSingleton(Mock.Of<ICacheService>());

            services.RemoveAll<IUsuarioRepository>();
            services.AddSingleton(Mock.Of<IUsuarioRepository>());
        });
    }
}
