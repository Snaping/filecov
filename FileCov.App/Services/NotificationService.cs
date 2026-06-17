using System.Diagnostics;
using System.IO;
using MailKit.Net.Smtp;
using MimeKit;

namespace FileCov.App.Services;

public class NotificationService
{
    public bool AutoOpenFolder { get; set; } = true;
    public EmailSettings EmailConfig { get; set; } = new();

    public void OpenFolder(string path)
    {
        if (AutoOpenFolder && Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
    }

    public async Task SendEmailAsync(string to, string subject, string body, string smtpHost, int smtpPort, string from, string password)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("FileCov", from));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { TextBody = body };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(from, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public class EmailSettings
    {
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string From { get; set; } = "";
        public string Password { get; set; } = "";
        public string To { get; set; } = "";
        public bool Enabled { get; set; }
    }
}
