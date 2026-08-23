import React, { useState } from 'react';
import { Plus, Calendar, FileText, Edit3, Trash2 } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import TagsList from '../Common/TagsList';
import ProjectModal from '../Modals/ProjectModal';
import { hasPermission } from '../../utils/permissions';
import { formatDate } from '../../utils/helpers';

const Projects = ({ 
  user,
  projects, 
  teamMembers,
  loading, 
  errors, 
  loadProjects,
  apiService,
  setProjects
}) => {
  const [showAddProject, setShowAddProject] = useState(false);

  const handleDeleteProject = async (projectId) => {
    if (!hasPermission(user.role, 'delete')) {
      alert('You do not have permission to delete projects');
      return;
    }
    
    if (!window.confirm('Are you sure you want to delete this project? This will also delete all associated tasks.')) {
      return;
    }
    
    try {
      await apiService.deleteProject(projectId);
      setProjects(prevProjects => prevProjects.filter(project => project.id !== projectId));
      alert('Project deleted successfully!');
    } catch (error) {
      alert(`Error deleting project: ${error.message}`);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-3xl font-bold text-gray-900">Projects</h2>
          <p className="text-gray-600">Manage your projects</p>
        </div>
        {hasPermission(user.role, 'create') && (
          <button
            onClick={() => setShowAddProject(true)}
            className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center"
          >
            <Plus className="h-5 w-5 mr-2" />
            New Project
          </button>
        )}
      </div>

      {loading.projects && <LoadingSpinner text="Loading projects..." />}
      
      {errors.projects && (
        <ErrorMessage message={errors.projects} onRetry={loadProjects} />
      )}

      {!loading.projects && !errors.projects && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {projects.length === 0 ? (
            <div className="col-span-full text-center py-8 text-gray-500">
              No projects found. {hasPermission(user.role, 'create') && 'Create your first project!'}
            </div>
          ) : (
            projects.map((project) => (
              <div key={project.id} className="bg-white rounded-lg shadow p-6">
                <div className="flex justify-between items-start mb-4">
                  <h3 className="text-lg font-semibold text-gray-900">{project.name}</h3>
                  <div className="flex space-x-2">
                    {hasPermission(user.role, 'edit') && (
                      <button className="text-gray-400 hover:text-gray-600">
                        <Edit3 className="h-4 w-4" />
                      </button>
                    )}
                    {hasPermission(user.role, 'delete') && (
                      <button
                        onClick={() => handleDeleteProject(project.id)}
                        className="text-red-400 hover:text-red-600"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                </div>
                <p className="text-gray-600 mb-4">{project.description}</p>
                <div className="space-y-2">
                  <div className="flex justify-between items-center">
                    <PriorityBadge priority={project.priority} />
                    <span className="text-sm text-gray-500">
                      {project.teamMembers?.length || 0} members
                    </span>
                  </div>
                  <div className="text-sm text-gray-500">
                    <Calendar className="h-4 w-4 inline mr-1" />
                    {formatDate(project.startDate)} - {formatDate(project.endDate)}
                  </div>
                  {project.tags && project.tags.length > 0 && (
                    <TagsList tags={project.tags} />
                  )}
                  {project.taskCount !== undefined && (
                    <div className="flex items-center text-sm text-gray-500">
                      <FileText className="h-4 w-4 mr-1" />
                      {project.completedTaskCount || 0}/{project.taskCount || 0} tasks completed
                    </div>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {/* Add Project Modal */}
      {showAddProject && (
        <ProjectModal
          isOpen={showAddProject}
          onClose={() => setShowAddProject(false)}
          onSuccess={loadProjects}
          teamMembers={teamMembers}
          apiService={apiService}
        />
      )}
    </div>
  );
};

export default Projects;