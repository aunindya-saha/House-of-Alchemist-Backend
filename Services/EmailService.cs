using SendGrid;
using SendGrid.Helpers.Mail;

namespace HouseOfAlchemists.API.Services;

public class EmailService(IConfiguration config, ILogger<EmailService> logger)
{
    public async Task SendCommentReplyAsync(string toEmail, string toName, string question, string reply)
    {
        var apiKey = config["SendGrid:ApiKey"];
        if (string.IsNullOrEmpty(apiKey)) { logger.LogWarning("SendGrid not configured — email skipped"); return; }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(config["SendGrid:FromEmail"], config["SendGrid:FromName"]);
        var html = $"""
            <h3>Your question has been answered!</h3>
            <p><strong>Your question:</strong></p>
            <blockquote>{System.Net.WebUtility.HtmlEncode(question)}</blockquote>
            <p><strong>Teacher's reply:</strong></p>
            <blockquote>{System.Net.WebUtility.HtmlEncode(reply)}</blockquote>
            """;
        var msg = MailHelper.CreateSingleEmail(from, new EmailAddress(toEmail, toName),
            "Your question was answered — House of Alchemists", null, html);
        try { await client.SendEmailAsync(msg); }
        catch (Exception ex) { logger.LogWarning("Email failed: {Ex}", ex.Message); }
    }

    public async Task SendTempPasswordAsync(string toEmail, string toName, string tempPassword)
    {
        var apiKey = config["SendGrid:ApiKey"];
        if (string.IsNullOrEmpty(apiKey)) return;

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(config["SendGrid:FromEmail"], config["SendGrid:FromName"]);
        var html = $"""
            <h3>Welcome to House of Alchemists, {System.Net.WebUtility.HtmlEncode(toName)}!</h3>
            <p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(toEmail)}</p>
            <p><strong>Temporary password:</strong> <code>{System.Net.WebUtility.HtmlEncode(tempPassword)}</code></p>
            <p>You will be asked to change your password on first login.</p>
            """;
        var msg = MailHelper.CreateSingleEmail(from, new EmailAddress(toEmail, toName),
            "Your teacher account — House of Alchemists", null, html);
        try { await client.SendEmailAsync(msg); }
        catch (Exception ex) { logger.LogWarning("Email failed: {Ex}", ex.Message); }
    }
}
