using StackExchange.Redis;

using GustosApp.Application.Interfaces;
using GustosApp.Application.Services;
using GustosApp.Infraestructure.Services;

namespace GustosApp.API.Extensiones
{
    public static class RedisExtension
    {
        public static IServiceCollection AgregarRedisCache(this IServiceCollection services,
            IConfiguration config)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = config.GetConnectionString("Redis");
            });
            var redisConfig = config.GetConnectionString("Redis") ?? "localhost:6379";
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));
            services.AddSingleton<IAlmacenExplicaciones>(_ => new AlmacenExplicacionesRedis(redisConfig));
            services.AddSingleton<CacheExplicaciones>();
            return services;
        }
    }
}
