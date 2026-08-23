import React, { useState } from 'react';
import { validateProject, hasErrors } from '../../utils/validation';

const ProjectModal = ({ isOpen, onClose, onSuccess, teamMembers, apiService }) => {
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    priority: 'medium',
    startDate: '',
    endDate: '',
    teamMemberIds: [],
    tags: ''
  });
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState({});

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (loading) return; // guards against a double-click firing two submits

    const validationErrors = validateProject(formData);
    setErrors(validationErrors);
    if (hasErrors(validationErrors)) return;

    setLoading(true);

    try {
      const projectData = {
        name: formData.name,
        description: formData.description,
        priority: formData.priority,
        startDate: formData.startDate,
        endDate: formData.endDate,
        teamMemberIds: formData.teamMemberIds,
        tags: formData.tags.split(',').map(tag => tag.trim()).filter(tag => tag)
      };
      
      await apiService.createProject(projectData);
      
      setFormData({
        name: '',
        description: '',
        priority: 'medium',
        startDate: '',
        endDate: '',
        teamMemberIds: [],
        tags: ''
      });
      setErrors({});

      onSuccess();
      onClose();
      alert('Project created successfully!');
    } catch (error) {
      alert(`Error creating project: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-gray-600 bg-opacity-50 flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-lg max-w-md w-full p-6 max-h-screen overflow-y-auto">
        <h3 className="text-lg font-semibold mb-4">Add New Project</h3>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <input
              type="text"
              placeholder="Project Name"
              value={formData.name}
              onChange={(e) => setFormData({...formData, name: e.target.value})}
              className={`w-full border rounded-lg px-3 py-2 ${errors.name ? 'border-red-400' : 'border-gray-300'}`}
              aria-invalid={!!errors.name}
              required
            />
            {errors.name && <p className="text-xs text-red-600 mt-1">{errors.name}</p>}
          </div>
          <textarea
            placeholder="Description"
            value={formData.description}
            onChange={(e) => setFormData({...formData, description: e.target.value})}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
            rows="3"
          />
          <select
            value={formData.priority}
            onChange={(e) => setFormData({...formData, priority: e.target.value})}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
          >
            <option value="low">Low Priority</option>
            <option value="medium">Medium Priority</option>
            <option value="high">High Priority</option>
          </select>
          <input
            type="date"
            value={formData.startDate}
            onChange={(e) => setFormData({...formData, startDate: e.target.value})}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
            required
          />
          <div>
            <input
              type="date"
              value={formData.endDate}
              onChange={(e) => setFormData({...formData, endDate: e.target.value})}
              className={`w-full border rounded-lg px-3 py-2 ${errors.endDate ? 'border-red-400' : 'border-gray-300'}`}
              aria-invalid={!!errors.endDate}
              required
            />
            {errors.endDate && <p className="text-xs text-red-600 mt-1">{errors.endDate}</p>}
          </div>
          <input
            type="text"
            placeholder="Tags (comma separated)"
            value={formData.tags}
            onChange={(e) => setFormData({...formData, tags: e.target.value})}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
          />
          <div className="flex space-x-3">
            <button
              type="submit"
              disabled={loading}
              className="flex-1 bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {loading ? 'Creating...' : 'Add Project'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="flex-1 bg-gray-300 text-gray-700 py-2 rounded-lg hover:bg-gray-400"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ProjectModal;