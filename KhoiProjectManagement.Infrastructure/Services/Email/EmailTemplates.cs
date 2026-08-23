namespace KhoiProjectManagement.Infrastructure.Services
{
    // Shared branded HTML shell for every outbound email - keeps the visual design in one place
    // instead of duplicated inline markup per Send*EmailAsync method. Brand color (#0000D3) matches
    // the app's Tailwind blue-600 override in KhoiProjectManagementApp/tailwind.config.js.
    internal static class EmailTemplates
    {
        private const string BrandColor = "#0000D3";

        public static string Wrap(string headline, string bodyHtml, string? ctaText = null, string? ctaUrl = null)
        {
            var ctaHtml = string.Empty;
            if (!string.IsNullOrEmpty(ctaText) && !string.IsNullOrEmpty(ctaUrl))
            {
                ctaHtml = $@"
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin: 28px 0;"">
                    <tr>
                        <td style=""background-color: {BrandColor}; border-radius: 8px;"">
                            <a href=""{ctaUrl}"" target=""_blank"" style=""display: inline-block; padding: 12px 28px; color: #ffffff; font-family: Arial, Helvetica, sans-serif; font-size: 15px; font-weight: 600; text-decoration: none;"">{ctaText}</a>
                        </td>
                    </tr>
                </table>
                <p style=""font-family: Arial, Helvetica, sans-serif; font-size: 13px; color: #6b7280; word-break: break-all;"">
                    Or copy this link into your browser:<br>
                    <a href=""{ctaUrl}"" style=""color: {BrandColor};"">{ctaUrl}</a>
                </p>";
            }

            return $@"
<!DOCTYPE html>
<html>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: Arial, Helvetica, sans-serif;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f1f5f9; padding: 32px 16px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" style=""max-width: 560px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.08);"">
                    <tr>
                        <td style=""background-color: {BrandColor}; padding: 20px 32px;"">
                            <span style=""color: #ffffff; font-size: 18px; font-weight: 700; letter-spacing: 0.02em;"">Khoi Pro</span>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 32px;"">
                            <h2 style=""margin: 0 0 16px; color: #111827; font-size: 20px;"">{headline}</h2>
                            <div style=""color: #374151; font-size: 15px; line-height: 1.6;"">
                                {bodyHtml}
                            </div>
                            {ctaHtml}
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 32px; background-color: #f9fafb; border-top: 1px solid #e5e7eb;"">
                            <p style=""margin: 0; color: #9ca3af; font-size: 12px;"">Khoi Pro &mdash; Project Management System. This is an automated message, please do not reply.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
    }
}
