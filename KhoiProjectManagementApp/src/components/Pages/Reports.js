import React, { useState } from 'react';
import { FileText, Users, Flag, Download, Shield } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { downloadReport } from '../../utils/helpers';

const Reports = ({ user, apiService }) => {
  const [loading, setLoading] = useState(false);

  const generateReport = async (type) => {
    setLoading(true);
    try {
      let reportData;
      
      switch(type) {
        case 'project-summary':
          reportData = await apiService.getProjectSummaryReport();
          break;
        case 'team-performance':
          reportData = await apiService.getTeamPerformanceReport();
          break;
        case 'overdue-tasks':
          reportData = await apiService.getOverdueTasksReport();
          break;
        default:
          throw new Error('Unknown report type');
      }
      
      downloadReport(reportData);
      alert(`${reportData.title} downloaded successfully!`);
    } catch (error) {
      alert(`Error generating report: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-gray-900">Reports</h2>
        <p className="text-gray-600">Generate and download reports</p>
      </div>

      {hasPermission(user.role, 'reports') ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center mb-4">
              <FileText className="h-8 w-8 text-blue-600 mr-3" />
              <h3 className="text-lg font-semibold text-gray-900">Project Summary</h3>
            </div>
            <p className="text-gray-600 mb-4">Overview of all projects, their status, and completion rates.</p>
            <button
              onClick={() => generateReport('project-summary')}
              disabled={loading}
              className="w-full bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700 flex items-center justify-center disabled:opacity-50"
            >
              {loading ? (
                <>
                  <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                  Generating...
                </>
              ) : (
                <>
                  <Download className="h-4 w-4 mr-2" />
                  Generate Report
                </>
              )}
            </button>
          </div>

          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center mb-4">
              <Users className="h-8 w-8 text-green-600 mr-3" />
              <h3 className="text-lg font-semibold text-gray-900">Team Performance</h3>
            </div>
            <p className="text-gray-600 mb-4">Individual team member performance and task completion statistics.</p>
            <button
              onClick={() => generateReport('team-performance')}
              disabled={loading}
              className="w-full bg-green-600 text-white py-2 rounded-lg hover:bg-green-700 flex items-center justify-center disabled:opacity-50"
            >
              {loading ? (
                <>
                  <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                  Generating...
                </>
              ) : (
                <>
                  <Download className="h-4 w-4 mr-2" />
                  Generate Report
                </>
              )}
            </button>
          </div>

          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center mb-4">
              <Flag className="h-8 w-8 text-red-600 mr-3" />
              <h3 className="text-lg font-semibold text-gray-900">Overdue Tasks</h3>
            </div>
            <p className="text-gray-600 mb-4">List of all overdue tasks with assignees and due dates.</p>
            <button
              onClick={() => generateReport('overdue-tasks')}
              disabled={loading}
              className="w-full bg-red-600 text-white py-2 rounded-lg hover:bg-red-700 flex items-center justify-center disabled:opacity-50"
            >
              {loading ? (
                <>
                  <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                  Generating...
                </>
              ) : (
                <>
                  <Download className="h-4 w-4 mr-2" />
                  Generate Report
                </>
              )}
            </button>
          </div>
        </div>
      ) : (
        <div className="bg-gray-100 rounded-lg p-8 text-center">
          <Shield className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <p className="text-gray-600">You don't have permission to access reports.</p>
        </div>
      )}
    </div>
  );
};

export default Reports;