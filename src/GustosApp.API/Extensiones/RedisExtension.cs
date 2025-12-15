using StackExchange.Redis;

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
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IConnectionMultiplexer>>();
                var redisConfig = config.GetConnectionString("Redis") ?? "localhost:6379";

                logger.LogInformation("🚀 Conectando a Redis con: {RedisConnectionString}", redisConfig);

                return ConnectionMultiplexer.Connect(redisConfig);
            });
            return services;
        }
    }
}
