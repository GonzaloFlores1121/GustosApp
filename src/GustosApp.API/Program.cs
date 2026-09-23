using FluentValidation;
using FluentValidation.AspNetCore;
using GustosApp.API.Extensiones;
using GustosApp.API.Hubs;
using GustosApp.API.Hubs.GustosApp.API.Hubs;
using GustosApp.API.Middleware;
using GustosApp.API.Validations.OpinionRestaurantes;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Application.Validations.Restaurantes;
using GustosApp.Infraestructure;
using GustosApp.Infraestructure.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


// =====================
//   Firebase / Auth
// =====================
builder.Services.AgregarFirebaseAuth(builder.Configuration, builder.Environment);


// =====================
//   JWT Bearer
// =====================
builder.Services.AgregarJwtAuthentication(builder.Configuration);


// =====================
//      REDIS
// =====================
builder.Services.AgregarRedisCache(builder.Configuration);


// =====================
//      Automapper
// =====================
builder.Services.AddAutoMapper(cfg => {}, AppDomain.CurrentDomain.GetAssemblies());

// ===========================
//    Autorización explícita
// ===========================
builder.Services.AgregarAutorizacion();

builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("ReclamosRestaurante", contexto => RateLimitPartition.GetFixedWindowLimiter(
        ObtenerClaveCliente(contexto), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    opciones.AddPolicy("BusquedaRestaurantes", contexto => RateLimitPartition.GetFixedWindowLimiter(
        ObtenerClaveCliente(contexto), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

// =====================
//        CORS
// =====================

builder.Services.AgregarCors(builder.Configuration,builder.Environment);

// =====================
//    UseCases existentes
// =====================

builder.Services.AgregarApplicationUseCases();

// =====================
//     Repositorios / Servicios Infraestructura
// =====================

builder.Services.AgregarInfraestructura(builder.Configuration);


// =====================
//        OCR 
// =====================

builder.Services.AgregarOcrService(builder.Configuration);


// =====================
//       File Firebase Storage 
// =====================

builder.Services.AgregarFileStorage(builder.Configuration);


// =====================
//      Gemini
// =====================

builder.Services.AgregarGeminiIntegracion(builder.Configuration);


builder.Services.AddScoped<IEmbeddingService>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var modelPath = Path.Combine(AppContext.BaseDirectory, "ML", "model.onnx");
    var tokPath = Path.Combine(AppContext.BaseDirectory, "ML", "tokenizer.json");
    return new OnnxEmbeddingService(modelPath, tokPath);
});


//Fluent Validations
builder.Services
    .AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CrearSolicitudRestauranteValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CrearOpinionRestauranteValidator>();



// =====================
//    Controllers / JSON
// =====================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });



// =====================
//   EF Core / SQL Server
// =====================
builder.Services.AddDbContext<GustosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));



// Para notificaciones en tiempo real
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// =====================
//    Swagger
// =====================
builder.Services.AddHttpClient();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GustosApp API",
        Version = "v1"
    });

    // 🔐 Esquema Bearer (JWT) para botón Authorize
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegá tu idToken de Firebase con el prefijo 'Bearer '.\nEjemplo: Bearer eyJhbGciOi..."
    });

    // 🔒 Requisito global (aplica Bearer a todos los endpoints)
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// =====================
var culture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var app = builder.Build();


app.ValidateGeminiSettings();

// =====================
//   Pipeline HTTP
// =====================
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ManejadorErrorMiddleware>();

// CORS debe ir antes de UseRouting para SignalR
app.UseCors(CorsExtension.CorsPolicyName);

app.UseStaticFiles(); // Habilitar archivos estáticos

app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();


app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificacionesHub>("/notificacionesHub");
app.MapHub<SolicitudesAmistadHub>("/solicitudesAmistadHub");
app.MapHub<VotacionesHub>("/votacionesHub");

app.Run();

static string ObtenerClaveCliente(HttpContext contexto) =>
    contexto.User.FindFirst("user_id")?.Value
    ?? contexto.User.FindFirst("sub")?.Value
    ?? contexto.Connection.RemoteIpAddress?.ToString()
    ?? "anonimo";

public partial class Program;
