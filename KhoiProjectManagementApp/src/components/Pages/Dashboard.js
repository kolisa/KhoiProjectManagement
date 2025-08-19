import React from 'react';
import { CheckCircle, Clock, AlertCircle, Flag, Users } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import StatusBadge from '../Common/StatusBadge';
import PriorityBadge from '../Common/PriorityBadge';
import TagsList from '../Common/TagsList';
import { getTeamMemberName, getProjectName } from '../../utils/helpers';

const Dashboard = ({ 
  dashboardStats, 
  tasks, 
  teamMembers, 
  projects, 
  loading, 
  errors, 
  loadDashboardData 
}) => {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-gray-900">Dashboard</h2>
        <p className="text-gray-600">Overview of all projects and tasks</p>
      </div>

      {loading.dashboard && <LoadingSpinner text="Loading dashboard..." />}
      
      {errors.dashboard && (
        <ErrorMessage message={errors.dashboard} onRetry={loadDashboardData} />
      )}

      {!loading.dashboard && !errors.dashboard && (
        <>
          {/* Stats Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-6">
            <div className="bg-white p-6 rounded-lg shadow">
              <div className="flex items-center">
                <CheckCircle className="h-8 w-8 text-blue-600 mr-3" />
                <div>
                  <p className="text-sm font-medium text-gray-500">Total Projects</p>
                  <p className="text-2xl font-bold text-gray-900">{dashboardStats.totalProjects}</p>
                </div>
              </div>
            </div>

            <div className="bg-white p-6 rounded-lg shadow">
              <div className="flex items-center">
                <Clock className="h-8 w-8 text-green-600 mr-3" />
                <div>
                  <p className="text-sm font-medium text-gray-500">Active Projects</p>
                  <p className="text-2xl font-bold text-gray-900">{dashboardStats.activeProjects}</p>
                </div>
              </div>
            </div>

            <div className="bg-white p-6 rounded-lg shadow">
              <div className="flex items-center">
                <AlertCircle className="h-8 w-8 text-yellow-600 mr-3" />
                <div>
                  <p className="text-sm font-medium text-gray-500">Total Tasks</p>
                  <p className="text-2xl font-bold text-gray-900">{dashboardStats.totalTasks}</p>
                </div>
              </div>
            </div>

            <div className="bg-white p-6 rounded-lg shadow">
              <div className="flex items-center">
                <Flag className="h-8 w-8 text-red-600 mr-3" />
                <div>
                  <p className="text-sm font-medium text-gray-500">Overdue Tasks</p>
                  <p className="text-2xl font-bold text-red-900">{dashboardStats.overdueTasks}</p>
                </div>
              </div>
            </div>

            <div className="bg-white p-6 rounded-lg shadow">
              <div className="flex items-center">
                <Users className="h-8 w-8 text-purple-600 mr-3" />
                <div>
                  <p className="text-sm font-medium text-gray-500">Completion Rate</p>
                  <p className="text-2xl font-bold text-gray-900">{Math.round(dashboardStats.completionRate)}%</p>
                </div>
              </div>
            </div>
          </div>

          {/* Overdue Tasks Alert */}
          {dashboardStats.overdueTasks > 0 && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <div className="flex items-center">
                <Flag className="h-5 w-5 text-red-600 mr-2" />
                <h3 className="text-red-800 font-medium">Attention: {dashboardStats.overdueTasks} overdue tasks</h3>
              </div>
              <p className="text-red-700 text-sm mt-1">Review and update these tasks to keep projects on track.</p>
            </div>
          )}

          {/* Recent Tasks */}
          <div className="bg-white rounded-lg shadow">
            <div className="px-6 py-4 border-b border-gray-200">
              <h3 className="text-lg font-medium text-gray-900">Recent Tasks</h3>
            </div>
            <div className="divide-y divide-gray-200">
              {tasks.length === 0 ? (
                <div className="px-6 py-8 text-center text-gray-500">
                  No tasks found
                </div>
              ) : (
                tasks.slice(0, 5).map((task) => (
                  <div key={task.id} className="px-6 py-4 flex items-center justify-between">
                    <div className="flex-1">
                      <div className="flex items-center">
                        <h4 className="text-sm font-medium text-gray-900">{task.title}</h4>
                        {task.isOverdue && <Flag className="h-4 w-4 text-red-500 ml-2" />}
                      </div>
                      <p className="text-sm text-gray-500">{task.projectName || getProjectName(task.projectId, projects)}</p>
                      {task.tags && task.tags.length > 0 && (
                        <div className="mt-1">
                          <TagsList tags={task.tags} />
                        </div>
                      )}
                    </div>
                    <div className="flex items-center space-x-4">
                      <StatusBadge status={task.status} />
                      <PriorityBadge priority={task.priority} />
                      <span className="text-sm text-gray-500">{task.assignedToName || getTeamMemberName(task.assignedToId, teamMembers)}</span>
                    </div>
                  </div>
                ))  
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default Dashboard;