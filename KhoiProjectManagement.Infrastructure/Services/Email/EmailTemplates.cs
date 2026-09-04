namespace KhoiProjectManagement.Infrastructure.Services
{
    // Shared branded HTML shell for every outbound email - keeps the visual design in one place
    // instead of duplicated inline markup per Send*EmailAsync method. Brand color (#5D4AA4) matches
    // the app's Tailwind blue-600/primary-600 override in KhoiProjectManagementApp/tailwind.config.js.
    // Stays table-based with fully inline styles throughout (no <style> block, no webfonts, no CSS
    // Grid/Flexbox) since Outlook's Word rendering engine ignores most of that - this is deliberately
    // the lowest-common-denominator approach every mainstream email client renders consistently.
    internal static class EmailTemplates
    {
        private const string BrandColor = "#5D4AA4";
        private const string BrandColorDark = "#4B3A8C";
        private const string FontStack = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

        // Renders as a bordered table - label left/gray/uppercase, value right/dark, a thin divider
        // between rows (none after the last one). Used by templates that already have 1+ discrete
        // facts to show (a due date, a priority, an hours total) instead of burying them in prose.
        private static string BuildDetailRows(IEnumerable<(string Label, string Value)>? rows)
        {
            var rowList = rows?.ToList();
            if (rowList == null || rowList.Count == 0) return string.Empty;

            var rowsHtml = string.Join("", rowList.Select((row, index) =>
            {
                var borderStyle = index == rowList.Count - 1 ? "" : "border-bottom: 1px solid #eef0f3;";
                return $@"
                    <tr>
                        <td style=""padding: 10px 0; {borderStyle} width: 40%; color: #9ca3af; font-size: 11.5px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; vertical-align: top;"">{row.Label}</td>
                        <td style=""padding: 10px 0; {borderStyle} color: #111827; font-size: 14.5px; vertical-align: top;"">{row.Value}</td>
                    </tr>";
            }));

            return $@"
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin: 18px 0 4px; border-top: 1px solid #eef0f3;"">
                    {rowsHtml}
                </table>";
        }

        // appBaseUrl drives the small "Open KhoiHub" footer link shown on every email regardless of
        // whether that particular email also has its own specific ctaUrl (e.g. a task-assignment email's
        // CTA jumps straight to the Tasks tab; the footer link is just "open the app" as a fallback) -
        // it also builds the "Notification settings" link (appBaseUrl + "?tab=settings", where
        // NotificationPreferences.jsx renders - see App.jsx's activeTab === 'settings' block).
        public static string Wrap(string eyebrow, string headline, string bodyHtml, string? ctaText = null, string? ctaUrl = null, string? appBaseUrl = null, IEnumerable<(string Label, string Value)>? detailRows = null)
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
                    Or paste this into your browser: <a href=""{ctaUrl}"" style=""color: {BrandColor};"">{ctaUrl}</a>
                </p>";
            }

            var footerLinks = string.IsNullOrEmpty(appBaseUrl)
                ? string.Empty
                : $@"<p style=""margin: 0 0 4px;"">
                        <a href=""{appBaseUrl}"" style=""color: {BrandColorDark}; text-decoration: none; font-weight: 600;"">Open KhoiHub</a>
                        <span style=""color: #d1d5db;""> &middot; </span>
                        <a href=""{appBaseUrl.TrimEnd('/')}/?tab=settings"" style=""color: {BrandColorDark}; text-decoration: none; font-weight: 600;"">Notification settings</a>
                    </p>";

            return $@"
<!DOCTYPE html>
<html>
<body style=""margin: 0; padding: 0; background-color: #eef1f6; font-family: {FontStack};"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #eef1f6; padding: 40px 16px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" style=""max-width: 580px; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 16px rgba(17,24,39,0.08);"">
                    <tr>
                        <td style=""background-color: {BrandColor}; height: 6px; line-height: 6px; font-size: 0;"">&nbsp;</td>
                    </tr>
                    <tr>
                        <td style=""padding: 28px 32px 8px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td style=""background-color: {BrandColor}; border-radius: 8px; width: 32px; height: 32px; text-align: center; vertical-align: middle;"">
                                        <span style=""color: #ffffff; font-size: 16px; font-weight: 800; line-height: 32px;"">K</span>
                                    </td>
                                    <td style=""padding-left: 12px; vertical-align: middle;"">
                                        <span style=""color: #111827; font-size: 17px; font-weight: 700; letter-spacing: 0.01em;"">KhoiHub</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 8px 32px 32px;"">
                            <p style=""margin: 0 0 6px; color: {BrandColorDark}; font-size: 11.5px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em;"">{eyebrow}</p>
                            <h2 style=""margin: 0 0 18px; color: #111827; font-size: 21px; font-weight: 700; letter-spacing: -0.01em;"">{headline}</h2>
                            <div style=""color: #374151; font-size: 15px; line-height: 1.65;"">
                                {bodyHtml}
                            </div>
                            {BuildDetailRows(detailRows)}
                            {ctaHtml}
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 18px 32px; background-color: #f9fafb; border-top: 1px solid #eef0f3;"">
                            <p style=""margin: 0 0 4px; color: #374151; font-size: 12.5px; font-weight: 700;"">KhoiHub <span style=""color: #9ca3af; font-weight: 400;"">&middot; Project Management System</span></p>
                            {footerLinks}
                            <p style=""margin: 4px 0 0; color: #9ca3af; font-size: 12px; line-height: 1.6;"">Automated message &mdash; please do not reply.</p>
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
