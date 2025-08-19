// src/App.js - Complete Project Management Frontend
import React, { useState, useEffect, createContext, useContext } from 'react';
import { Plus, Search, Calendar, Users, CheckCircle, Clock, AlertCircle, Trash2, Edit3, User, Bell, FileText, Tag, Download, Upload, Flag, Shield, UserCheck, Eye, LogOut, Menu, X } from 'lucide-react';

// API Configuration and Service
const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:905/api';

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

// Context for Authentication
const AuthContext = createContext();

const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const token = localStorage.getItem('jwt_token');
        if (token) {
            try {
                const payload = JSON.parse(atob(token.split('.')[1]));
                if (payload.exp * 1000 > Date.now()) {
                    setUser({
                        id: parseInt(payload.nameid),
                        name: payload.unique_name,
                        email: payload.email,
                        role: payload.role
                    });
                } else {
                    localStorage.removeItem('jwt_token');
                }
            } catch (error) {
                console.error('Invalid token:', error);
                localStorage.removeItem('jwt_token');
            }
        }
        setLoading(false);
    }, []);

    const login = async (email, password) => {
        const apiService = new ApiService();
        const response = await apiService.login(email, password);
        if (response?.user) {
            setUser(response.user);
            return response;
        }
        throw new Error('Login failed');
    };

    const logout = () => {
        setUser(null);
        localStorage.removeItem('jwt_token');
    };

    const value = {
        user,
        login,
        logout,
        loading
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within AuthProvider');
    }
    return context;
};

// Utility Components
const StatusBadge = ({ status }) => {
    const statusConfig = {
        'todo': { color: 'bg-gray-100 text-gray-800', icon: Clock },
        'in-progress': { color: 'bg-blue-100 text-blue-800', icon: AlertCircle },
        'completed': { color: 'bg-green-100 text-green-800', icon: CheckCircle }
    };

    const config = statusConfig[status] || statusConfig['todo'];
    const Icon = config.icon;

    return (
        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${config.color}`}>
            <Icon className="w-3 h-3 mr-1" />
            {status.replace('-', ' ')}
        </span>
    );
};

const PriorityBadge = ({ priority }) => {
    const priorityColors = {
        'low': 'bg-gray-100 text-gray-800',
        'medium': 'bg-yellow-100 text-yellow-800',
        'high': 'bg-red-100 text-red-800'
    };

    return (
        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${priorityColors[priority]}`}>
            {priority}
        </span>
    );
};

