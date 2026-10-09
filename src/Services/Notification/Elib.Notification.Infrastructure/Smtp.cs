using System.Net.Sockets;
using Elib.Notification.Application;
using Elib.Notification.Domain;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Elib.Notification.Infrastructure;

/// <summary>Gửi thư bằng MailKit — mỗi thư một kết nối (lưu lượng GĐ0 thấp; gom kết nối khi có gửi hàng loạt).</summary>
public sealed class MailKitSmtpSender(IOptions<NotificationOptions> options) : ISmtpSender
{
    public async Task SendAsync(SmtpEndpoint endpoint, OutgoingEmail email, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(endpoint.FromName ?? "", endpoint.FromAddress));
        message.To.Add(new MailboxAddress(email.ToName ?? "", email.ToAddress));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { HtmlBody = email.HtmlBody }.ToMessageBody();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.SendTimeout);
        using var client = new SmtpClient { Timeout = (int)options.Value.SendTimeout.TotalMilliseconds };
        try
        {
            await client.ConnectAsync(endpoint.Host, endpoint.Port, ToMailKit(endpoint.Security), timeout.Token);
            if (!string.IsNullOrEmpty(endpoint.UserName))
                await client.AuthenticateAsync(endpoint.UserName, endpoint.Password ?? "", timeout.Token);
            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SmtpDeliveryException($"Quá thời gian kết nối máy chủ SMTP {endpoint.Host}:{endpoint.Port}.");
        }
        catch (Exception ex) when (ex is SocketException or IOException or SslHandshakeException or System.Security.Authentication.AuthenticationException
                                       or MailKit.Security.AuthenticationException or SmtpCommandException or SmtpProtocolException
                                       or ServiceNotConnectedException or ServiceNotAuthenticatedException)
        {
            throw new SmtpDeliveryException(Describe(ex, endpoint), ex);
        }
    }

    private static SecureSocketOptions ToMailKit(SmtpSecurity security) => security switch
    {
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        _ => SecureSocketOptions.None,
    };

    private static string Describe(Exception ex, SmtpEndpoint endpoint) => ex switch
    {
        MailKit.Security.AuthenticationException => "Máy chủ SMTP từ chối tên đăng nhập hoặc mật khẩu.",
        SslHandshakeException => "Không thiết lập được kết nối TLS — kiểm tra cổng và kiểu mã hoá.",
        SocketException => $"Không kết nối được máy chủ SMTP {endpoint.Host}:{endpoint.Port}.",
        SmtpCommandException c => $"Máy chủ SMTP từ chối thư ({(int)c.StatusCode}): {c.Message}",
        _ => $"Lỗi gửi thư: {ex.Message}",
    };
}

/// <summary>Mã hoá mật khẩu SMTP bằng Data Protection (khoá lưu trong bảng data_protection_keys của service).</summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Elib.Notification.SmtpPassword.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
