using System.Net;
using System.Net.Mail;

namespace ClinicManagement.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var settings = _config.GetSection("EmailSettings");

            var smtp = new SmtpClient(settings["SmtpServer"])
            {
                Port = int.Parse(settings["Port"]),
                Credentials = new NetworkCredential(settings["Username"], settings["Password"]),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(settings["From"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            await smtp.SendMailAsync(message);
        }
    }
}
