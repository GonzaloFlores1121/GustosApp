using GustosApp.API.Mapping;

namespace GustosApp.API.Tests;

public sealed class HorariosSolicitudPruebas
{
    [Fact]
    public void ConvertirHorarios_DelFormulario_ConservaDiaYCierreYDeclaracionDe24Horas()
    {
        var resultado = new HorariosJsonConverter().Convert(
            """[{"dia":"Lunes","todoElDia":true},{"dia":"Martes","cerrado":true}]""", null!);
        Assert.Equal("Lunes", resultado[0].Dia);
        Assert.True(resultado[0].TodoElDia);
        Assert.Equal("Martes", resultado[1].Dia);
        Assert.True(resultado[1].Cerrado);
    }
}
