using System.Net;
using System.Net.Mail;

namespace MultiTenantSaaS.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendApprovalEmailAsync(string toEmail, string companyName, string companyId)
    {
        try
        {
            var host = _configuration["EmailSettings:Host"];
            if (string.IsNullOrEmpty(host))
            {
                _logger.LogWarning("EmailSettings:Host yapılandırılmadığı için {ToEmail} adresine e-posta gönderimi atlandı.", toEmail);
                return;
            }

            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? username;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "SuTakip Bayi Yönetimi"),
                Subject = "Üyeliğiniz Onaylandı - Sisteme Giriş Yapabilirsiniz",
                IsBodyHtml = true,
                Body = $@"
                    <div style='font-family: Arial, sans-serif; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #ddd; border-top: 5px solid #0071E3; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05);'>
                        <div style='padding: 30px;'>
                            <h2 style='color: #0071E3; margin-top: 0;'>Üyeliğiniz Onaylandı!</h2>
                            <p style='font-size: 16px; line-height: 1.5;'>Merhaba <strong>{companyName}</strong>,</p>
                            <p style='font-size: 16px; line-height: 1.5;'>Sistemimize yapmış olduğunuz üyelik başvurusu başarıyla onaylanmıştır! Artık size özel güvenli veritabanınız ve altyapınız hazırdır.</p>
                            
                            <div style='background-color: #f5f5f7; border: 1px solid #e5e5ea; padding: 20px; border-radius: 12px; margin: 30px 0; text-align: center;'>
                                <p style='font-size: 14px; color: #86868b; text-transform: uppercase; letter-spacing: 1px; margin: 0 0 10px 0;'>FİRMA İD BİLGİNİZ</p>
                                <h1 style='color: #1d1d1f; font-size: 36px; margin: 0; letter-spacing: 2px;'>{companyId}</h1>
                            </div>

                            <p style='font-size: 16px; line-height: 1.5;'>Giriş ekranında kendi kayıtlı e-posta ve şifrenizin yanı sıra yukarıdaki Firma ID bilginizi girerek sisteme erişim sağlayabilirsiniz.</p>
                            <p style='font-size: 16px; line-height: 1.5;'>Sistemi kullanmaya başlamak için hemen giriş yapabilirsiniz.</p>
                            
                            <p style='font-size: 16px; line-height: 1.5; margin-top: 30px;'>İyi çalışmalar dileriz,<br><strong>Sistem Yönetimi</strong></p>
                        </div>
                        <div style='background-color: #f5f5f7; padding: 15px; text-align: center; font-size: 12px; color: #86868b;'>
                            Bu e-posta otomatik olarak gönderilmiştir. Lütfen yanıtlamayınız.
                        </div>
                    </div>"
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Kayıt onay maili {ToEmail} adresine başarıyla gönderildi.", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ToEmail} adresine e-posta gönderimi başarısız oldu.", toEmail);
        }
    }

    public async Task SendNewRegistrationNotificationAsync(string companyName, string applicantEmail)
    {
        try
        {
            var host = _configuration["EmailSettings:Host"];
            if (string.IsNullOrEmpty(host))
            {
                _logger.LogWarning("EmailSettings:Host yapılandırılmadığı için yöneticiye yeni kayıt bildirimi atlandı.");
                return;
            }

            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? username;
            
            // Send to AdminEmail if configured, else fallback to FromEmail (which admin owns)
            var adminEmail = _configuration["EmailSettings:AdminEmail"] ?? fromEmail;
            var adminPanelUrl = _configuration["EmailSettings:AdminPanelUrl"] ?? "http://20.199.138.36/";

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "Sistem Bildirimi"),
                Subject = $"🚀 Yeni Firma Kayıt Talebi Geldi: {companyName}",
                IsBodyHtml = true,
                Body = $@"
                    <div style='font-family: Arial, sans-serif; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #ddd; border-top: 5px solid #F59E0B; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05);'>
                        <div style='padding: 30px;'>
                            <h2 style='color: #F59E0B; margin-top: 0;'>Yeni Üyelik Talebi Bizi Bekliyor!</h2>
                            <p style='font-size: 16px; line-height: 1.5;'>Merhaba Yönetici,</p>
                            <p style='font-size: 16px; line-height: 1.5;'>Sisteme yeni bir firma tarafından üyelik ve altyapı kurulum isteği gelmiştir. Gelen başvuruyu inceleyip onaylayabilirsiniz.</p>
                            
                            <div style='background-color: #f5f5f7; border: 1px solid #e5e5ea; padding: 20px; border-radius: 12px; margin: 30px 0;'>
                                <p style='font-size: 14px; margin: 0 0 10px 0;'><strong style='color: #86868b; text-transform: uppercase; letter-spacing: 1px; width: 150px; display: inline-block;'>Firma Adı:</strong> <span style='color: #1d1d1f; font-weight: bold;'>{companyName}</span></p>
                                <p style='font-size: 14px; margin: 0;'><strong style='color: #86868b; text-transform: uppercase; letter-spacing: 1px; width: 150px; display: inline-block;'>E-Posta:</strong> <span style='color: #1d1d1f; font-weight: bold;'>{applicantEmail}</span></p>
                            </div>

                            <div style='text-align: center; margin: 40px 0 20px 0;'>
                                <a href='{adminPanelUrl}' style='background-color: #0071E3; color: white; padding: 16px 32px; text-decoration: none; border-radius: 12px; font-weight: bold; font-size: 16px; display: inline-block; box-shadow: 0 4px 15px rgba(0, 113, 227, 0.4);'>
                                    Yönetim Paneline Git ve Onayla
                                </a>
                            </div>
                        </div>
                        <div style='background-color: #f5f5f7; padding: 15px; text-align: center; font-size: 12px; color: #86868b;'>
                            Modülleri ve yeni bayileri Super Admin hesabınız üzerinden yönetebilirsiniz.
                        </div>
                    </div>"
            };

            mailMessage.To.Add(adminEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Yeni üyelik talebi bildirimi yönetici ({AdminEmail}) adresine gönderildi.", adminEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yöneticiye üyelik talebi e-postası gönderilirken hata oluştu.");
        }
    }
}
