using System.Text.Json;
using GustosApp.API.DTO;

namespace GustosApp.API.Tests;

public class ActualizarRestauranteDashboardRequestTests
{
    [Fact]
    public void WebUrlOmitida_SeDistingueDeNullExplicito()
    {
        var omitida = JsonSerializer.Deserialize<ActualizarRestauranteDashboardRequest>("{}");
        var explicita = JsonSerializer.Deserialize<ActualizarRestauranteDashboardRequest>("{\"webUrl\":null}");

        Assert.NotNull(omitida);
        Assert.False(omitida.WebUrlFueEnviado);
        Assert.NotNull(explicita);
        Assert.True(explicita.WebUrlFueEnviado);
        Assert.Null(explicita.WebUrl);
    }
}
