using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Threading.Tasks;
using EventManagement.Models;
using EventManagement.Settings;
using EventManagement.Tasks;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using QRCoder;
using static QRCoder.PayloadGenerator;
namespace EventManagement.Tasks;

public class SendMailService : ISendMailService
{
    private readonly MailSettings mailSettings;

    private readonly ILogger<SendMailService> logger;


    // mailSetting được Inject qua dịch vụ hệ thống
    // Có inject Logger để xuất log
    public SendMailService(IOptions<MailSettings> _mailSettings, ILogger<SendMailService> _logger)
    {
        mailSettings = _mailSettings.Value;
        logger = _logger;
        logger.LogInformation("Create SendMailService");
    }

    // Gửi email, theo nội dung trong mailContent
    public async Task SendMail(MailContent mailContent)
    {
        var email = new MimeMessage();
        email.Sender = new MailboxAddress(mailSettings.DisplayName, mailSettings.Mail);
        email.From.Add(new MailboxAddress(mailSettings.DisplayName, mailSettings.Mail));
        email.To.Add(MailboxAddress.Parse(mailContent.To));
        email.Subject = mailContent.Subject;


        var builder = new BodyBuilder();
        builder.HtmlBody = mailContent.Body;
        email.Body = builder.ToMessageBody();

        // dùng SmtpClient của MailKit
        using var smtp = new MailKit.Net.Smtp.SmtpClient();

        try
        {
            smtp.Connect(mailSettings.Host, mailSettings.Port, SecureSocketOptions.StartTls);
            smtp.Authenticate(mailSettings.Mail, mailSettings.Password);
            await smtp.SendAsync(email);
        }
        catch (Exception ex)
        {
            // Gửi mail thất bại, nội dung email sẽ lưu vào thư mục mailssave
            System.IO.Directory.CreateDirectory("mailssave");
            var emailsavefile = string.Format(@"mailssave/{0}.eml", Guid.NewGuid());
            await email.WriteToAsync(emailsavefile);

            logger.LogInformation("Lỗi gửi mail, lưu tại - " + emailsavefile);
            logger.LogError(ex.Message);
        }

        smtp.Disconnect(true);

        logger.LogInformation("send mail to " + mailContent.To);

    }
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        await SendMail(new MailContent()
        {
            To = email,
            Subject = subject,
            Body = htmlMessage
        });
    }

    public async Task SendEmailWithQrCode(byte[] qrCodeData, string recipientEmail)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mailSettings.DisplayName, mailSettings.Mail));
        message.To.Add(new MailboxAddress("", recipientEmail));
        message.Subject = "Mã vé sự kiện";

        var builder = new BodyBuilder();
        builder.TextBody = "Mã QR của sự kiện";

        // Attach the QR Code image
        builder.Attachments.Add("QRCode.png", qrCodeData, ContentType.Parse("image/png"));

        message.Body = builder.ToMessageBody();

        using var smtp = new MailKit.Net.Smtp.SmtpClient();

        smtp.Connect(mailSettings.Host, mailSettings.Port, SecureSocketOptions.StartTls);
        smtp.Authenticate(mailSettings.Mail, mailSettings.Password);
        await smtp.SendAsync(message);
        smtp.Disconnect(true);
    }

    public static byte[] GenerateQrCode(string data)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        QRCode qrCode = new QRCode(qrCodeData);
        Bitmap qrCodeImage = qrCode.GetGraphic(20); // Kích thước của mã QR

        // Chuyển ảnh QR Code thành mảng byte
        using (MemoryStream stream = new MemoryStream())
        {
            qrCodeImage.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }
}