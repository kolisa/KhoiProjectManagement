import React from 'react';
import { Check, Minus } from 'lucide-react';
import { PROJECT_ROLES, ABILITY_ROWS, roleCan, accessLevel, ACCESS_LEVEL_LABELS } from './pmConstants';
import { AccessBadge } from './pmUi';

// "Who can do what": every role against every ability, so nobody has to guess what Developer vs Tester vs
// Viewer means. Driven by the same table the buttons use (pmConstants' can()), so it cannot drift from the UI.
// `highlightRole` marks the viewer's own column.
const RoleMatrix = ({ highlightRole }) => (
  <div className="overflow-x-auto">
    <table className="w-full text-sm">
      <caption className="sr-only">What each project role is allowed to do</caption>
      <thead>
        <tr className="border-b border-gray-200 text-left text-xs text-gray-500">
          <th scope="col" className="py-2 pr-4 font-medium">Ability</th>
          {PROJECT_ROLES.map((r) => (
            <th key={r.key} scope="col" className={`px-2 py-2 text-center font-medium ${r.key === highlightRole ? 'bg-blue-50 text-blue-800' : ''}`}>{r.label}</th>
          ))}
        </tr>
      </thead>
      <tbody>
        {ABILITY_ROWS.map((row) => (
          <tr key={row.key} className="border-b border-gray-100">
            <th scope="row" className="py-2 pr-4 text-left font-normal text-gray-800">{row.label}</th>
            {PROJECT_ROLES.map((r) => {
              const allowed = roleCan(r.key, row.key);
              return (
                <td key={r.key} className={`px-2 py-2 text-center ${r.key === highlightRole ? 'bg-blue-50' : ''}`}>
                  {allowed
                    ? <Check className="mx-auto h-4 w-4 text-green-600" aria-label="Allowed" />
                    : <Minus className="mx-auto h-4 w-4 text-gray-300" aria-label="Not allowed" />}
                </td>
              );
            })}
          </tr>
        ))}
        <tr>
          <th scope="row" className="py-2 pr-4 text-left font-medium text-gray-800">Access level</th>
          {PROJECT_ROLES.map((r) => (
            <td key={r.key} className={`px-2 py-2 text-center ${r.key === highlightRole ? 'bg-blue-50' : ''}`}>
              <span className="sr-only">{ACCESS_LEVEL_LABELS[accessLevel(r.key)]}</span>
              <AccessBadge role={r.key} />
            </td>
          ))}
        </tr>
      </tbody>
    </table>
    <p className="mt-3 text-xs text-gray-500">
      Separately, a SuperAdmin can set someone's module access to <strong>Read only</strong> - that caps them at the first row whatever their role.
    </p>
  </div>
);

export default RoleMatrix;
