namespace MultiTenantSaaS.Services;

public interface IEmailService
{
    Task SendApprovalEmailAsync(string toEmail, string companyName, string companyId);
    Task SendNewRegistrationNotificationAsync(string companyName, string applicantEmail);
    Task SendEmailVerificationAsync(string toEmail, string companyName, string verificationLink);
    Task SendPasswordResetAsync(string toEmail, string resetLink);
}
