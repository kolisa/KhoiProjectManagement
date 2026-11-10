// Helpers for work item descriptions, which are rich text (a small, safe subset of HTML). The server sanitises
// everything it stores; this file does it again on the way to the screen (defence in depth - a description can also
// arrive from the classic Tasks tab as plain text) and holds the writing aids: tidy (spelling/grammar) and subtask
// suggestions. Pure functions over the DOM, no network.

const ALLOWED_TAGS = new Set(['P', 'BR', 'STRONG', 'B', 'EM', 'I', 'U', 'S', 'H1', 'H2', 'H3', 'UL', 'OL', 'LI', 'BLOCKQUOTE', 'CODE', 'PRE', 'A', 'HR', 'DIV', 'SPAN']);
// Removed together with everything inside them (unwrapping would leave script text behind).
const DROP_WITH_CONTENT = new Set(['SCRIPT', 'STYLE', 'IFRAME', 'OBJECT', 'EMBED', 'NOSCRIPT', 'TEMPLATE', 'SVG', 'MATH', 'TEXTAREA', 'SELECT']);
const BLOCKS = new Set(['P', 'DIV', 'LI', 'H1', 'H2', 'H3', 'BLOCKQUOTE', 'PRE', 'UL', 'OL']);

const escapeHtml = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

export const looksLikeHtml = (s) => /<\/?[a-z][\s\S]*?>/i.test(s || '');

const parse = (html) => new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html').body;

const cleanChildren = (parent) => {
  Array.from(parent.childNodes).forEach((node) => {
    if (node.nodeType === 3) return;               // text
    if (node.nodeType !== 1) { node.remove(); return; }
    const tag = node.tagName.toUpperCase();
    if (DROP_WITH_CONTENT.has(tag)) { node.remove(); return; }
    cleanChildren(node);
    if (!ALLOWED_TAGS.has(tag)) {                  // unknown wrapper: keep its (already cleaned) content
      node.replaceWith(...Array.from(node.childNodes));
      return;
    }
    const href = tag === 'A' ? node.getAttribute('href') : null;
    Array.from(node.attributes).forEach((a) => node.removeAttribute(a.name));
    if (tag === 'A') {
      if (href && /^(https?:|mailto:)/i.test(href.trim())) {
        node.setAttribute('href', href.trim());
        node.setAttribute('target', '_blank');
        node.setAttribute('rel', 'noopener noreferrer');
      }
    }
  });
};

// Allow-list sanitiser: only the formatting the editor can produce survives.
export const sanitizeHtml = (html) => {
  if (!html) return '';
  const body = parse(html);
  cleanChildren(body);
  return body.innerHTML.trim();
};

// What the editor (and the read-only view) should show for a stored description: legacy plain text becomes paragraphs.
export const toEditorHtml = (stored) => {
  if (!stored || !stored.trim()) return '';
  if (looksLikeHtml(stored)) return sanitizeHtml(stored);
  return stored.trim().split(/\n{2,}/).map((p) => `<p>${escapeHtml(p).replace(/\n/g, '<br>')}</p>`).join('');
};

export const htmlToText = (html) => {
  if (!html) return '';
  if (!looksLikeHtml(html)) return html;
  const spaced = html.replace(/<\/(p|div|li|h[1-6]|blockquote|pre)>/gi, '\n').replace(/<br\s*\/?>/gi, '\n');
  return (parse(spaced).textContent || '').replace(/\n{3,}/g, '\n\n').trim();
};

export const isEmptyHtml = (html) => htmlToText(html).trim() === '';

