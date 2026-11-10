// Parsing for the "import members" box. Accepts what people actually paste from a spreadsheet or write by
// hand: one person per line, "email" or "email, role", separated by comma, semicolon or tab, with an
// optional header row. The server does the real validation (existing accounts only, role names, duplicates);
// this only turns text into rows.

const DELIMITERS = ['\t', ',', ';'];

const splitLine = (line) => {
  const delimiter = DELIMITERS.find((d) => line.includes(d));
  if (!delimiter) return [line.trim()];

  const cells = [];
  let current = '';
  let quoted = false;
  for (let i = 0; i < line.length; i += 1) {
    const ch = line[i];
    if (ch === '"') {
      if (quoted && line[i + 1] === '"') { current += '"'; i += 1; } else { quoted = !quoted; }
    } else if (ch === delimiter && !quoted) {
      cells.push(current.trim());
      current = '';
    } else {
      current += ch;
    }
  }
  cells.push(current.trim());
  return cells;
};

export const parseMemberRows = (text) => {
  const rows = String(text || '')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [email = '', role = ''] = splitLine(line);
      return { email, role };
    });

  // A header row ("email,role") is dropped; anything that looks like an address is kept.
  if (rows.length > 0 && !rows[0].email.includes('@') && /^(e-?mail|user|name|login)/i.test(rows[0].email)) rows.shift();
  return rows;
};

export const MEMBER_TEMPLATE_CSV = 'email,role\nsomeone@company.com,Developer\nanother@company.com,Scrum Master\n';

export const downloadText = (fileName, text, type = 'text/csv') => {
  const url = window.URL.createObjectURL(new Blob([text], { type }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
};

export const OUTCOME_STYLES = {
  Added: 'bg-green-100 text-green-700',
  Updated: 'bg-blue-100 text-blue-700',
  Unchanged: 'bg-gray-100 text-gray-600',
  NotFound: 'bg-red-100 text-red-700',
  Inactive: 'bg-red-100 text-red-700',
  InvalidEmail: 'bg-red-100 text-red-700',
  InvalidRole: 'bg-red-100 text-red-700',
  NoModuleAccess: 'bg-amber-100 text-amber-700',
  Duplicate: 'bg-gray-100 text-gray-600',
};

export const OUTCOME_LABELS = {
  Added: 'Add',
  Updated: 'Change role',
  Unchanged: 'No change',
  NotFound: 'No account',
  Inactive: 'Deactivated',
  InvalidEmail: 'Bad email',
  InvalidRole: 'Bad role',
  NoModuleAccess: 'No PM access',
  Duplicate: 'Duplicate',
};
