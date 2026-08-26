using System.Text;
using System.Text.Json;

namespace KhoiProjectManagement.Application
{
    // One row extracted from an imported file, before validation/truncation - deliberately separate
    // from CreateVaultEntryDto since a row is allowed to be incomplete (missing Name/SecretValue get
    // skipped and reported by the caller, not thrown on mid-parse).
    internal class VaultImportRow
    {
        public string? Name { get; set; }
        public string? SystemOrUrl { get; set; }
        public string? Username { get; set; }
        public string? SecretValue { get; set; }
        public string? Notes { get; set; }
    }

    // Three formats, picked by file extension in VaultService.ImportEntriesAsync - deliberately no
    // external CSV/dotenv package (matches this repo's "no new dependency without a real need"
    // pattern elsewhere): a KEY=VALUE .env file and a name/secret CSV are simple enough to hand-roll
    // correctly, and System.Text.Json already covers JSON.
    internal static class VaultImportParser
    {
        // The fallback for anything that isn't .csv/.json - covers real .env files (KEY=VALUE) and
        // also a plain Notepad/Notepad++ .txt list jotted as "Label: value" per line, since that's
        // the realistic shape of a hand-typed secrets note. '#' comments and blank lines are skipped,
        // an optional leading "export " (shell-sourced .env variants) is stripped, and a value
        // wrapped in matching single/double quotes has them stripped. Whichever of '=' or ':' appears
        // first on the line is the separator - a line with neither is ignored rather than guessed at.
        // Like a real .env, there's no concept of username/system/notes here, so those stay null.
        public static List<VaultImportRow> ParseEnv(string content)
        {
            var rows = new List<VaultImportRow>();
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim().TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) line = line[7..].TrimStart();

                var (key, value) = SplitKeyValue(line);
                if (key == null) continue;

                rows.Add(new VaultImportRow { Name = key, SecretValue = StripQuotes(value!) });
            }
            return rows;
        }

        private static (string? Key, string? Value) SplitKeyValue(string line)
        {
            var eqIndex = line.IndexOf('=');
            var colonIndex = line.IndexOf(':');

            int sepIndex;
            if (eqIndex < 0 && colonIndex < 0) return (null, null);
            if (eqIndex < 0) sepIndex = colonIndex;
            else if (colonIndex < 0) sepIndex = eqIndex;
            else sepIndex = Math.Min(eqIndex, colonIndex);

            if (sepIndex <= 0) return (null, null);

            return (line[..sepIndex].Trim(), line[(sepIndex + 1)..].Trim());
        }

        // Header-based, with flexible column-name aliases so a "password" or "value" column works
        // the same as "secret" - most spreadsheet exports use one of those interchangeably. Not a full
        // RFC4180 parser (a quoted field may not contain a literal newline), which real-world secret
        // exports essentially never do; quoted commas and escaped "" quotes inside a field do work.
        public static List<VaultImportRow> ParseCsv(string content)
        {
            var rows = new List<VaultImportRow>();
            var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) return rows;

            var header = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
            int IndexOfAny(params string[] names) => header.FindIndex(names.Contains);

            var nameIdx = IndexOfAny("name", "title");
            var systemIdx = IndexOfAny("systemorurl", "system_or_url", "system", "url");
            var userIdx = IndexOfAny("username", "user");
            var secretIdx = IndexOfAny("secret", "secretvalue", "value", "password");
            var notesIdx = IndexOfAny("notes", "note", "description");

            if (nameIdx < 0 || secretIdx < 0)
                throw new InvalidOperationException("CSV must have a \"name\" column and a \"secret\" (or \"password\"/\"value\") column.");

            for (var i = 1; i < lines.Count; i++)
            {
                var fields = SplitCsvLine(lines[i]);
                string? Get(int idx) => idx >= 0 && idx < fields.Count ? fields[idx].Trim() : null;

                rows.Add(new VaultImportRow
                {
                    Name = Get(nameIdx),
                    SystemOrUrl = Get(systemIdx),
                    Username = Get(userIdx),
                    SecretValue = Get(secretIdx),
                    Notes = Get(notesIdx),
                });
            }
            return rows;
        }

        // Either an array of {name, systemOrUrl/system/url, username/user, secret/secretValue/value/
        // password, notes/note} objects (property names matched case-insensitively), or a flat
        // { "KEY": "value" } object treated the same way as a .env file.
        public static List<VaultImportRow> ParseJson(string content)
        {
            var rows = new List<VaultImportRow>();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    rows.Add(new VaultImportRow
                    {
                        Name = GetProp(item, "name", "title"),
                        SystemOrUrl = GetProp(item, "systemOrUrl", "system", "url"),
                        Username = GetProp(item, "username", "user"),
                        SecretValue = GetProp(item, "secret", "secretValue", "value", "password"),
                        Notes = GetProp(item, "notes", "note"),
                    });
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        rows.Add(new VaultImportRow { Name = prop.Name, SecretValue = prop.Value.GetString() });
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("JSON must be an array of entries or a flat {\"name\": \"secret\"} object.");
            }
            return rows;
        }

        private static string? GetProp(JsonElement obj, params string[] names)
        {
            foreach (var p in obj.EnumerateObject())
            {
                if (names.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    return p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString(),
                        JsonValueKind.Null => null,
                        _ => p.Value.ToString(),
                    };
                }
            }
            return null;
        }

        private static string StripQuotes(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                return value[1..^1];
            }
            return value;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }
    }
}
