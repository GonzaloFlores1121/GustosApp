using System.Net;
using System.Net.Mail;
using GustosApp.Application.Interfaces;
using GustosApp.Application.Common.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GustosApp.Infraestructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task EnviarEmailAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var smtpHost = _config["Email:SmtpHost"];
            if (string.IsNullOrWhiteSpace(smtpHost) || !int.TryParse(_config["Email:SmtpPort"], out var puerto)
                || puerto is < 1 or > 65535 || !MailAddress.TryCreate(_config["Email:From"], out var remitente)
                || !MailAddress.TryCreate(destinatario, out var destino))
                throw new CorreoNoIniciadoException();

            using var client = new SmtpClient(smtpHost, puerto)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_config["Email:SmtpUser"], _config["Email:SmtpPass"])
            };
            using var mail = new MailMessage(remitente, destino)
            {
                Subject = asunto,
                Body = cuerpoHtml,
                IsBodyHtml = true
            };

            try
            {
                await client.SendMailAsync(mail, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Resultado SMTP sin confirmar. Tipo: {Tipo}.", ex.GetType().Name);
                throw new ResultadoCorreoInciertoException();
            }
        }
    }

}