// ---------------------------------------------------------------------------------------------- tidy
// Common misspellings (lower case -> correction). Deliberately a short, conservative list: a wrong "fix" is worse than a
// missed one. The browser's own spell-checker still underlines everything else while typing.
const TYPOS = {
  teh: 'the', adn: 'and', taht: 'that', wich: 'which', whcih: 'which', thier: 'their', recieve: 'receive', recieved: 'received',
  seperate: 'separate', seperately: 'separately', definately: 'definitely', definatly: 'definitely', occured: 'occurred', occurence: 'occurrence',
  adress: 'address', becuase: 'because', becasue: 'because', beleive: 'believe', untill: 'until', wierd: 'weird', accomodate: 'accommodate',
  acheive: 'achieve', tommorow: 'tomorrow', tomorow: 'tomorrow', enviroment: 'environment', sucessful: 'successful', succesful: 'successful',
  sucessfully: 'successfully', refered: 'referred', neccessary: 'necessary', necesary: 'necessary', existance: 'existence', goverment: 'government',
  calender: 'calendar', arguement: 'argument', maintainance: 'maintenance', independant: 'independent', dependecy: 'dependency', dependancy: 'dependency',
  dependencys: 'dependencies', managment: 'management', developement: 'development', requirment: 'requirement', requirments: 'requirements',
  implemention: 'implementation', fucntion: 'function', fuction: 'function', paramter: 'parameter', paramters: 'parameters', databse: 'database',
  cofiguration: 'configuration', configration: 'configuration', authetication: 'authentication', authentification: 'authentication',
  documention: 'documentation', documenation: 'documentation', deploment: 'deployment', recomend: 'recommend', reccomend: 'recommend',
  occuring: 'occurring', begining: 'beginning', writting: 'writing', alot: 'a lot', thru: 'through', abit: 'a bit',
};

const matchCase = (word, fix) => {
  if (word === word.toUpperCase() && word.length > 1) return fix.toUpperCase();
  if (word[0] === word[0].toUpperCase()) return fix[0].toUpperCase() + fix.slice(1);
  return fix;
};

const SKIP_PARENTS = new Set(['CODE', 'PRE', 'A']);

const nearestBlock = (node, root) => {
  let n = node.parentNode;
  while (n && n !== root) { if (BLOCKS.has(n.tagName)) return n; n = n.parentNode; }
  return root;
};

