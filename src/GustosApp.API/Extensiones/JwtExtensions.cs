using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GustosApp.API.Extensiones
{
    public static class JwtExtensions
    {
        public static IServiceCollection AgregarJwtAuthentication(this IServiceCollection services, 
            IConfiguration config)
        {
           var  firebaseProjectId= config["FIREBASE_PROJECTID"]
                ?? "gustosapp-5c3c9";

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
              .AddJwtBearer(options =>
      {
          options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";

          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuer = true,
              ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
              ValidateAudience = true,
              ValidAudience = firebaseProjectId,
              ValidateLifetime = true
          };

          options.Events = new JwtBearerEvents
          {
              OnMessageReceived = context =>
              {
                  var logger = context.HttpContext.RequestServices
                      .GetRequiredService<ILoggerFactory>()
                      .CreateLogger("JWT");

                  logger.LogInformation("Request a: {Path}", context.Request.Path);

                  // Prioridad 1: Cookie
                  if (context.Request.Cookies.TryGetValue("token", out var raw))
                  {
                      logger.LogInformation("Token encontrado en cookie.");
                      context.Token = raw;
                      return Task.CompletedTask;
                  }

                  // Prioridad 2: Query (SignalR)
                  var accessToken = context.Request.Query["access_token"];
                  if (!string.IsNullOrEmpty(accessToken))
                  {
                      logger.LogInformation("Token encontrado en QueryString (SignalR)");
                      context.Token = accessToken;
                      return Task.CompletedTask;
                  }

                  // Prioridad 3: Header Authorization
                  var authHeader = context.Request.Headers["Authorization"].ToString();
                  if (!string.IsNullOrEmpty(authHeader) &&
                      authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                  {
                      context.Token = authHeader.Substring("Bearer ".Length).Trim();
                      return Task.CompletedTask;
                  }

                  logger.LogWarning("No se recibió token por Cookie, QueryString ni Header.");
                  return Task.CompletedTask;
              },

              OnTokenValidated = context =>
              {
                  var logger = context.HttpContext.RequestServices
                      .GetRequiredService<ILoggerFactory>()
                      .CreateLogger("JWT");

                  logger.LogInformation("Token validado correctamente");

                  return Task.CompletedTask;
              },

              OnAuthenticationFailed = context =>
              {
                  var logger = context.HttpContext.RequestServices
                      .GetRequiredService<ILoggerFactory>()
                      .CreateLogger("JWT");

                  logger.LogError(context.Exception,
                      "Error en autenticación JWT: {Error}",
                      context.Exception.Message);

                  return Task.CompletedTask;
              }
          };
      });

            return services;
        }
    }
}
