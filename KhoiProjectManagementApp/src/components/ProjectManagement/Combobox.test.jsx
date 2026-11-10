import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { useState } from 'react';
import Combobox from './Combobox';

const options = [
  { value: 1, label: 'Ada Lovelace', description: 'ada@x.com', person: true },
  { value: 2, label: 'Ben Carter', description: 'ben@x.com', person: true },
  { value: 3, label: 'Cleo Park', description: 'cleo@x.com', person: true },
];

const Harness = ({ onChange = vi.fn(), initial = '', ...rest }) => {
  const [v, setV] = useState(initial);
  return (
    <>
      <label htmlFor="who">Who</label>
      <Combobox id="who" value={v} onChange={(x) => { setV(x); onChange(x); }} options={options} emptyLabel="Unassigned" {...rest} />
    </>
  );
};

describe('Combobox', () => {
  it('opens a list, filters it as you type, and picks with the mouse', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Harness onChange={onChange} />);

    await user.click(screen.getByLabelText('Who'));
    expect(screen.getAllByRole('option')).toHaveLength(4);                         // Unassigned + 3 people
    await user.type(screen.getByLabelText('Search options'), 'ben');
    expect(screen.getAllByRole('option')).toHaveLength(1);
    await user.click(screen.getByRole('option', { name: /Ben Carter/ }));

    expect(onChange).toHaveBeenCalledWith(2);
    expect(screen.getByLabelText('Who')).toHaveTextContent('Ben Carter');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('also searches the second line (the email)', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByLabelText('Who'));
    await user.type(screen.getByLabelText('Search options'), 'cleo@');
    expect(screen.getByRole('option', { name: /Cleo Park/ })).toBeInTheDocument();
  });

  it('works from the keyboard: arrows move, Enter picks, Escape closes', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Harness onChange={onChange} />);

    await user.click(screen.getByLabelText('Who'));
    await user.keyboard('{ArrowDown}{ArrowDown}{Enter}');          // Unassigned -> Ada -> Ben
    expect(onChange).toHaveBeenCalledWith(2);

    await user.click(screen.getByLabelText('Who'));
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('can choose the empty option to clear the value', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Harness onChange={onChange} initial={1} />);
    expect(screen.getByLabelText('Who')).toHaveTextContent('Ada Lovelace');

    await user.click(screen.getByLabelText('Who'));
    await user.click(screen.getByRole('option', { name: 'Unassigned' }));

    expect(onChange).toHaveBeenCalledWith('');
    expect(screen.getByLabelText('Who')).toHaveTextContent('Unassigned');
  });

  it('says when nothing matches', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByLabelText('Who'));
    await user.type(screen.getByLabelText('Search options'), 'zzz');
    expect(screen.getByText('No matches')).toBeInTheDocument();
  });

  it('does not open when disabled', async () => {
    const user = userEvent.setup();
    render(<Harness disabled title="Your access is read-only." />);
    await user.click(screen.getByLabelText('Who'));
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Who')).toBeDisabled();
  });

  it('closes when you click elsewhere', async () => {
    const user = userEvent.setup();
    render(<><Harness /><button>Elsewhere</button></>);
    await user.click(screen.getByLabelText('Who'));
    await user.click(screen.getByRole('button', { name: 'Elsewhere' }));
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });
});
