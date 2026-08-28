namespace KhoiProjectManagement.Infrastructure.Services
{
    // Shared branded HTML shell for every outbound email - keeps the visual design in one place
    // instead of duplicated inline markup per Send*EmailAsync method. Brand color (#0000D3) matches
    // the app's Tailwind blue-600 override in KhoiProjectManagementApp/tailwind.config.js. Stays
    // table-based with fully inline styles throughout (no <style> block, no webfonts, no CSS Grid/
    // Flexbox) since Outlook's Word rendering engine ignores most of that - this is deliberately the
    // lowest-common-denominator approach every mainstream email client renders consistently.
    internal static class EmailTemplates
    {
        private const string BrandColor = "#0000D3";
        private const string BrandColorDark = "#00009e";
        private const string FontStack = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

        // appBaseUrl drives the small "Open Khoi Pro" footer link shown on every email regardless of
        // whether that particular email also has its own specific ctaUrl (e.g. a task-assignment email's
        // CTA jumps straight to the Tasks tab; the footer link is just "open the app" as a fallback).
        public static string Wrap(string headline, string bodyHtml, string? ctaText = null, string? ctaUrl = null, string? appBaseUrl = null)
        {
            var ctaHtml = string.Empty;
            if (!string.IsNullOrEmpty(ctaText) && !string.IsNullOrEmpty(ctaUrl))
            {
                ctaHtml = $@"
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin: 30px 0 8px;"">
                    <tr>
                        <td style=""background-color: {BrandColor}; border-radius: 10px;"">
                            <a href=""{ctaUrl}"" target=""_blank"" style=""display: inline-block; padding: 13px 30px; color: #ffffff; font-family: {FontStack}; font-size: 15px; font-weight: 600; text-decoration: none; border-radius: 10px;"">{ctaText} &rarr;</a>
                        </td>
                    </tr>
                </table>
                <p style=""font-family: {FontStack}; font-size: 12.5px; color: #9ca3af; word-break: break-all; margin: 8px 0 0;"">
                    Or copy this link: <a href=""{ctaUrl}"" style=""color: {BrandColor};"">{ctaUrl}</a>
                </p>";
            }

            var footerAppLink = string.IsNullOrEmpty(appBaseUrl)
                ? string.Empty
                : $@"<a href=""{appBaseUrl}"" style=""color: {BrandColorDark}; text-decoration: none; font-weight: 600;"">Open Khoi Pro</a> &middot; ";

            return $@"
<!DOCTYPE html>
<html>
<body style=""margin: 0; padding: 0; background-color: #eef1f6; font-family: {FontStack};"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #eef1f6; padding: 40px 16px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" style=""max-width: 580px; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 16px rgba(17,24,39,0.08);"">
                    <tr>
                        <td style=""background-color: {BrandColor}; padding: 24px 32px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td style=""background-color: rgba(255,255,255,0.16); border-radius: 8px; width: 32px; height: 32px; text-align: center; vertical-align: middle;"">
                                        <span style=""color: #ffffff; font-size: 16px; font-weight: 800; line-height: 32px;"">K</span>
                                    </td>
                                    <td style=""padding-left: 12px; vertical-align: middle;"">
                                        <span style=""color: #ffffff; font-size: 17px; font-weight: 700; letter-spacing: 0.01em;"">Khoi Pro</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 36px 32px 32px;"">
                            <h2 style=""margin: 0 0 18px; color: #111827; font-size: 21px; font-weight: 700; letter-spacing: -0.01em;"">{headline}</h2>
                            <div style=""color: #374151; font-size: 15px; line-height: 1.65;"">
                                {bodyHtml}
                            </div>
                            {ctaHtml}
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 18px 32px; background-color: #f9fafb; border-top: 1px solid #eef0f3;"">
                            <p style=""margin: 0; color: #9ca3af; font-size: 12px; line-height: 1.6;"">
                                {footerAppLink}Khoi Pro &mdash; Project Management System. This is an automated message, please do not reply.
                            </p>
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