// Tidies the text of an HTML fragment: spelling of common words, repeated words, spacing around punctuation,
// a stray lower-case "i", and capital letters at the start of sentences. Never touches code, preformatted text or links.
// Returns the new HTML and a human-readable list of what changed, so nothing happens silently.
export const tidyText = (html) => {
  const body = parse(html || '');
  const counts = { spelling: 0, repeated: 0, spacing: 0, capitals: 0 };
  const seenBlock = new Set();
  const walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
  const nodes = [];
  while (walker.nextNode()) nodes.push(walker.currentNode);

  nodes.forEach((node) => {
    for (let p = node.parentNode; p && p !== body; p = p.parentNode) if (SKIP_PARENTS.has(p.tagName)) return;
    const original = node.nodeValue;
    let text = original;
    if (!text.trim()) return;

    // Misspellings (whole words only).
    text = text.replace(/\b[A-Za-z]+\b/g, (w) => {
      const fix = TYPOS[w.toLowerCase()];
      if (!fix) return w;
      counts.spelling += 1;
      return matchCase(w, fix);
    });
    // "the the" -> "the"
    text = text.replace(/\b([A-Za-z]+)(\s+)\1\b/gi, (m, w) => { counts.repeated += 1; return w; });
    // A lone "i" is the pronoun: "i think", "i'm".
    text = text.replace(/\bi\b(?=(\s|'|$))/g, () => { counts.capitals += 1; return 'I'; });
    // Spacing: runs of spaces, space before , . ; : ! ?, missing space after them.
    text = text.replace(/ {2,}/g, () => { counts.spacing += 1; return ' '; });
    text = text.replace(/ +([,.;:!?])/g, (m, p) => { counts.spacing += 1; return p; });
    text = text.replace(/([,;:!?])(?=[A-Za-z])/g, (m, p) => { counts.spacing += 1; return `${p} `; });
    text = text.replace(/([a-z]{2,}[.])(?=[A-Z][a-z])/g, (m) => { counts.spacing += 1; return `${m} `; });
    // Capital after . ! ? and at the start of a block.
    text = text.replace(/([.!?]\s+)([a-z])/g, (m, lead, c) => { counts.capitals += 1; return lead + c.toUpperCase(); });
    const block = nearestBlock(node, body);
    if (!seenBlock.has(block)) {
      seenBlock.add(block);
      text = text.replace(/^(\s*)([a-z])/, (m, lead, c) => { counts.capitals += 1; return lead + c.toUpperCase(); });
    }
    if (text !== original) node.nodeValue = text;
  });

  const changes = [];
  const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;
  if (counts.spelling) changes.push(`Fixed ${plural(counts.spelling, 'misspelled word', 'misspelled words')}`);
  if (counts.repeated) changes.push(`Removed ${plural(counts.repeated, 'repeated word', 'repeated words')}`);
  if (counts.capitals) changes.push(`Fixed ${plural(counts.capitals, 'capital letter', 'capital letters')}`);
  if (counts.spacing) changes.push(`Fixed spacing in ${plural(counts.spacing, 'place', 'places')}`);
  return { html: body.innerHTML, changes };
};

// ---------------------------------------------------------------------------------------------- subtask suggestions
const ACTION_START = /^(?:(?:also|then|and|finally|next|first|lastly),?\s+)?(?:we\s+(?:need|have|must|should)\s+to\s+|need(?:s)?\s+to\s+|should\s+|must\s+|has\s+to\s+|have\s+to\s+|will\s+need\s+to\s+|to\s+)?(add|create|build|implement|update|fix|migrate|write|test|deploy|document|design|review|configure|set up|setup|refactor|remove|integrate|validate|investigate|analy[sz]e|prepare|release|monitor|train|demo|plan|define)\b/i;
const MAX_TITLE = 120;
const MAX_SUGGESTIONS = 12;

const tidyTitle = (raw) => {
  let t = raw.replace(/^\s*(?:[-*•–]|\d+[.)]|\[[ xX]?\])\s*/, '').replace(/\s+/g, ' ').trim().replace(/[.;:,]+$/, '');
  t = t.replace(/^(?:(?:also|then|and|finally|next|first|lastly),?\s+)?(?:we\s+(?:need|have|must|should)\s+to|need(?:s)?\s+to|should|must|has\s+to|have\s+to|will\s+need\s+to|to)\s+/i, '');
  if (!t) return '';
  t = t[0].toUpperCase() + t.slice(1);
  return t.length > MAX_TITLE ? `${t.slice(0, MAX_TITLE - 1).trimEnd()}…` : t;
};

// Proposes subtasks from a description: list items first (what people write when they break work down), otherwise the
// sentences that read as something to do. `existing` titles are skipped so a suggestion is never a duplicate.
export const suggestSubtasks = (html, existing = []) => {
  if (!html || isEmptyHtml(html)) return [];
  const known = new Set(existing.map((t) => t.trim().toLowerCase()));
  const body = parse(looksLikeHtml(html) ? html : toEditorHtml(html));

  const fromLists = Array.from(body.querySelectorAll('li'))
    .filter((li) => !li.querySelector('li'))                       // leaf items only
    .map((li) => tidyTitle(li.textContent || ''));

  const text = htmlToText(body.innerHTML);
  const bulletLines = text.split('\n').filter((l) => /^\s*(?:[-*•–]|\d+[.)]|\[[ xX]?\])\s+\S/.test(l)).map(tidyTitle);
  const sentences = text.replace(/([.!?])\s+/g, '$1\n').split('\n').map((s) => s.trim())
    .filter((s) => s.length > 3 && ACTION_START.test(s.replace(/^\s*(?:[-*•–]|\d+[.)])\s*/, '')) && !/^\s*(?:[-*•–]|\d+[.)])\s+/.test(s))
    .map(tidyTitle);

  const ordered = fromLists.length || bulletLines.length ? [...fromLists, ...bulletLines] : sentences;
  const seen = new Set();
  return ordered.filter((t) => {
    const key = t.toLowerCase();
    if (!t || t.length < 3 || known.has(key) || seen.has(key)) return false;
    seen.add(key);
    return true;
  }).slice(0, MAX_SUGGESTIONS);
};