const RoleBadge = ({ role }) => {
    const roleColors = {
        'admin': 'bg-purple-100 text-purple-800',
        'manager': 'bg-blue-100 text-blue-800',
        'member': 'bg-green-100 text-green-800'
    };

    const roleIcons = {
        'admin': Shield,
        'manager': UserCheck,
        'member': User
    };

    const Icon = roleIcons[role];

    return (
        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${roleColors[role]}`}>
            <Icon className="w-3 h-3 mr-1" />
            {role}
        </span>
    );
};

const TagsList = ({ tags }) => {
    if (!tags || tags.length === 0) return null;

    return (
        <div className="flex flex-wrap gap-1">
            {tags.map((tag, index) => (
                <span key={index} className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-800">
                    <Tag className="w-3 h-3 mr-1" />
                    {tag}
                </span>
            ))}
        </div>
    );
};

const LoadingSpinner = ({ text = "Loading..." }) => (
    <div className="flex justify-center items-center py-8">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        <span className="ml-2 text-gray-600">{text}</span>
    </div>
);

const ErrorMessage = ({ message, onRetry }) => (
    <div className="bg-red-50 border border-red-200 rounded-lg p-4">
        <p className="text-red-800">Error: {message}</p>
        {onRetry && (
            <button
                onClick={onRetry}
                className="mt-2 text-red-600 hover:text-red-800 underline"
            >
                Try again
            </button>
        )}
    </div>
);

// Login Component
const LoginForm = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const { login } = useAuth();

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        setError('');

        try {
            await login(email, password);
        } catch (error) {
            setError('Invalid email or password');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
            <div className="max-w-md w-full space-y-8">
                <div>
                    <div className="mx-auto h-12 w-12 flex items-center justify-center rounded-full bg-blue-100">
                        <CheckCircle className="h-8 w-8 text-blue-600" />
                    </div>
                    <h2 className="mt-6 text-center text-3xl font-extrabold text-gray-900">
                        Sign in to Khoi Pro
                    </h2>
                    <p className="mt-2 text-center text-sm text-gray-600">
                        Enter your credentials to access the project management system
                    </p>
                </div>
                <form className="mt-8 space-y-6" onSubmit={handleSubmit}>
                    <div className="rounded-md shadow-sm -space-y-px">
                        <div>
                            <input
                                type="email"
                                required
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                className="appearance-none rounded-none relative block w-full px-3 py-2 border border-gray-300 placeholder-gray-500 text-gray-900 rounded-t-md focus:outline-none focus:ring-blue-500 focus:border-blue-500 focus:z-10 sm:text-sm"
                                placeholder="Email address"
                            />
                        </div>
                        <div>
                            <input
                                type="password"
                                required
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                className="appearance-none rounded-none relative block w-full px-3 py-2 border border-gray-300 placeholder-gray-500 text-gray-900 rounded-b-md focus:outline-none focus:ring-blue-500 focus:border-blue-500 focus:z-10 sm:text-sm"
                                placeholder="Password"
                            />
                        </div>
                    </div>

                    {error && (
                        <div className="text-red-600 text-sm text-center">{error}</div>
                    )}

                    <div>
                        <button
                            type="submit"
                            disabled={loading}
                            className="group relative w-full flex justify-center py-2 px-4 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
                        >
                            {loading ? 'Signing in...' : 'Sign in'}
                        </button>
                    </div>

                    <div className="mt-6">
                        <div className="relative">
                            <div className="absolute inset-0 flex items-center">
                                <div className="w-full border-t border-gray-300" />
                            </div>
                           
                        </div>
                        
                    </div>
                </form>
            </div>
        </div>
    );
};

// Main Dashboard Component
const ProjectManagementSystem = () => {
    const { user, logout } = useAuth();
    const [apiService] = useState(() => new ApiService());

    // Data state
    const [projects, setProjects] = useState([]);
    const [tasks, setTasks] = useState([]);
    const [teamMembers, setTeamMembers] = useState([]);
    const [notifications, setNotifications] = useState([]);
    const [dashboardStats, setDashboardStats] = useState({
        totalProjects: 0,
        activeProjects: 0,
        totalTasks: 0,
        completedTasks: 0,
        inProgressTasks: 0,
        todoTasks: 0,
        overdueTasks: 0,
        completionRate: 0
    });

    // Loading and error states
    const [loading, setLoading] = useState({
        dashboard: false,
        projects: false,
        tasks: false,
        teamMembers: false,
        reports: false
    });
    const [errors, setErrors] = useState({});

    // UI state
    const [activeTab, setActiveTab] = useState('dashboard');
    const [searchTerm, setSearchTerm] = useState('');
    const [filterStatus, setFilterStatus] = useState('all');
    const [showAddProject, setShowAddProject] = useState(false);
    const [showAddTask, setShowAddTask] = useState(false);
    const [showAddMember, setShowAddMember] = useState(false);
    const [showNotifications, setShowNotifications] = useState(false);
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    // Form states
    const [newProject, setNewProject] = useState({
        name: '',
        description: '',
        priority: 'medium',
        startDate: '',
        endDate: '',
        teamMemberIds: [],
        tags: ''
    });

    const [newTask, setNewTask] = useState({
        projectId: '',
        title: '',
        description: '',
        priority: 'medium',
        assignedToId: '',
        dueDate: '',
        tags: ''
    });

    const [newMember, setNewMember] = useState({
        name: '',
        role: 'member',
        position: '',
        email: '',
        password: ''
    });

    // Permission checking
    const hasPermission = (action) => {
        const permissions = {
            admin: ['create', 'edit', 'delete', 'assign', 'reports', 'manage_users'],
            manager: ['create', 'edit', 'assign', 'reports'],
            member: ['create', 'edit']
        };
        return permissions[user?.role]?.includes(action) || false;
    };

    // Data loading functions
    const loadDashboardData = async () => {
        setLoading(prev => ({ ...prev, dashboard: true }));
        try {
            const [stats, recentTasks, notifs] = await Promise.all([
                apiService.getDashboardStats(),
                apiService.getTasks({ limit: 5 }),
                apiService.getNotifications()
            ]);

            setDashboardStats(stats || dashboardStats);
            setTasks(recentTasks || []);
            setNotifications(notifs || []);
            setErrors(prev => ({ ...prev, dashboard: null }));
        } catch (error) {
            setErrors(prev => ({ ...prev, dashboard: error.message }));
        } finally {
            setLoading(prev => ({ ...prev, dashboard: false }));
        }
    };

    const loadProjects = async () => {
        setLoading(prev => ({ ...prev, projects: true }));
        try {
            const projectsData = await apiService.getProjects();
            setProjects(projectsData || []);
            setErrors(prev => ({ ...prev, projects: null }));
        } catch (error) {
            setErrors(prev => ({ ...prev, projects: error.message }));
        } finally {
            setLoading(prev => ({ ...prev, projects: false }));
        }
    };

    const loadTasks = async () => {
        setLoading(prev => ({ ...prev, tasks: true }));
        try {
            const filter = {};
            if (filterStatus !== 'all') {
                if (filterStatus === 'overdue') {
                    filter.isOverdue = true;
                } else {
                    filter.status = filterStatus;
                }
            }
            if (searchTerm) {
                filter.searchTerm = searchTerm;
            }

            const tasksData = await apiService.getTasks(filter);
            setTasks(tasksData || []);
            setErrors(prev => ({ ...prev, tasks: null }));
        } catch (error) {
            setErrors(prev => ({ ...prev, tasks: error.message }));
        } finally {
            setLoading(prev => ({ ...prev, tasks: false }));
        }
    };

    const loadTeamMembers = async () => {
        setLoading(prev => ({ ...prev, teamMembers: true }));
        try {
            const members = await apiService.getUsers();
            setTeamMembers(members || []);
            setErrors(prev => ({ ...prev, teamMembers: null }));
        } catch (error) {
            setErrors(prev => ({ ...prev, teamMembers: error.message }));
        } finally {
            setLoading(prev => ({ ...prev, teamMembers: false }));
        }
    };

    // Initial data loading
    useEffect(() => {
        loadDashboardData();
    }, []);

    useEffect(() => {
        if (activeTab === 'projects') {
            loadProjects();
        } else if (activeTab === 'tasks') {
            loadTasks();
        } else if (activeTab === 'team') {
            loadTeamMembers();
        }
    }, [activeTab]);

    useEffect(() => {
        if (activeTab === 'tasks') {
            loadTasks();
        }
    }, [filterStatus, searchTerm]);

    // Helper functions
    const getTeamMemberName = (id) => {
        const member = teamMembers.find(m => m.id === id);
        return member ? member.name : 'Unassigned';
    };

    const getProjectName = (id) => {
        const project = projects.find(p => p.id === id);
        return project ? project.name : 'Unknown Project';
    };

    // CRUD operations
    const handleAddProject = async (e) => {
        e.preventDefault();
        try {
            const projectData = {
                name: newProject.name,
                description: newProject.description,
                priority: newProject.priority,
                startDate: newProject.startDate,
                endDate: newProject.endDate,
                teamMemberIds: newProject.teamMemberIds,
                tags: newProject.tags.split(',').map(tag => tag.trim()).filter(tag => tag)
            };

            await apiService.createProject(projectData);

            setNewProject({
                name: '',
                description: '',
                priority: 'medium',
                startDate: '',
                endDate: '',
                teamMemberIds: [],
                tags: ''
            });
            setShowAddProject(false);

            await loadProjects();
            alert('Project created successfully!');
        } catch (error) {
            alert(`Error creating project: ${error.message}`);
        }
    };

    const handleAddTask = async (e) => {
        e.preventDefault();
        try {
            const taskData = {
                projectId: parseInt(newTask.projectId),
                title: newTask.title,
                description: newTask.description,
                priority: newTask.priority,
                assignedToId: newTask.assignedToId ? parseInt(newTask.assignedToId) : null,
                dueDate: newTask.dueDate,
                tags: newTask.tags.split(',').map(tag => tag.trim()).filter(tag => tag)
            };

            await apiService.createTask(taskData);

            setNewTask({
                projectId: '',
                title: '',
                description: '',
                priority: 'medium',
                assignedToId: '',
                dueDate: '',
                tags: ''
            });
            setShowAddTask(false);

            await loadTasks();
            alert('Task created successfully!');
        } catch (error) {
            alert(`Error creating task: ${error.message}`);
        }
    };

    const handleAddMember = async (e) => {
        e.preventDefault();
        if (!hasPermission('manage_users')) {
            alert('You do not have permission to add team members');
            return;
        }

        try {
            const memberData = {
                name: newMember.name,
                role: newMember.role,
                position: newMember.position,
                email: newMember.email,
                password: newMember.password
            };

            await apiService.createUser(memberData);

            setNewMember({
                name: '',
                role: 'member',
                position: '',
                email: '',
                password: ''
            });
            setShowAddMember(false);

            await loadTeamMembers();
            alert('Team member added successfully!');
        } catch (error) {
            alert(`Error adding team member: ${error.message}`);
        }
    };

    const updateTaskStatus = async (taskId, newStatus) => {
        if (!hasPermission('edit')) {
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

    const deleteTask = async (taskId) => {
        if (!hasPermission('delete')) {
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

    const deleteProject = async (projectId) => {
        if (!hasPermission('delete')) {
            alert('You do not have permission to delete projects');
            return;
        }

        if (!window.confirm('Are you sure you want to delete this project? This will also delete all associated tasks.')) {
            return;
        }

        try {
            await apiService.deleteProject(projectId);
            setProjects(prevProjects => prevProjects.filter(project => project.id !== projectId));
            setTasks(prevTasks => prevTasks.filter(task => task.projectId !== projectId));
            alert('Project deleted successfully!');
        } catch (error) {
            alert(`Error deleting project: ${error.message}`);
        }
    };

    const markNotificationAsRead = async (notificationId) => {
        try {
            await apiService.markNotificationAsRead(notificationId);
            setNotifications(prevNotifications =>
                prevNotifications.map(n =>
                    n.id === notificationId ? { ...n, isRead: true } : n
                )
            );
        } catch (error) {
            console.error('Error marking notification as read:', error);
        }
    };

    const generateReport = async (type) => {
        setLoading(prev => ({ ...prev, reports: true }));
        try {
            let reportData;

            switch (type) {
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

            // Create downloadable file
            const blob = new Blob([JSON.stringify(reportData, null, 2)], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${reportData.title.replace(/\s+/g, '_')}_${new Date().toISOString().split('T')[0]}.json`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            alert(`${reportData.title} downloaded successfully!`);
        } catch (error) {
            alert(`Error generating report: ${error.message}`);
        } finally {
            setLoading(prev => ({ ...prev, reports: false }));
        }
    };

    return (
        <div className="min-h-screen bg-gray-50">
            {/* Header */}
            <header className="bg-white shadow-sm border-b">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex justify-between items-center py-4">
                        <div className="flex items-center">
                            <CheckCircle className="h-8 w-8 text-blue-600 mr-3" />
                            <h1 className="text-2xl font-bold text-gray-900">Khoi Pro</h1>
                        </div>

                        <div className="hidden md:flex items-center space-x-4">
                            <div className="relative">
                                <Search className="h-5 w-5 absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                                <input
                                    type="text"
                                    placeholder="Search projects, tasks..."
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                    className="pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                                />
                            </div>

                            {/* Notifications */}
                            <div className="relative">
                                <button
                                    onClick={() => setShowNotifications(!showNotifications)}
                                    className="relative p-2 text-gray-400 hover:text-gray-600"
                                >
                                    <Bell className="h-6 w-6" />
                                    {notifications.filter(n => !n.isRead).length > 0 && (
                                        <span className="absolute -top-1 -right-1 h-4 w-4 bg-red-500 text-white text-xs rounded-full flex items-center justify-center">
                                            {notifications.filter(n => !n.isRead).length}
                                        </span>
                                    )}
                                </button>

                                {showNotifications && (
                                    <div className="absolute right-0 mt-2 w-80 bg-white rounded-lg shadow-lg border z-50">
                                        <div className="p-4 border-b">
                                            <h3 className="font-semibold text-gray-900">Notifications</h3>
                                        </div>
                                        <div className="max-h-64 overflow-y-auto">
                                            {notifications.length === 0 ? (
                                                <div className="p-4 text-center text-gray-500">
                                                    No notifications
                                                </div>
                                            ) : (
                                                notifications.slice(0, 5).map((notification) => (
                                                    <div
                                                        key={notification.id}
                                                        className={`p-4 border-b cursor-pointer hover:bg-gray-50 ${!notification.isRead ? 'bg-blue-50' : ''}`}
                                                        onClick={() => markNotificationAsRead(notification.id)}
                                                    >
                                                        <p className="text-sm text-gray-900">{notification.message}</p>
                                                        <p className="text-xs text-gray-500 mt-1">
                                                            {new Date(notification.createdAt).toLocaleDateString()}
                                                        </p>
                                                    </div>
                                                ))
                                            )}
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* User Menu */}
                            <div className="flex items-center space-x-2">
                                <div className="h-8 w-8 bg-blue-100 rounded-full flex items-center justify-center">
                                    <User className="h-5 w-5 text-blue-600" />
                                </div>
                                <div className="text-sm">
                                    <p className="font-medium text-gray-900">{user?.name}</p>
                                    <RoleBadge role={user?.role} />
                                </div>
                                <button
                                    onClick={logout}
                                    className="p-2 text-gray-400 hover:text-gray-600"
                                    title="Logout"
                                >
                                    <LogOut className="h-5 w-5" />
                                </button>
                            </div>
                        </div>

                        {/* Mobile menu button */}
                        <div className="md:hidden">
                            <button
                                onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                                className="p-2 text-gray-400 hover:text-gray-600"
                            >
                                {mobileMenuOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
                            </button>
                        </div>
                    </div>
                </div>
            </header>

            {/* Mobile Navigation */}
            {mobileMenuOpen && (
                <div className="md:hidden bg-white border-b">
                    <div className="px-4 py-2 space-y-1">
                        {['dashboard', 'projects', 'tasks', 'team', 'reports'].map((tab) => (
                            <button
                                key={tab}
                                onClick={() => {
                                    setActiveTab(tab);
                                    setMobileMenuOpen(false);
                                }}
                                className={`block w-full text-left px-3 py-2 rounded-md text-sm font-medium capitalize ${activeTab === tab
                                        ? 'bg-blue-100 text-blue-700'
                                        : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
                                    }`}
                            >
                                {tab}
                            </button>
                        ))}
                        <button
                            onClick={logout}
                            className="block w-full text-left px-3 py-2 rounded-md text-sm font-medium text-red-600 hover:text-red-900 hover:bg-red-50"
                        >
                            Logout
                        </button>
                    </div>
                </div>
            )}

            {/* Desktop Navigation */}
            <nav className="bg-white border-b hidden md:block">
                <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <div className="flex space-x-8">
                        {['dashboard', 'projects', 'tasks', 'team', 'reports'].map((tab) => (
                            <button
                                key={tab}
                                onClick={() => setActiveTab(tab)}
                                className={`py-4 px-1 border-b-2 font-medium text-sm capitalize ${activeTab === tab
                                        ? 'border-blue-500 text-blue-600'
                                        : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                                    }`}
                            >
                                {tab}
                            </button>
                        ))}
                    </div>
                </div>
            </nav>

            {/* Main Content */}
            <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
                {/* Dashboard Tab */}
                {activeTab === 'dashboard' && (
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
                                                        <p className="text-sm text-gray-500">{task.projectName || getProjectName(task.projectId)}</p>
                                                        {task.tags && task.tags.length > 0 && (
                                                            <div className="mt-1">
                                                                <TagsList tags={task.tags} />
                                                            </div>
                                                        )}
                                                    </div>
                                                    <div className="flex items-center space-x-4">
                                                        <StatusBadge status={task.status} />
                                                        <PriorityBadge priority={task.priority} />
                                                        <span className="text-sm text-gray-500">{task.assignedToName || getTeamMemberName(task.assignedToId)}</span>
                                                    </div>
                                                </div>
                                            ))
                                        )}
                                    </div>
                                </div>
                            </>
                        )}
                    </div>
                )}

                {/* Projects Tab */}
                {activeTab === 'projects' && (
                    <div className="space-y-6">
                        <div className="flex justify-between items-center">
                            <div>
                                <h2 className="text-3xl font-bold text-gray-900">Projects</h2>
                                <p className="text-gray-600">Manage your projects</p>
                            </div>
                            {hasPermission('create') && (
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
                                        No projects found. {hasPermission('create') && 'Create your first project!'}
                                    </div>
                                ) : (
                                    projects.map((project) => (
                                        <div key={project.id} className="bg-white rounded-lg shadow p-6">
                                            <div className="flex justify-between items-start mb-4">
                                                <h3 className="text-lg font-semibold text-gray-900">{project.name}</h3>
                                                <div className="flex space-x-2">
                                                    {hasPermission('edit') && (
                                                        <button className="text-gray-400 hover:text-gray-600">
                                                            <Edit3 className="h-4 w-4" />
                                                        </button>
                                                    )}
                                                    {hasPermission('delete') && (
                                                        <button
                                                            onClick={() => deleteProject(project.id)}
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
                                                    {new Date(project.startDate).toLocaleDateString()} - {new Date(project.endDate).toLocaleDateString()}
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
                    </div>
                )}

                {/* Tasks Tab */}
                {activeTab === 'tasks' && (
                    <div className="space-y-6">
                        <div className="flex justify-between items-center">
                            <div>
                                <h2 className="text-3xl font-bold text-gray-900">Tasks</h2>
                                <p className="text-gray-600">Manage all tasks</p>
                            </div>
                            {hasPermission('create') && (
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
                                                            {task.projectName || getProjectName(task.projectId)}
                                                        </td>
                                                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                                            {task.assignedToName || getTeamMemberName(task.assignedToId)}
                                                        </td>
                                                        <td className="px-6 py-4 whitespace-nowrap">
                                                            {hasPermission('edit') ? (
                                                                <select
                                                                    value={task.status}
                                                                    onChange={(e) => updateTaskStatus(task.id, e.target.value)}
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
                                                            {new Date(task.dueDate).toLocaleDateString()}
                                                        </td>
                                                        <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                                                            <div className="flex space-x-2">
                                                                <button className="text-blue-600 hover:text-blue-900">
                                                                    <Eye className="h-4 w-4" />
                                                                </button>
                                                                {hasPermission('delete') && (
                                                                    <button
                                                                        onClick={() => deleteTask(task.id)}
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
                    </div>
                )}

                {/* Team Tab */}
                {activeTab === 'team' && (
                    <div className="space-y-6">
                        <div className="flex justify-between items-center">
                            <div>
                                <h2 className="text-3xl font-bold text-gray-900">Team</h2>
                                <p className="text-gray-600">Manage team members</p>
                            </div>
                            {hasPermission('manage_users') && (
                                <button
                                    onClick={() => setShowAddMember(true)}
                                    className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center"
                                >
                                    <Plus className="h-5 w-5 mr-2" />
                                    Add Member
                                </button>
                            )}
                        </div>

                        {loading.teamMembers && <LoadingSpinner text="Loading team members..." />}

                        {errors.teamMembers && (
                            <ErrorMessage message={errors.teamMembers} onRetry={loadTeamMembers} />
                        )}

                        {!loading.teamMembers && !errors.teamMembers && (
                            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                                {teamMembers.length === 0 ? (
                                    <div className="col-span-full text-center py-8 text-gray-500">
                                        No team members found.
                                    </div>
                                ) : (
                                    teamMembers.map((member) => (
                                        <div key={member.id} className="bg-white rounded-lg shadow p-6">
                                            <div className="flex items-center mb-4">
                                                <div className="h-12 w-12 bg-blue-100 rounded-full flex items-center justify-center">
                                                    <User className="h-6 w-6 text-blue-600" />
                                                </div>
                                                <div className="ml-4">
                                                    <h3 className="text-lg font-semibold text-gray-900">{member.name}</h3>
                                                    <p className="text-gray-600">{member.position}</p>
                                                </div>
                                            </div>
                                            <div className="space-y-2">
                                                <RoleBadge role={member.role} />
                                                <p className="text-sm text-gray-500">{member.email}</p>
                                                <div className="text-sm text-gray-600">
                                                    <p>Tasks assigned: {tasks.filter(t => t.assignedToId === member.id).length}</p>
                                                    <p>Tasks completed: {tasks.filter(t => t.assignedToId === member.id && t.status === 'completed').length}</p>
                                                    <p className="text-red-600">Overdue tasks: {tasks.filter(t => t.assignedToId === member.id && t.isOverdue).length}</p>
                                                </div>
                                            </div>
                                        </div>
                                    ))
                                )}
                            </div>
                        )}
                    </div>
                )}

                {/* Reports Tab */}
                {activeTab === 'reports' && (
                    <div className="space-y-6">
                        <div>
                            <h2 className="text-3xl font-bold text-gray-900">Reports</h2>
                            <p className="text-gray-600">Generate and download reports</p>
                        </div>

                        {hasPermission('reports') ? (
                            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                                <div className="bg-white rounded-lg shadow p-6">
                                    <div className="flex items-center mb-4">
                                        <FileText className="h-8 w-8 text-blue-600 mr-3" />
                                        <h3 className="text-lg font-semibold text-gray-900">Project Summary</h3>
                                    </div>
                                    <p className="text-gray-600 mb-4">Overview of all projects, their status, and completion rates.</p>
                                    <button
                                        onClick={() => generateReport('project-summary')}
                                        disabled={loading.reports}
                                        className="w-full bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700 flex items-center justify-center disabled:opacity-50"
                                    >
                                        {loading.reports ? (
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
                                        disabled={loading.reports}
                                        className="w-full bg-green-600 text-white py-2 rounded-lg hover:bg-green-700 flex items-center justify-center disabled:opacity-50"
                                    >
                                        {loading.reports ? (
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
                                        disabled={loading.reports}
                                        className="w-full bg-red-600 text-white py-2 rounded-lg hover:bg-red-700 flex items-center justify-center disabled:opacity-50"
                                    >
                                        {loading.reports ? (
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
                )}
            </main>

            {/* Modals */}
            {/* Add Project Modal */}
            {showAddProject && (
                <div className="fixed inset-0 bg-gray-600 bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-lg max-w-md w-full p-6 max-h-screen overflow-y-auto">
                        <h3 className="text-lg font-semibold mb-4">Add New Project</h3>
                        <form onSubmit={handleAddProject} className="space-y-4">
                            <input
                                type="text"
                                placeholder="Project Name"
                                value={newProject.name}
                                onChange={(e) => setNewProject({ ...newProject, name: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <textarea
                                placeholder="Description"
                                value={newProject.description}
                                onChange={(e) => setNewProject({ ...newProject, description: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                rows="3"
                            />
                            <select
                                value={newProject.priority}
                                onChange={(e) => setNewProject({ ...newProject, priority: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                            >
                                <option value="low">Low Priority</option>
                                <option value="medium">Medium Priority</option>
                                <option value="high">High Priority</option>
                            </select>
                            <input
                                type="date"
                                value={newProject.startDate}
                                onChange={(e) => setNewProject({ ...newProject, startDate: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <input
                                type="date"
                                value={newProject.endDate}
                                onChange={(e) => setNewProject({ ...newProject, endDate: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <input
                                type="text"
                                placeholder="Tags (comma separated)"
                                value={newProject.tags}
                                onChange={(e) => setNewProject({ ...newProject, tags: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                            />
                            <div className="flex space-x-3">
                                <button
                                    type="submit"
                                    className="flex-1 bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700"
                                >
                                    Add Project
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setShowAddProject(false)}
                                    className="flex-1 bg-gray-300 text-gray-700 py-2 rounded-lg hover:bg-gray-400"
                                >
                                    Cancel
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Add Task Modal */}
            {showAddTask && (
                <div className="fixed inset-0 bg-gray-600 bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-lg max-w-md w-full p-6 max-h-screen overflow-y-auto">
                        <h3 className="text-lg font-semibold mb-4">Add New Task</h3>
                        <form onSubmit={handleAddTask} className="space-y-4">
                            <select
                                value={newTask.projectId}
                                onChange={(e) => setNewTask({ ...newTask, projectId: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            >
                                <option value="">Select Project</option>
                                {projects.map(project => (
                                    <option key={project.id} value={project.id}>{project.name}</option>
                                ))}
                            </select>
                            <input
                                type="text"
                                placeholder="Task Title"
                                value={newTask.title}
                                onChange={(e) => setNewTask({ ...newTask, title: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <textarea
                                placeholder="Description"
                                value={newTask.description}
                                onChange={(e) => setNewTask({ ...newTask, description: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                rows="3"
                            />
                            <select
                                value={newTask.priority}
                                onChange={(e) => setNewTask({ ...newTask, priority: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                            >
                                <option value="low">Low Priority</option>
                                <option value="medium">Medium Priority</option>
                                <option value="high">High Priority</option>
                            </select>
                            <select
                                value={newTask.assignedToId}
                                onChange={(e) => setNewTask({ ...newTask, assignedToId: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                            >
                                <option value="">Assign To</option>
                                {teamMembers.map(member => (
                                    <option key={member.id} value={member.id}>{member.name}</option>
                                ))}
                            </select>
                            <input
                                type="date"
                                value={newTask.dueDate}
                                onChange={(e) => setNewTask({ ...newTask, dueDate: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <input
                                type="text"
                                placeholder="Tags (comma separated)"
                                value={newTask.tags}
                                onChange={(e) => setNewTask({ ...newTask, tags: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                            />
                            <div className="flex space-x-3">
                                <button
                                    type="submit"
                                    className="flex-1 bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700"
                                >
                                    Add Task
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setShowAddTask(false)}
                                    className="flex-1 bg-gray-300 text-gray-700 py-2 rounded-lg hover:bg-gray-400"
                                >
                                    Cancel
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Add Member Modal */}
            {showAddMember && (
                <div className="fixed inset-0 bg-gray-600 bg-opacity-50 flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-lg max-w-md w-full p-6">
                        <h3 className="text-lg font-semibold mb-4">Add Team Member</h3>
                        <form onSubmit={handleAddMember} className="space-y-4">
                            <input
                                type="text"
                                placeholder="Full Name"
                                value={newMember.name}
                                onChange={(e) => setNewMember({ ...newMember, name: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <select
                                value={newMember.role}
                                onChange={(e) => setNewMember({ ...newMember, role: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                            >
                                <option value="member">Member</option>
                                <option value="manager">Manager</option>
                                {user?.role === 'admin' && <option value="admin">Admin</option>}
                            </select>
                            <input
                                type="text"
                                placeholder="Position"
                                value={newMember.position}
                                onChange={(e) => setNewMember({ ...newMember, position: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <input
                                type="email"
                                placeholder="Email"
                                value={newMember.email}
                                onChange={(e) => setNewMember({ ...newMember, email: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <input
                                type="password"
                                placeholder="Password"
                                value={newMember.password}
                                onChange={(e) => setNewMember({ ...newMember, password: e.target.value })}
                                className="w-full border border-gray-300 rounded-lg px-3 py-2"
                                required
                            />
                            <div className="flex space-x-3">
                                <button
                                    type="submit"
                                    className="flex-1 bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700"
                                >
                                    Add Member
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setShowAddMember(false)}
                                    className="flex-1 bg-gray-300 text-gray-700 py-2 rounded-lg hover:bg-gray-400"
                                >
                                    Cancel
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};

// Main App Component
const App = () => {
    return (
        <AuthProvider>
            <div className="App">
                <AuthGuard />
            </div>
        </AuthProvider>
    );
};

// Auth Guard Component
const AuthGuard = () => {
    const { user, loading } = useAuth();

    if (loading) {
        return (
            <div className="min-h-screen flex items-center justify-center">
                <LoadingSpinner text="Loading application..." />
            </div>
        );
    }

    return user ? <ProjectManagementSystem /> : <LoginForm />;
};

export default App;