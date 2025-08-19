// src/services/ApiService.js
const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7148/api';

class ApiService {
  constructor() {
    this.token = localStorage.getItem('jwt_token') || null;
  }

  async request(endpoint, options = {}) {
    const url = `${API_BASE_URL}${endpoint}`;
    const config = {
      headers: {
        'Content-Type': 'application/json',
        ...(this.token && { Authorization: `Bearer ${this.token}` }),
        ...options.headers,
      },
      ...options,
    };

    try {
      const response = await fetch(url, config);
      
      if (response.status === 401) {
        this.token = null;
        localStorage.removeItem('jwt_token');
        window.location.reload();
        return null;
      }

      if (!response.ok) {
        throw new Error(`API Error: ${response.status} ${response.statusText}`);
      }

      const contentType = response.headers.get('content-type');
      if (contentType && contentType.includes('application/json')) {
        return await response.json();
      }
      
      return response;
    } catch (error) {
      console.error('API Request failed:', error);
      throw error;
    }
  }

  // Authentication
  async login(email, password) {
    const response = await this.request('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    
    if (response?.token) {
      this.token = response.token;
      localStorage.setItem('jwt_token', response.token);
    }
    
    return response;
  }

  async register(userData) {
    return await this.request('/auth/register', {
      method: 'POST',
      body: JSON.stringify(userData),
    });
  }

  // Projects
  async getProjects() {
    return await this.request('/projects');
  }

  async createProject(projectData) {
    return await this.request('/projects', {
      method: 'POST',
      body: JSON.stringify(projectData),
    });
  }

  async updateProject(id, projectData) {
    return await this.request(`/projects/${id}`, {
      method: 'PUT',
      body: JSON.stringify(projectData),
    });
  }

  async deleteProject(id) {
    return await this.request(`/projects/${id}`, {
      method: 'DELETE',
    });
  }

  // Tasks
  async getTasks(filter = {}) {
    const queryParams = new URLSearchParams();
    Object.keys(filter).forEach(key => {
      if (filter[key] !== null && filter[key] !== undefined && filter[key] !== '') {
        queryParams.append(key, filter[key]);
      }
    });
    const queryString = queryParams.toString();
    return await this.request(`/tasks${queryString ? `?${queryString}` : ''}`);
  }

  async createTask(taskData) {
    return await this.request('/tasks', {
      method: 'POST',
      body: JSON.stringify(taskData),
    });
  }

  async updateTaskStatus(id, status) {
    return await this.request(`/tasks/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify(status),
    });
  }

  async deleteTask(id) {
    return await this.request(`/tasks/${id}`, {
      method: 'DELETE',
    });
  }

  // Users
  async getUsers() {
    return await this.request('/users');
  }

  async createUser(userData) {
    return await this.request('/users', {
      method: 'POST',
      body: JSON.stringify(userData),
    });
  }

  // Reports
  async getProjectSummaryReport() {
    return await this.request('/reports/project-summary');
  }

  async getTeamPerformanceReport() {
    return await this.request('/reports/team-performance');
  }

  async getOverdueTasksReport() {
    return await this.request('/reports/overdue-tasks');
  }

  // Notifications
  async getNotifications() {
    return await this.request('/notifications');
  }

  async markNotificationAsRead(id) {
    return await this.request(`/notifications/${id}/read`, {
      method: 'PUT',
    });
  }

  // Dashboard
  async getDashboardStats() {
    return await this.request('/dashboard/statistics');
  }

  logout() {
    this.token = null;
    localStorage.removeItem('jwt_token');
  }
}

export default ApiService;