using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GustosApp.Domain.Common
{
    public class UsuarioPreferencias
    {
        public List<string> Gustos { get; set; } = new();
        public List<string> Restricciones { get; set; } = new();
        public List<string> CondicionesMedicas { get; set; } = new();
        // Las etiquetas provienen de las relaciones del catálogo, no del nombre visible.
        public List<ReglaCompatibilidadPreferencia> ReglasCompatibilidad { get; set; } = new();
    }

    public sealed record ReglaCompatibilidadPreferencia(
        Guid Id,
        bool EsRestriccion,
        IReadOnlyCollection<string> TagsCriticos);

}
