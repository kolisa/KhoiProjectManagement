export const getTeamMemberName = (id, teamMembers) => {
  const member = teamMembers.find(m => m.id === id);
  return member ? member.name : 'Unassigned';
};

export const getProjectName = (id, projects) => {
  const project = projects.find(p => p.id === id);
  return project ? project.name : 'Unknown Project';
};

export const formatDate = (dateString) => {
  return new Date(dateString).toLocaleDateString();
};

export const downloadReport = (reportData) => {
  const blob = new Blob([JSON.stringify(reportData, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${reportData.title.replace(/\s+/g, '_')}_${new Date().toISOString().split('T')[0]}.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};