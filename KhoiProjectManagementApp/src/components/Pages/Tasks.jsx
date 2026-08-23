import React, { useState } from 'react';
import { Plus, Flag, Eye, Trash2 } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import StatusBadge from '../Common/StatusBadge';
import PriorityBadge from '../Common/PriorityBadge';
import TagsList from '../Common/TagsList';
import TaskModal from '../Modals/TaskModal';
import { hasPermission } from '../../utils/permissions';
import { getTeamMemberName, getProjectName, formatDate } from '../../utils/helpers';

const Tasks = ({ 
  user,
  tasks,
  projects,
  teamMembers,
  loading,
  errors,
  filterStatus,
  setFilterStatus,
  loadTasks,
  apiService,
  setTasks
}) => {
  const [showAddTask, setShowAddTask] = useState(false);

  const handleUpdateTaskStatus = async (taskId, newStatus) => {
    if (!hasPermission(user.role, 'edit')) {
      alert('You do not have permission to update tasks');
      return;
    }
    
    try {
      await apiService.updateTaskStatus(taskId, newStatus);
      
      setTasks(prevTasks => 
        prevTasks.map(task => 
          task.id === taskId ? { ...task, status: newStatus } : task
        )
      );
      
      if (newStatus === 'completed') {
        alert('Task marked as completed!');
      }
    } catch (error) {
      alert(`Error updating task: ${error.message}`);
    }
  };

  const handleDeleteTask = async (taskId) => {
    if (!hasPermission(user.role, 'delete')) {
      alert('You do not have permission to delete tasks');
      return;
    }
    
    if (!window.confirm('Are you sure you want to delete this task?')) {
      return;
    }
    
    try {
      await apiService.deleteTask(taskId);
      setTasks(prevTasks => prevTasks.filter(task => task.id !== taskId));
      alert('Task deleted successfully!');
    } catch (error) {
      alert(`Error deleting task: ${error.message}`);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-3xl font-bold text-gray-900">Tasks</h2>
          <p className="text-gray-600">Manage all tasks</p>
        </div>
        {hasPermission(user.role, 'create') && (
          <button
            onClick={() => setShowAddTask(true)}
            className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center"
          >
            <Plus className="h-5 w-5 mr-2" />
            New Task
          </button>
        )}
      </div>

      <div className="flex space-x-4 mb-6">
        <select
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2"
        >
          <option value="all">All Status</option>
          <option value="todo">To Do</option>
          <option value="in-progress">In Progress</option>
          <option value="completed">Completed</option>
          <option value="overdue">Overdue</option>
        </select>
      </div>

      {loading.tasks && <LoadingSpinner text="Loading tasks..." />}
      
      {errors.tasks && (
        <ErrorMessage message={errors.tasks} onRetry={loadTasks} />
      )}

      {!loading.tasks && !errors.tasks && (
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Task</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Project</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Assigned To</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Priority</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Due Date</th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {tasks.length === 0 ? (
                  <tr>
                    <td colSpan="7" className="px-6 py-8 text-center text-gray-500">
                      No tasks found for the selected filter
                    </td>
                  </tr>
                ) : (
                  tasks.map((task) => (
                    <tr key={task.id} className={task.isOverdue ? 'bg-red-50' : ''}>
                      <td className="px-6 py-4">
                        <div className="flex items-center">
                          <div className="text-sm font-medium text-gray-900 flex items-center">
                            {task.title}
                            {task.isOverdue && <Flag className="h-4 w-4 text-red-500 ml-2" />}
                          </div>
                        </div>
                        <div className="text-sm text-gray-500">{task.description}</div>
                        {task.tags && task.tags.length > 0 && (
                          <div className="mt-1">
                            <TagsList tags={task.tags} />
                          </div>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {task.projectName || getProjectName(task.projectId, projects)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {task.assignedToName || getTeamMemberName(task.assignedToId, teamMembers)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        {hasPermission(user.role, 'edit') ? (
                          <select
                            value={task.status}
                            onChange={(e) => handleUpdateTaskStatus(task.id, e.target.value)}
                            className="text-sm border border-gray-300 rounded px-2 py-1"
                          >
                            <option value="todo">To Do</option>
                            <option value="in-progress">In Progress</option>
                            <option value="completed">Completed</option>
                          </select>
                        ) : (
                          <StatusBadge status={task.status} />
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <PriorityBadge priority={task.priority} />
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                        {formatDate(task.dueDate)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                        <div className="flex space-x-2">
                          <button className="text-blue-600 hover:text-blue-900">
                            <Eye className="h-4 w-4" />
                          </button>
                          {hasPermission(user.role, 'delete') && (
                            <button
                              onClick={() => handleDeleteTask(task.id)}
                              className="text-red-600 hover:text-red-900"
                            >
                              <Trash2 className="h-4 w-4" />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Add Task Modal */}
      {showAddTask && (
        <TaskModal
          isOpen={showAddTask}
          onClose={() => setShowAddTask(false)}
          onSuccess={loadTasks}
          projects={projects}
          teamMembers={teamMembers}
          apiService={apiService}
        />
      )}
    </div>
  );
};

export default Tasks;