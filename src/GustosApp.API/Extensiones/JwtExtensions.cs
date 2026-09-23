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
          options.Audience = firebaseProjectId;

          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuer = true,
              ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
              ValidateAudience = true,
              ValidAudience = firebaseProjectId,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true
          };

          options.Events = new JwtBearerEvents
          {
              OnMessageReceived = context =>
              {
                  var logger = context.HttpContext.RequestServices
                      .GetRequiredService<ILoggerFactory>()
                      .CreateLogger("JWT");

                  logger.LogInformation("Request a: {Path}", context.Request.Path);

                  context.Token = ObtenerTokenAutenticacion(context.Request);
                  if (!string.IsNullOrEmpty(context.Token))
                      return Task.CompletedTask;

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

                  logger.LogWarning(context.Exception, "Falló la autenticación JWT.");

                  return Task.CompletedTask;
              }
          };
      });

            return services;
        }

        internal static string? ObtenerTokenAutenticacion(HttpRequest request)
        {
            var authHeader = request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return LimpiarToken(authHeader["Bearer ".Length..]);
            }

            if (EsRutaSignalR(request.Path))
            {
                var accessToken = request.Query["access_token"].ToString();
                if (!string.IsNullOrWhiteSpace(accessToken))
                    return LimpiarToken(accessToken);
            }

            return request.Cookies.TryGetValue("token", out var cookieToken)
                ? LimpiarToken(cookieToken)
                : null;
        }

        private static bool EsRutaSignalR(PathString path) =>
            path.StartsWithSegments("/chatHub") ||
            path.StartsWithSegments("/notificacionesHub") ||
            path.StartsWithSegments("/solicitudesAmistadHub") ||
            path.StartsWithSegments("/votacionesHub");

        private static string? LimpiarToken(string token)
        {
            var tokenLimpio = token.Replace("\"", string.Empty).Trim();
            return string.IsNullOrEmpty(tokenLimpio) ? null : tokenLimpio;
        }
    }
}
