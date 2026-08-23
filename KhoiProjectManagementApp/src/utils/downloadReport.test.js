import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { downloadReport } from './helpers';

// downloadReport's only current caller (components/Pages/Reports.jsx) is dead code per CLAUDE.md's
// frontend-structure note (never imported from anywhere) - tested here directly as a pure-ish utility
// since the function itself isn't dead, only its one caller.
describe('downloadReport', () => {
  let clickSpy;

  beforeEach(() => {
    // jsdom doesn't implement createObjectURL/revokeObjectURL, and clicking a real <a href="blob:...">
    // would attempt a navigation jsdom can't perform - stub the DOM/Blob boundary rather than the
    // function under test.
    URL.createObjectURL = vi.fn(() => 'blob:fake-url');
    URL.revokeObjectURL = vi.fn();
    clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('triggers a download named after the report title and today\'s date', () => {
    downloadReport({ title: 'Project Summary Report', rows: [] });

    expect(clickSpy).toHaveBeenCalledTimes(1);
    const todayIso = new Date().toISOString().split('T')[0];
    expect(clickSpy.mock.instances[0].download).toBe(`Project_Summary_Report_${todayIso}.json`);
  });

  it('serializes the full report data as the blob content', () => {
    // jsdom's Blob doesn't reliably round-trip through .text()/Response() in this environment - spy on
    // the Blob constructor itself to capture what was actually handed to it, rather than trying to
    // read a real Blob back out.
    const RealBlob = globalThis.Blob;
    const blobSpy = vi.fn((parts, options) => new RealBlob(parts, options));
    vi.stubGlobal('Blob', blobSpy);

    const reportData = { title: 'Overdue Tasks', items: [{ id: 1, title: 'Late task' }] };
    downloadReport(reportData);

    expect(blobSpy).toHaveBeenCalledTimes(1);
    const [parts, options] = blobSpy.mock.calls[0];
    expect(options).toEqual({ type: 'application/json' });
    expect(JSON.parse(parts[0])).toEqual(reportData);
  });

  it('revokes the object URL after triggering the download (no leaked blob URLs)', () => {
    downloadReport({ title: 'Team Performance', rows: [] });

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:fake-url');
  });

  it('replaces whitespace in the title with underscores for the filename', () => {
    downloadReport({ title: 'Multi   Word    Title', rows: [] });

    const todayIso = new Date().toISOString().split('T')[0];
    expect(clickSpy.mock.instances[0].download).toBe(`Multi_Word_Title_${todayIso}.json`);
  });
});
