// src/components/Team/OrgChartTree.js
// Reporting-structure view of the Team tab - same recursive expand/collapse shape as
// components/Spaces/SpaceTree.jsx (ParentSpaceId there is exactly User.managerId here), but
// synchronous: the full team list is already loaded in App.js, so the tree is built once in memory
// instead of lazily fetched per node. Each node is rendered as the same avatar/name/position/RoleBadge
// card language the Team tab's own member grid already uses, just in a compact single-row form,
// rather than a plain indented text tree.
import React, { useMemo, useState } from 'react';
import { ChevronRight, ChevronDown } from 'lucide-react';
import { getAvatarColor } from '../../utils/avatarColor';
import RoleBadge from '../Common/RoleBadge';

const OrgChartNode = ({ member, childrenByManagerId, depth }) => {
  const [expanded, setExpanded] = useState(depth < 1);
  const reports = childrenByManagerId.get(member.id) || [];
  const initials = (member.name || '?').split(' ').filter(Boolean).map((n) => n[0]).slice(0, 2).join('').toUpperCase();

  return (
    <div>
      <div
        className="flex items-center gap-2 py-2 pr-2 rounded-lg hover:bg-gray-50/60 transition-colors"
        style={{ paddingLeft: `${depth * 28 + 4}px` }}
      >
        <button
          onClick={() => setExpanded((e) => !e)}
          className={`flex-shrink-0 ${reports.length === 0 ? 'invisible' : ''}`}
          aria-label="Toggle direct reports"
        >
          {expanded ? <ChevronDown className="h-4 w-4 text-gray-400" /> : <ChevronRight className="h-4 w-4 text-gray-400" />}
        </button>
        <div className={`h-9 w-9 ${getAvatarColor(member.name)} rounded-full flex items-center justify-center text-white text-xs font-semibold flex-shrink-0`}>
          {initials}
        </div>
        <div className="min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm font-semibold text-gray-900 truncate">{member.name}</span>
            <RoleBadge role={member.role} />
          </div>
          <p className="text-xs text-gray-500 truncate">
            {member.position}
            {reports.length > 0 && (
              <span className="text-gray-400"> &middot; {reports.length} direct report{reports.length !== 1 ? 's' : ''}</span>
            )}
          </p>
        </div>
      </div>
      {expanded && reports.length > 0 && (
        <div>
          {reports.map((report) => (
            <OrgChartNode key={report.id} member={report} childrenByManagerId={childrenByManagerId} depth={depth + 1} />
          ))}
        </div>
      )}
    </div>
  );
};

const OrgChartTree = ({ teamMembers }) => {
  const childrenByManagerId = useMemo(() => {
    const map = new Map();
    teamMembers.forEach((m) => {
      if (m.managerId == null) return;
      if (!map.has(m.managerId)) map.set(m.managerId, []);
      map.get(m.managerId).push(m);
    });
    return map;
  }, [teamMembers]);

  const roots = teamMembers.filter((m) => m.managerId == null);

  if (teamMembers.length === 0) {
    return <div className="text-sm text-gray-400 p-2">No team members yet.</div>;
  }

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-3">
      {roots.map((member) => (
        <OrgChartNode key={member.id} member={member} childrenByManagerId={childrenByManagerId} depth={0} />
      ))}
    </div>
  );
};

export default OrgChartTree;
