import { describe, it, expect } from 'vitest';
import { sanitizeHtml, toEditorHtml, htmlToText, tidyText, suggestSubtasks, isEmptyHtml } from './richText';

describe('sanitizeHtml', () => {
  it('drops scripts, handlers and unsafe links but keeps formatting', () => {
    const out = sanitizeHtml('<p onclick="x()">Hi <strong>there</strong></p><script>alert(1)</script><a href="javascript:alert(1)">bad</a><img src=x onerror=alert(1)>');
    expect(out).toContain('<p>Hi <strong>there</strong></p>');
    expect(out).not.toMatch(/script|onclick|onerror|javascript|<img/i);
  });
  it('keeps safe links and makes them open safely in a new tab', () => {
    const out = sanitizeHtml('<a href="https://example.com" style="color:red">x</a>');
    expect(out).toContain('href="https://example.com"');
    expect(out).toContain('rel="noopener noreferrer"');
    expect(out).not.toContain('style');
  });
});

describe('toEditorHtml / htmlToText', () => {
  it('turns legacy plain text into escaped paragraphs', () => {
    expect(toEditorHtml('Line one\nline two\n\nSecond a & b')).toBe('<p>Line one<br>line two</p><p>Second a &amp; b</p>');
  });
  it('flattens rich text back to readable plain text for the classic screens', () => {
    expect(htmlToText('<p>One</p><ul><li>a</li><li>b</li></ul>')).toBe('One\na\nb');
    expect(htmlToText('plain stays')).toBe('plain stays');
  });
  it('knows when a description is empty', () => {
    expect(isEmptyHtml('<p><br></p>')).toBe(true);
    expect(isEmptyHtml('<p>x</p>')).toBe(false);
  });
});

describe('tidyText', () => {
  it('fixes spelling, repeated words, spacing and capitals, and says what it did', () => {
    const { html, changes } = tidyText('<p>i think teh the login  page should recieve data ,then show it.Next step is hard</p>');
    expect(html).toBe('<p>I think the login page should receive data, then show it. Next step is hard</p>');
    expect(changes.length).toBeGreaterThan(2);
    expect(changes.join(' ')).toMatch(/misspelled/);
  });
  it('keeps the capitalisation of a corrected word', () => {
    expect(tidyText('<p>Teh plan</p>').html).toBe('<p>The plan</p>');
  });
  it('leaves code and links alone', () => {
    const src = '<p>Run <code>teh  command</code> or see <a href="https://x.io">teh docs</a>.</p>';
    expect(tidyText(src).html).toContain('<code>teh  command</code>');
    expect(tidyText(src).html).toContain('>teh docs</a>');
  });
  it('reports nothing when there is nothing to fix', () => {
    const r = tidyText('<p>All good here.</p>');
    expect(r.changes).toEqual([]);
    expect(r.html).toBe('<p>All good here.</p>');
  });
});

describe('suggestSubtasks', () => {
  it('turns list items into subtasks', () => {
    const html = '<p>Build the login:</p><ul><li>Create the login form</li><li>Add validation.</li><li>Write tests</li></ul>';
    expect(suggestSubtasks(html)).toEqual(['Create the login form', 'Add validation', 'Write tests']);
  });
  it('reads bullet lines in plain text', () => {
    expect(suggestSubtasks('- design the table\n- wire the API\n3. deploy')).toEqual(['Design the table', 'Wire the API', 'Deploy']);
  });
  it('falls back to sentences that describe something to do', () => {
    const html = '<p>The page is slow. We need to add an index. Also should migrate old rows. Users like it.</p>';
    expect(suggestSubtasks(html)).toEqual(['Add an index', 'Migrate old rows']);
  });
  it('skips titles that already exist and duplicates', () => {
    expect(suggestSubtasks('<ul><li>Write tests</li><li>write tests</li><li>Deploy</li></ul>', ['Deploy'])).toEqual(['Write tests']);
  });
  it('suggests nothing for an empty description', () => {
    expect(suggestSubtasks('<p><br></p>')).toEqual([]);
  });
});
