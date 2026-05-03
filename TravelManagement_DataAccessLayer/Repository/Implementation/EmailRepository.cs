using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class EmailRepository : IEmailRepository
    {
        private readonly EmailSmtpSettings _settings;

        public EmailRepository(IOptions<EmailSmtpSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var mail = new MailMessage
            {
                From = new MailAddress(_settings.Username, "EzyGoa Taxi Services"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            mail.To.Add(to);

            using var smtp = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.UseSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            await smtp.SendMailAsync(mail);
        }
    }
}