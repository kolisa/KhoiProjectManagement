import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import OrgChartTree from './OrgChartTree';

// OrgChartTree receives the full team list as a flat array (each member carrying a managerId
// reference, per App.jsx - see CLAUDE.md's frontend-structure note) and builds the tree itself via
// the childrenByManagerId Map in the component. No fetching, no MSW needed here - these tests exercise
// that flattening-to-tree logic directly with small crafted datasets, plus the expand/collapse
// behavior each node manages independently.

const member = (id, name, managerId, overrides = {}) => ({
  id,
  name,
  managerId,
  role: 'member',
  position: 'Engineer',
  ...overrides,
});

describe('OrgChartTree', () => {
  it('shows the empty-state message when there are no team members', () => {
    render(<OrgChartTree teamMembers={[]} />);

    expect(screen.getByText(/no team members yet/i)).toBeInTheDocument();
  });

  it('renders a single manager-less member as a root leaf node with no toggle and no report count', () => {
    render(<OrgChartTree teamMembers={[member(1, 'Solo Person', null)]} />);

    expect(screen.getByText('Solo Person')).toBeInTheDocument();
    // No direct reports -> the expand/collapse toggle is rendered but visually hidden (invisible class),
    // not removed - it still exists in the DOM to keep the row's layout aligned with sibling rows.
    expect(screen.getByRole('button', { name: /toggle direct reports/i })).toHaveClass('invisible');
    expect(screen.queryByText(/direct report/i)).not.toBeInTheDocument();
  });

  it('builds a 3-level hierarchy from a flat manager-id list, nesting each report under its manager', () => {
    const teamMembers = [
      member(1, 'CEO Carla', null, { role: 'admin', position: 'CEO' }),
      member(2, 'Manager Mo', 1, { role: 'manager', position: 'Engineering Manager' }),
      member(3, 'Manager Nia', 1, { role: 'manager', position: 'Sales Manager' }),
      // Mo gets a single report here (not two) so his own row's count text ("1 direct report")
      // can't collide with Carla's ("2 direct reports") when queried below.
      member(4, 'Engineer Eli', 2),
    ];
    render(<OrgChartTree teamMembers={teamMembers} />);

    // depth-0 nodes (roots) default to expanded, so both direct reports of the CEO are visible
    // immediately without any interaction.
    expect(screen.getByText('Manager Mo')).toBeInTheDocument();
    expect(screen.getByText('Manager Nia')).toBeInTheDocument();
    expect(screen.getByText(/2 direct reports/i)).toBeInTheDocument(); // CEO's count
    expect(screen.getByText(/1 direct report$/i)).toBeInTheDocument(); // Mo's count (singular, no "s")

    // depth-1 nodes default to collapsed (useState(depth < 1)), so Mo's own report (depth 2)
    // should NOT be visible yet even though Mo's row itself already shows a "1 direct report" count.
    expect(screen.queryByText('Engineer Eli')).not.toBeInTheDocument();

    // Nia has no reports at all - a true leaf - and must show no count. Scope to the "min-w-0"
    // wrapper that holds both the name/badge row and the position paragraph together - the name
    // <span>'s nearest ancestor <div> is a narrower wrapper (name + RoleBadge only) that never
    // contains the report-count paragraph regardless, which would make the assertion vacuous.
    const niaRow = screen.getByText('Manager Nia').closest('.min-w-0');
    expect(within(niaRow).queryByText(/direct report/i)).not.toBeInTheDocument();
  });

  it('expands a collapsed manager on click to reveal their direct reports, and collapses again on a second click', async () => {
    const user = userEvent.setup();
    const teamMembers = [
      member(1, 'CEO Carla', null),
      member(2, 'Manager Mo', 1),
      member(4, 'Engineer Eli', 2),
      member(5, 'Engineer Fay', 2),
    ];
    render(<OrgChartTree teamMembers={teamMembers} />);

    // Mo is a depth-1 node, so it starts collapsed - his reports aren't in the DOM yet.
    expect(screen.queryByText('Engineer Eli')).not.toBeInTheDocument();

    // Two toggle buttons exist (Carla's and Mo's, both have reports); Carla's is already expanded
    // (depth 0 default), so the second toggle button belongs to Mo.
    const toggles = screen.getAllByRole('button', { name: /toggle direct reports/i });
    expect(toggles).toHaveLength(2);
    await user.click(toggles[1]);

    expect(screen.getByText('Engineer Eli')).toBeInTheDocument();
    expect(screen.getByText('Engineer Fay')).toBeInTheDocument();

    // Click again to collapse.
    await user.click(toggles[1]);
    expect(screen.queryByText('Engineer Eli')).not.toBeInTheDocument();
    expect(screen.queryByText('Engineer Fay')).not.toBeInTheDocument();
  });

  it('renders multiple independent roots when several members have no manager', () => {
    const teamMembers = [
      member(1, 'Root One', null),
      member(2, 'Root Two', null),
      member(3, 'Child Of One', 1),
    ];
    render(<OrgChartTree teamMembers={teamMembers} />);

    expect(screen.getByText('Root One')).toBeInTheDocument();
    expect(screen.getByText('Root Two')).toBeInTheDocument();
    // Root One's child is visible (depth-0 defaults expanded); Root Two has no children at all.
    expect(screen.getByText('Child Of One')).toBeInTheDocument();
    const rootTwoRow = screen.getByText('Root Two').closest('.min-w-0');
    expect(within(rootTwoRow).queryByText(/direct report/i)).not.toBeInTheDocument();
  });

  it('treats a member whose managerId points at a nonexistent id as an orphan, rendering nothing for them', () => {
    // childrenByManagerId keys off real member ids present in the list; a dangling managerId (e.g. the
    // manager was deleted/deactivated and the id wasn't cleared) means the member is neither a root
    // (managerId isn't null) nor reachable from any rendered node - a real edge case worth locking down.
    const teamMembers = [
      member(1, 'CEO Carla', null),
      member(99, 'Orphaned Otto', 12345),
    ];
    render(<OrgChartTree teamMembers={teamMembers} />);

    expect(screen.getByText('CEO Carla')).toBeInTheDocument();
    expect(screen.queryByText('Orphaned Otto')).not.toBeInTheDocument();
  });

  it('renders the correct initials, role badge text, and position for a node', () => {
    render(<OrgChartTree teamMembers={[member(1, 'Ada Lovelace', null, { role: 'admin', position: 'Chief Engineer' })]} />);

    expect(screen.getByText('AL')).toBeInTheDocument();
    expect(screen.getByText('admin')).toBeInTheDocument();
    expect(screen.getByText(/chief engineer/i)).toBeInTheDocument();
  });
});
