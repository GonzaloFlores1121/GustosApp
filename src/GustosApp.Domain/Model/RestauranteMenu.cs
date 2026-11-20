using System;
using GustosApp.Domain.Model.@enum;

namespace GustosApp.Domain.Model
{
    public class RestauranteMenu
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RestauranteId { get; set; }
        public Restaurante Restaurante { get; set; } = null!;

        public string Moneda { get; set; } = "ARS";
        public string Json { get; set; } = "{}";
        public int Version { get; set; } = 1;
        public DateTime FechaActualizacionUtc { get; set; } = DateTime.UtcNow;

        public string? ImagenFuentePublicadaUrl { get; private set; }
        public Common.OrigenImagenRestaurante OrigenImagenPublicada { get; private set; } = Common.OrigenImagenRestaurante.Desconocida;
        public Common.OrigenImagenRestaurante OrigenImagenBorrador { get; private set; } = Common.OrigenImagenRestaurante.Desconocida;
        public Guid? BorradorToken { get; private set; }
        public string? BorradorJson { get; private set; }
        public string? BorradorImagenFuenteUrl { get; private set; }
        public EstadoBorradorMenu EstadoBorrador { get; private set; } = EstadoBorradorMenu.Ninguno;
        public string? BorradorError { get; private set; }
        public DateTime? FechaBorradorUtc { get; private set; }

        public void IniciarBorrador(Guid token, string imagenFuenteUrl, DateTime fechaUtc, Common.OrigenImagenRestaurante origen = Common.OrigenImagenRestaurante.Desconocida)
        {
            BorradorToken = token;
            BorradorJson = null;
            BorradorImagenFuenteUrl = imagenFuenteUrl;
            OrigenImagenBorrador = origen;
            EstadoBorrador = EstadoBorradorMenu.Procesando;
            BorradorError = null;
            FechaBorradorUtc = fechaUtc;
        }

        public bool CompletarBorrador(Guid token, string json, DateTime fechaUtc)
        {
            if (BorradorToken != token) return false;
            BorradorJson = json;
            EstadoBorrador = EstadoBorradorMenu.Listo;
            BorradorError = null;
            FechaBorradorUtc = fechaUtc;
            return true;
        }

        public bool MarcarErrorBorrador(Guid token, string mensaje, DateTime fechaUtc)
        {
            if (BorradorToken != token) return false;
            BorradorJson = null;
            EstadoBorrador = EstadoBorradorMenu.Error;
            BorradorError = string.IsNullOrWhiteSpace(mensaje)
                ? "No pudimos leer el menú. Revisá la imagen e intentá nuevamente."
                : mensaje;
            FechaBorradorUtc = fechaUtc;
            return true;
        }

        public string? PublicarBorrador(Guid token, string jsonRevisado, DateTime fechaUtc)
        {
            if (BorradorToken != token || EstadoBorrador != EstadoBorradorMenu.Listo)
                throw new InvalidOperationException("El borrador cambió. Volvé a cargarlo antes de publicar.");

            var imagenAnterior = ImagenFuentePublicadaUrl;
            Json = jsonRevisado;
            Moneda = "ARS";
            Version = Math.Max(1, Version + 1);
            FechaActualizacionUtc = fechaUtc;
            ImagenFuentePublicadaUrl = BorradorImagenFuenteUrl;
            OrigenImagenPublicada = OrigenImagenBorrador;
            LimpiarBorrador();
            return imagenAnterior;
        }

        public string? DescartarBorrador(Guid token)
        {
            if (BorradorToken != token)
                throw new InvalidOperationException("El borrador cambió. Actualizá la pantalla antes de descartarlo.");
            var imagen = BorradorImagenFuenteUrl;
            LimpiarBorrador();
            return imagen;
        }

        private void LimpiarBorrador()
        {
            BorradorToken = null;
            BorradorJson = null;
            BorradorImagenFuenteUrl = null;
            OrigenImagenBorrador = Common.OrigenImagenRestaurante.Desconocida;
            EstadoBorrador = EstadoBorradorMenu.Ninguno;
            BorradorError = null;
            FechaBorradorUtc = null;
        }
    }
}
