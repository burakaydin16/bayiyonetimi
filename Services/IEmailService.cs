namespace MultiTenantSaaS.Services;

public interface IEmailService
{
    Task SendApprovalEmailAsync(string toEmail, string companyName, string companyId);
}
