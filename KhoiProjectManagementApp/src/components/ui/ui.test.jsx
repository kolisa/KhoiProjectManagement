import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  Accordion, Alert, Avatar, AvatarGroup, Badge, Breadcrumb, Button, Card, CardTitle, Collapse, Dropdown,
  FieldError, Input, Modal, Pagination, Progress, Stepper, Switch, Tabs, Tooltip,
} from './index';

describe('UI kit', () => {
  it('Button fires onClick, defaults to type=button, and respects disabled', async () => {
    const onClick = vi.fn();
    const { rerender } = render(<Button onClick={onClick}>Save</Button>);
    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('type', 'button');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onClick).toHaveBeenCalledTimes(1);
    rerender(<Button disabled onClick={onClick}>Save</Button>);
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('Badge, Card and Alert render their content with the right roles', () => {
    render(<><Badge variant="success">Done</Badge><Card><CardTitle>Title</CardTitle></Card><Alert variant="destructive" title="Boom">Details</Alert></>);
    expect(screen.getByText('Done')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Title' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Boom');
  });

  it('Alert dismiss calls onDismiss', async () => {
    const onDismiss = vi.fn();
    render(<Alert onDismiss={onDismiss}>Hi</Alert>);
    await userEvent.click(screen.getByLabelText('Dismiss'));
    expect(onDismiss).toHaveBeenCalled();
  });

  it('Avatar shows initials and AvatarGroup collapses the overflow', () => {
    render(<><Avatar name="Ada Lovelace" /><AvatarGroup max={2} people={[{ name: 'A B' }, { name: 'C D' }, { name: 'E F' }, { name: 'G H' }]} /></>);
    expect(screen.getByText('AL')).toBeInTheDocument();
    expect(screen.getByText('+2')).toBeInTheDocument();
  });

  it('Input marks itself invalid and FieldError announces', () => {
    render(<><Input invalid aria-label="Name" /><FieldError>Required</FieldError></>);
    expect(screen.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Required');
  });

  it('Switch toggles via onChange with the opposite value', async () => {
    const onChange = vi.fn();
    render(<Switch checked={false} onChange={onChange} label="Email" />);
    await userEvent.click(screen.getByRole('switch'));
    expect(onChange).toHaveBeenCalledWith(true);
  });

  it('Progress clamps its value', () => {
    render(<Progress value={250} max={100} label="Load" />);
    expect(screen.getByRole('progressbar').firstChild).toHaveStyle({ width: '100%' });
  });

  it('Tabs switch panels by click and arrow keys', async () => {
    render(<Tabs tabs={[{ key: 'a', label: 'A', content: 'Panel A' }, { key: 'b', label: 'B', content: 'Panel B' }]} />);
    expect(screen.getByText('Panel A')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'B' }));
    expect(screen.getByText('Panel B')).toBeInTheDocument();
    screen.getByRole('tab', { name: 'B' }).focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByText('Panel A')).toBeInTheDocument();
  });

  it('Pagination navigates and hides itself for a single page', async () => {
    const onChange = vi.fn();
    const { rerender, container } = render(<Pagination page={2} pageCount={5} onChange={onChange} />);
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(onChange).toHaveBeenCalledWith(3);
    rerender(<Pagination page={1} pageCount={1} onChange={onChange} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('Breadcrumb marks the last item as the current page', () => {
    render(<Breadcrumb items={[{ label: 'Home', onClick: () => {} }, { label: 'Projects' }]} />);
    expect(screen.getByText('Projects')).toHaveAttribute('aria-current', 'page');
  });

  it('Stepper marks the current step', () => {
    render(<Stepper steps={['One', 'Two', 'Three']} current={1} />);
    expect(screen.getByText('Two').closest('li')).toHaveAttribute('aria-current', 'step');
  });

  it('Collapse and Accordion expand; a single-open Accordion closes the previous item', async () => {
    render(<><Collapse title="More">Hidden body</Collapse><Accordion items={[{ key: 'x', title: 'X', content: 'XC' }, { key: 'y', title: 'Y', content: 'YC' }]} /></>);
    expect(screen.queryByText('Hidden body')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /More/ }));
    expect(screen.getByText('Hidden body')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /^X/ }));
    await userEvent.click(screen.getByRole('button', { name: /^Y/ }));
    expect(screen.queryByText('XC')).not.toBeInTheDocument();
    expect(screen.getByText('YC')).toBeInTheDocument();
  });

  it('Modal is a labelled dialog, closes on Escape, and renders nothing when closed', async () => {
    const onClose = vi.fn();
    const { rerender } = render(<Modal open onClose={onClose} title="Edit">Body</Modal>);
    expect(screen.getByRole('dialog', { name: 'Edit' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
    rerender(<Modal open={false} onClose={onClose} title="Edit">Body</Modal>);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('Dropdown opens, selects an item, and closes', async () => {
    const onSelect = vi.fn();
    render(<Dropdown trigger={<button>Menu</button>} items={[{ key: 'd', label: 'Delete', onSelect }]} />);
    await userEvent.click(screen.getByRole('button', { name: 'Menu' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
    expect(onSelect).toHaveBeenCalled();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('Tooltip wires aria-describedby to its text', () => {
    render(<Tooltip text="Helpful"><button>Hover me</button></Tooltip>);
    const id = screen.getByRole('button', { name: 'Hover me' }).getAttribute('aria-describedby');
    expect(document.getElementById(id)).toHaveTextContent('Helpful');
  });
});
