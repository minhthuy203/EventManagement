using EventManagement.Models;
using Org.BouncyCastle.Asn1.Pkcs;

namespace EventManagement.Tasks;

public interface ISendMailService
{
    Task SendMail(MailContent mailContent);

    Task SendEmailAsync(string email, string subject, string htmlMessage);

    Task SendEmailWithQrCode(byte[] qrCodeData, string recipientEmail);
}
