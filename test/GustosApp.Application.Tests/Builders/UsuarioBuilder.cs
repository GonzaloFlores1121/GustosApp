using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GustosApp.Domain.Model;

namespace GustosApp.Application.Tests.Builders
{
    public class UsuarioBuilder
    {
        private Guid _id = Guid.NewGuid();
        private string _firebaseUid = "test-uid";
        private List<Gusto> _gustos = new();
        private List<Restriccion> _restricciones = new();
        private List<CondicionMedica> _condiciones = new();

        public UsuarioBuilder ConId(Guid id)
        {
            _id = id;
            return this;
        }

        public UsuarioBuilder ConFirebaseUid(string uid)
        {
            _firebaseUid = uid;
            return this;
        }

        public UsuarioBuilder ConGustos(params string[] gustos)
        {
            _gustos = gustos.Select(g => new Gusto { Nombre = g }).ToList();
            return this;
        }

        public UsuarioBuilder ConRestricciones(params string[] restricciones)
        {
            _restricciones = restricciones.Select(r => new Restriccion { Nombre = r }).ToList();
            return this;
        }

        public UsuarioBuilder ConCondiciones(params string[] condiciones)
        {
            _condiciones = condiciones.Select(c => new CondicionMedica { Nombre = c }).ToList();
            return this;
        }

        public Usuario Build()
        {
            return new Usuario
            {
                Id = _id,
                FirebaseUid = _firebaseUid,
                Gustos = _gustos,
                Restricciones = _restricciones,
                CondicionesMedicas = _condiciones
            };
        }
    }

}
