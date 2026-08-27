// src/App.js - Complete Project Management Frontend
import React, { useState, useEffect, useRef } from 'react';
import { Plus, Search, Calendar, Users, CheckCircle, Clock, AlertCircle, Trash2, Edit3, User, Bell, FileText, Tag, Download, Upload, Flag, Shield, UserCheck, Eye, EyeOff, LogOut, Menu, X, Mail, Lock, ChevronDown, LayoutDashboard, Folder, CheckSquare, BookOpen, Archive, Lightbulb, DollarSign, BarChart2, Settings as SettingsIcon, ArrowRight } from 'lucide-react';
import ApiService, { NetworkError } from './services/ApiService';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ToastProvider, useToast } from './contexts/ToastContext';
import { hasPermission } from './utils/permissions';
import { validateProject, validateTask, validateTeamMember, hasErrors } from './utils/validation';
import { getAvatarColor } from './utils/avatarColor';
import { reportApiError } from './utils/apiError';
import VaultPage from './components/Vault/VaultPage';
import WikiPage from './components/Wiki/WikiPage';
import LibraryPage from './components/Library/LibraryPage';
import NotificationPreferences from './components/Settings/NotificationPreferences';
import DashboardWidgetSettings from './components/Settings/DashboardWidgetSettings';
import PermissionsManagement from './components/Settings/PermissionsManagement';
import GroupsManagement from './components/Settings/GroupsManagement';
import AuditLog from './components/Settings/AuditLog';
import OrgChartTree from './components/Team/OrgChartTree';
import IdeasPage from './components/Ideas/IdeasPage';
import InvoicesPage from './components/Finance/InvoicesPage';
import RemindersPage from './components/Reminders/RemindersPage';
import ForgotPasswordForm from './components/Auth/ForgotPasswordForm';
import ResetPasswordForm from './components/Auth/ResetPasswordForm';
import OfflineBanner from './components/Common/OfflineBanner';
import UpdateAvailableBanner from './components/Common/UpdateAvailableBanner';
import khoiLogo from './assets/khoi-logo.png';

// Grouped sidebar/drawer nav config - single source of truth for both the desktop sidebar and the
// mobile drawer (previously two separate flat arrays of tab names duplicated between them).
const NAV_GROUPS = [
    {
        label: null,
        items: [
            { key: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
            { key: 'reminders', label: 'Reminders', icon: Bell },
        ],
    },
    {
        label: 'Work',
        items: [
            { key: 'projects', label: 'Projects', icon: Folder },
            { key: 'tasks', label: 'Tasks', icon: CheckSquare },
            { key: 'team', label: 'Team', icon: Users },
        ],
    },
    {
        label: 'Knowledge',
        items: [
            { key: 'vault', label: 'Vault', icon: Lock },
            { key: 'wiki', label: 'Wiki', icon: BookOpen },
            { key: 'library', label: 'Library', icon: Archive },
        ],
    },
    {
        label: 'Business',
        items: [
            { key: 'ideas', label: 'Ideas', icon: Lightbulb },
            { key: 'finance', label: 'Finance', icon: DollarSign },
            { key: 'reports', label: 'Reports', icon: BarChart2 },
        ],
    },
];
const SETTINGS_ITEM = { key: 'settings', label: 'Settings', icon: SettingsIcon };

// Utility Components
const CHIP_CLASS = 'inline-flex items-center px-[9px] py-[3px] rounded-[7px] text-[11.5px] font-semibold whitespace-nowrap';

const StatusBadge = ({ status }) => {
    const statusColors = {
        'todo': 'bg-[#F2F2F4] text-[#62626A]',
        'in-progress': 'bg-[#EEEEFF] text-[#4131B0]',
        'blocked': 'bg-[#FFEBE8] text-[#B71824]',
        'completed': 'bg-[#E3F8E9] text-[#005F2E]'
    };

    return (
        <span className={`${CHIP_CLASS} ${statusColors[status] || statusColors['todo']}`}>
            {status.replace('-', ' ')}
        </span>
    );
};

const PriorityBadge = ({ priority }) => {
    const priorityColors = {
        'low': 'bg-[#F2F2F4] text-[#62626A]',
        'medium': 'bg-[#FFEED6] text-[#874400]',
        'high': 'bg-[#FFEBE8] text-[#B71824]'
    };

    return (
        <span className={`${CHIP_CLASS} ${priorityColors[priority]}`}>
            {priority}
        </span>
    );
};

const RoleBadge = ({ role }) => {
    const roleColors = {
        'admin': 'bg-[#EEEEFF] text-[#4131B0]',
        'manager': 'bg-[#E3F8E9] text-[#005F2E]',
        'member': 'bg-[#F2F2F4] text-[#62626A]'
    };

    return (
        <span className={`${CHIP_CLASS} ${roleColors[role]}`}>
            {role}
        </span>
    );
};

const TagsList = ({ tags }) => {
    if (!tags || tags.length === 0) return null;

    return (
        <div className="flex flex-wrap gap-1.5">
            {tags.map((tag, index) => (
                <span key={index} className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-gray-100 text-gray-700">
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
const LoginForm = ({ onForgotPassword, onMustChangePassword }) => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [rememberMe, setRememberMe] = useState(true);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const { login } = useAuth();

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);
        setError('');

        try {
            const result = await login(email, password, rememberMe);
            if (result?.mustChangePassword) {
                onMustChangePassword(result.passwordResetToken);
            }
        } catch (error) {
            // A NetworkError means the request never reached the server at all (CORS block, offline,
            // timeout, DNS failure) - blaming the password for that is actively misleading and sends
            // people chasing the wrong problem (this exact confusion is why this distinction exists -
            // see ApiService.js's NetworkError).
            setError(
                error instanceof NetworkError
                    ? "Couldn't reach the server. Check your connection, or that this site is allowed to call the API (CORS)."
                    : 'Invalid email or password'
            );
        } finally {
            setLoading(false);
        }
    };

    const inputWrapClass = 'relative flex items-center rounded-[10px] border border-gray-200 bg-gray-50 transition-all focus-within:bg-white focus-within:border-blue-500 focus-within:ring-4 focus-within:ring-blue-500/10';
    const inputIconClass = 'h-[18px] w-[18px] ml-3.5 flex-shrink-0 text-gray-400 transition-colors peer-focus:text-blue-600';

    return (
        <div className="min-h-screen flex bg-gray-50">
            {/* Brand panel */}
            <div className="hidden lg:flex lg:w-[44%] relative overflow-hidden flex-col justify-between bg-gradient-to-br from-blue-900 via-blue-700 to-blue-600 px-14 py-14">
                <div
                    className="absolute inset-0 opacity-[0.07]"
                    style={{ backgroundImage: 'linear-gradient(rgba(255,255,255,0.6) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.6) 1px, transparent 1px)', backgroundSize: '42px 42px' }}
                />
                <div className="absolute -top-32 -right-32 h-[420px] w-[420px] rounded-full bg-blue-300/20 blur-3xl" />
                <div className="absolute -bottom-40 -left-24 h-[480px] w-[480px] rounded-full bg-blue-400/20 blur-3xl" />

                <div className="relative flex items-center gap-3">
                    <img src={khoiLogo} alt="Khoi" className="h-8 w-auto brightness-0 invert" />
                    <span className="text-white text-lg font-bold tracking-tight">Khoi Pro</span>
                </div>

                <div className="relative max-w-md">
                    <h1 className="text-white text-[34px] font-extrabold leading-[1.15] tracking-tight mb-4">
                        Where the whole company keeps its work in one place.
                    </h1>
                    <p className="text-blue-100/80 text-base leading-relaxed">
                        Projects, tasks, wiki, vault and finance &mdash; unified under one roof.
                    </p>

                    <div className="flex flex-col gap-3.5 mt-9">
                        {[
                            'Space-based permissions, inherited automatically',
                            'A secrets vault with a full audit trail',
                            'Timesheets, invoicing and reminders built in',
                        ].map((feature) => (
                            <div key={feature} className="flex items-center gap-3">
                                <div className="h-6 w-6 rounded-md bg-white/15 ring-1 ring-white/10 flex items-center justify-center flex-shrink-0">
                                    <CheckCircle className="h-3.5 w-3.5 text-white" />
                                </div>
                                <span className="text-blue-50/90 text-sm">{feature}</span>
                            </div>
                        ))}
                    </div>
                </div>

                <p className="relative text-blue-200/50 text-xs">&copy; 2026 Khoi. All rights reserved.</p>
            </div>

            {/* Form panel */}
            <div className="flex-1 flex items-center justify-center px-4 sm:px-6 lg:px-8 py-12">
                <div className="max-w-sm w-full">
                    <div className="lg:hidden flex flex-col items-center mb-8">
                        <img src={khoiLogo} alt="Khoi" className="h-10 w-auto mb-3" />
                    </div>

                    <div className="relative bg-white rounded-[20px] border border-gray-100 shadow-[0_2px_4px_rgba(16,24,40,0.04),0_20px_48px_-12px_rgba(16,24,40,0.14)] p-8 sm:p-10">
                        <div className="absolute inset-x-0 top-0 h-1 rounded-t-[20px] bg-gradient-to-r from-blue-600 via-blue-500 to-blue-400" />
                        <div className="hidden lg:flex h-11 w-11 rounded-xl bg-blue-50 items-center justify-center mb-5">
                            <Lock className="h-5 w-5 text-blue-600" />
                        </div>
                        <h2 className="text-[26px] font-bold text-gray-900 tracking-tight">
                            Sign in to Khoi Pro
                        </h2>
                        <p className="mt-1.5 text-sm text-gray-500">
                            Enter your credentials to access the project management system
                        </p>

                        <form className="mt-8 space-y-4" onSubmit={handleSubmit}>
                            <div className={inputWrapClass}>
                                <Mail className={inputIconClass} />
                                <input
                                    type="email"
                                    required
                                    value={email}
                                    onChange={(e) => setEmail(e.target.value)}
                                    className="peer w-full pl-2.5 pr-4 py-3 bg-transparent rounded-[10px] text-[15px] text-gray-900 placeholder-gray-400 focus:outline-none"
                                    placeholder="Email address"
                                />
                            </div>
                            <div className={inputWrapClass}>
                                <Lock className={inputIconClass} />
                                <input
                                    type={showPassword ? 'text' : 'password'}
                                    required
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    className="peer w-full pl-2.5 pr-2 py-3 bg-transparent rounded-[10px] text-[15px] text-gray-900 placeholder-gray-400 focus:outline-none"
                                    placeholder="Password"
                                />
                                <button
                                    type="button"
                                    onClick={() => setShowPassword((v) => !v)}
                                    className="mr-2.5 flex-shrink-0 text-gray-400 hover:text-gray-600 transition-colors p-1 -m-1"
                                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                                    tabIndex={-1}
                                >
                                    {showPassword ? <EyeOff className="h-[18px] w-[18px]" /> : <Eye className="h-[18px] w-[18px]" />}
                                </button>
                            </div>

                            <div className="flex items-center justify-between">
                                <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer select-none">
                                    <input
                                        type="checkbox"
                                        checked={rememberMe}
                                        onChange={(e) => setRememberMe(e.target.checked)}
                                        className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                    />
                                    Remember me for 30 days
                                </label>
                                <button
                                    type="button"
                                    onClick={onForgotPassword}
                                    className="text-sm font-medium text-blue-600 hover:text-blue-700 transition-colors"
                                >
                                    Forgot password?
                                </button>
                            </div>

                            {error && (
                                <div className="rounded-[10px] bg-red-50 border border-red-100 px-3.5 py-2.5 text-sm text-red-600 text-center">
                                    {error}
                                </div>
                            )}

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full flex items-center justify-center gap-2 py-3 px-4 rounded-[10px] text-[15px] font-semibold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 focus:outline-none focus:ring-4 focus:ring-blue-500/25 disabled:opacity-60 disabled:cursor-not-allowed transition-all shadow-sm hover:shadow-md"
                            >
                                {loading && (
                                    <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                                )}
                                {loading ? 'Signing in...' : 'Sign in'}
                            </button>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    );
};

// Main Dashboard Component
const ProjectManagementSystem = () => {
    const { user, logout } = useAuth();
    const toast = useToast();
    const [apiService] = useState(() => new ApiService());

    // Data state
    const [projects, setProjects] = useState([]);
    const [tasks, setTasks] = useState([]);
    const [teamMembers, setTeamMembers] = useState([]);
    const [showInactiveMembers, setShowInactiveMembers] = useState(false);
    const [notifications, setNotifications] = useState([]);
    const [recentExports, setRecentExports] = useState([]);
    const [reportSchedules, setReportSchedules] = useState([]);
    const [reportFormats, setReportFormats] = useState({ ProjectSummary: 'Csv', TeamPerformance: 'Csv', OverdueTasks: 'Csv' });
    const [widgetPrefs, setWidgetPrefs] = useState([]);
    const [pendingTimesheets, setPendingTimesheets] = useState([]);
    const [myTasks, setMyTasks] = useState([]);
    const [weeklyCompletion, setWeeklyCompletion] = useState([0, 0, 0, 0, 0, 0, 0]);
    const [activityFeed, setActivityFeed] = useState([]);
    const [dashboardMyTasksTab, setDashboardMyTasksTab] = useState('today');
    const [remindersActiveCount, setRemindersActiveCount] = useState(null);

    // Sidebar nav badge counts - fetched once here (not per-tab) so they're visible regardless of
    // which tab the user lands on, not just after visiting Reminders once.
    useEffect(() => {
        apiService.getReminderSummary().then((s) => setRemindersActiveCount(s?.totalActive ?? null)).catch(() => {});
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
    const [dashboardStats, setDashboardStats] = useState({
        totalProjects: 0,
        activeProjects: 0,
        totalTasks: 0,
        completedTasks: 0,
        inProgressTasks: 0,
        todoTasks: 0,
        blockedTasks: 0,
        overdueTasks: 0,
        completionRate: 0,
        activeProjectsDelta: null,
        totalTasksDelta: null,
        overdueTasksDelta: null,
        completionRateDelta: null
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

    // A shared link (Wiki "Share"/Library "Share" button) carries ?tab=wiki&spaceId=..&pageId=.. -
    // read once at mount, before the tab-restore fallback, so an incoming share link always wins over
    // wherever the recipient happened to be last. The link is a shortcut into the app, not a bypass -
    // whoever opens it must still log in and still goes through the normal Space permission checks.
    const [deepLink] = useState(() => {
        const params = new URLSearchParams(window.location.search);
        const tab = params.get('tab');
        if (!tab) return null;
        return {
            tab,
            spaceId: params.get('spaceId'),
            pageId: params.get('pageId'),
            fileId: params.get('fileId'),
        };
    });

    // UI state
    // Restores the tab the user was on before an auto-logout (session expiry) reloads the page -
    // AuthContext.logout() clears this on an explicit manual logout, so that path always starts fresh
    // at the dashboard instead of jumping back to wherever the user happened to be. An incoming share
    // link (deepLink) takes priority over both.
    const [activeTab, setActiveTab] = useState(() => deepLink?.tab || localStorage.getItem('khoi_last_tab') || 'dashboard');

    useEffect(() => {
        localStorage.setItem('khoi_last_tab', activeTab);
    }, [activeTab]);

    // Consume the share link's query string once so a later manual refresh doesn't keep re-forcing
    // navigation back to the shared item over whatever the user has since clicked into.
    useEffect(() => {
        if (deepLink) {
            window.history.replaceState({}, '', window.location.pathname);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
    const [searchTerm, setSearchTerm] = useState('');
    const searchInputRef = useRef(null);
    const searchBoxRef = useRef(null);
    const [globalSearchResults, setGlobalSearchResults] = useState(null);
    const [globalSearchOpen, setGlobalSearchOpen] = useState(false);
    const [globalSearching, setGlobalSearching] = useState(false);
    const globalSearchDebounceRef = useRef(null);

    // Cmd/Ctrl+K focuses the global search input, matching the shortcut hint shown inside it.
    useEffect(() => {
        const handleKeyDown = (e) => {
            if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
                e.preventDefault();
                searchInputRef.current?.focus();
            }
        };
        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, []);

    // Debounced global search across Projects/Tasks/People - fires GLOBAL_SEARCH_DEBOUNCE_MS after
    // typing stops, same pattern as Wiki's full-text search. Separate from searchTerm's other role of
    // filtering the Tasks tab's own table (loadTasks below) - that keeps working exactly as before;
    // this just layers a results dropdown on top of the same input.
    useEffect(() => {
        if (globalSearchDebounceRef.current) clearTimeout(globalSearchDebounceRef.current);
        const q = searchTerm.trim();
        if (q.length < 2) {
            setGlobalSearchResults(null);
            setGlobalSearchOpen(false);
            return;
        }
        globalSearchDebounceRef.current = setTimeout(async () => {
            setGlobalSearching(true);
            try {
                const results = await apiService.globalSearch(q);
                setGlobalSearchResults(results);
                setGlobalSearchOpen(true);
            } catch (error) {
                reportApiError(toast, error, 'Search failed.');
            } finally {
                setGlobalSearching(false);
            }
        }, 350);
        return () => clearTimeout(globalSearchDebounceRef.current);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [searchTerm]);

    useEffect(() => {
        const handleClickOutside = (e) => {
            if (searchBoxRef.current && !searchBoxRef.current.contains(e.target)) setGlobalSearchOpen(false);
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const handleGlobalSearchResultClick = (category, item) => {
        setGlobalSearchOpen(false);
        setSearchTerm('');
        if (category === 'projects') setActiveTab('projects');
        else if (category === 'tasks') setActiveTab('tasks');
        else if (category === 'people') setActiveTab('team');
    };

    const [filterStatus, setFilterStatus] = useState('all');
    const [showAddProject, setShowAddProject] = useState(false);
    // null = the "Add Project" modal is creating a new project; a project id = it's editing that
    // project instead (opened via the card's Edit3 button - see openEditProject/handleAddProject).
    const [editingProjectId, setEditingProjectId] = useState(null);
    const [showAddTask, setShowAddTask] = useState(false);
    const [showAddMember, setShowAddMember] = useState(false);
    const [teamView, setTeamView] = useState('list'); // 'list' | 'orgchart'
    // A member id = the Edit Member modal is open for that member; null = closed.
    const [editingMemberId, setEditingMemberId] = useState(null);
    const [editMemberForm, setEditMemberForm] = useState({ name: '', email: '', position: '', managerId: '' });
    const [savingMemberEdit, setSavingMemberEdit] = useState(false);
    const [showNotifications, setShowNotifications] = useState(false);
    const [showUserMenu, setShowUserMenu] = useState(false);
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    // Form states
    const emptyProjectForm = {
        name: '',
        description: '',
        priority: 'medium',
        status: 'active', // only sent on update - CreateProjectDto has no Status (server defaults it)
        startDate: '',
        endDate: '',
        teamMemberIds: [],
        tags: ''
    };
    const [newProject, setNewProject] = useState(emptyProjectForm);

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
        managerId: ''
    });
    // createUser can legitimately take a while (it synchronously sends the temp-password email - see
    // ApiService.createUser's comment on its longer timeout budget), so this needs its own visible
    // "in flight" state rather than a bare disabled button with no explanation of the wait.
    const [savingMember, setSavingMember] = useState(false);
    const [savingProject, setSavingProject] = useState(false);
    const [savingTask, setSavingTask] = useState(false);

    // Data loading functions
    const loadDashboardData = async () => {
        setLoading(prev => ({ ...prev, dashboard: true }));
        try {
            const canApproveTimesheets = hasPermission(user?.permissions, 'timesheets.approve');
            const [stats, recentTasks, notifs, widgetPrefsResult, timesheetsResult, myTasksResult, weeklyCompletionResult, activityResult] = await Promise.all([
                apiService.getDashboardStats(),
                apiService.getTasks({ limit: 5 }),
                apiService.getNotifications(),
                apiService.getMyDashboardWidgetPreferences(),
                apiService.getTimesheets(undefined, canApproveTimesheets ? 'Submitted' : undefined),
                apiService.getTasks({ assignedToId: user?.id }),
                apiService.getDashboardWeeklyCompletion(),
                apiService.getDashboardActivity(),
            ]);

            setDashboardStats(stats || dashboardStats);
            setTasks(recentTasks || []);
            setNotifications(notifs || []);
            setWidgetPrefs(widgetPrefsResult || []);
            setPendingTimesheets(
                canApproveTimesheets
                    ? (timesheetsResult || [])
                    : (timesheetsResult || []).filter((t) => t.status === 'Draft' || t.status === 'Rejected')
            );
            setMyTasks(myTasksResult || []);
            setWeeklyCompletion(weeklyCompletionResult || [0, 0, 0, 0, 0, 0, 0]);
            setActivityFeed(activityResult || []);
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
            const members = await apiService.getUsers(showInactiveMembers);
            setTeamMembers(members || []);
            setErrors(prev => ({ ...prev, teamMembers: null }));
        } catch (error) {
            setErrors(prev => ({ ...prev, teamMembers: error.message }));
        } finally {
            setLoading(prev => ({ ...prev, teamMembers: false }));
        }
    };

    // Reloads whenever the Dashboard tab becomes active (not just on first mount) - otherwise a
    // widget preference change made in Settings would never show up until a full page reload, since
    // widgetPrefs/pendingTimesheets/etc. are only fetched here. Matches the existing reload-on-tab-
    // switch pattern already used for Projects/Tasks/Team below.
    useEffect(() => {
        if (activeTab === 'dashboard') {
            loadDashboardData();
        } else if (activeTab === 'projects') {
            loadProjects();
        } else if (activeTab === 'tasks') {
            loadTasks();
            apiService.getDashboardStats().then((stats) => stats && setDashboardStats((prev) => ({ ...prev, ...stats })));
        } else if (activeTab === 'team') {
            loadTeamMembers();
        } else if (activeTab === 'reports') {
            loadReportsMeta();
        }
    }, [activeTab]);

    // Loaded once up front (not gated behind visiting the Team tab) - teamMembers backs the task
    // assignee dropdown and the project team picker too, both reachable from any tab, so those must
    // not render empty just because nobody has opened Team yet this session.
    useEffect(() => {
        loadTeamMembers();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
        if (activeTab === 'tasks') {
            loadTasks();
        }
    }, [filterStatus, searchTerm]);

    useEffect(() => {
        if (activeTab === 'team') {
            loadTeamMembers();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [showInactiveMembers]);

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
    const openEditProject = (project) => {
        setEditingProjectId(project.id);
        setNewProject({
            name: project.name,
            description: project.description || '',
            priority: project.priority,
            status: project.status,
            // <input type="date"> needs "YYYY-MM-DD" - the API returns a full ISO datetime string.
            startDate: (project.startDate || '').slice(0, 10),
            endDate: (project.endDate || '').slice(0, 10),
            teamMemberIds: project.teamMembers?.map(m => m.id) || [],
            tags: (project.tags || []).join(', ')
        });
        setShowAddProject(true);
    };

    const closeProjectModal = () => {
        setShowAddProject(false);
        setEditingProjectId(null);
        setNewProject(emptyProjectForm);
    };

    const handleAddProject = async (e) => {
        e.preventDefault();

        const validationErrors = validateProject(newProject);
        if (hasErrors(validationErrors)) {
            toast.error(Object.values(validationErrors)[0]);
            return;
        }

        const isEditing = editingProjectId !== null;
        setSavingProject(true);
        try {
            const tags = newProject.tags.split(',').map(tag => tag.trim()).filter(tag => tag);

            if (isEditing) {
                await apiService.updateProject(editingProjectId, {
                    name: newProject.name,
                    description: newProject.description,
                    priority: newProject.priority,
                    status: newProject.status,
                    startDate: newProject.startDate,
                    endDate: newProject.endDate,
                    teamMemberIds: newProject.teamMemberIds,
                    tags
                });
            } else {
                await apiService.createProject({
                    name: newProject.name,
                    description: newProject.description,
                    priority: newProject.priority,
                    startDate: newProject.startDate,
                    endDate: newProject.endDate,
                    teamMemberIds: newProject.teamMemberIds,
                    tags
                });
            }

            closeProjectModal();
            await loadProjects();
            toast.success(isEditing ? 'Project updated successfully!' : 'Project created successfully!');
        } catch (error) {
            reportApiError(toast, error, `Error ${isEditing ? 'updating' : 'creating'} project.`);
        } finally {
            setSavingProject(false);
        }
    };

    const handleAddTask = async (e) => {
        e.preventDefault();

        const validationErrors = validateTask(newTask);
        if (hasErrors(validationErrors)) {
            toast.error(Object.values(validationErrors)[0]);
            return;
        }

        setSavingTask(true);
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
            toast.success('Task created successfully!');
        } catch (error) {
            reportApiError(toast, error, 'Error creating task.');
        } finally {
            setSavingTask(false);
        }
    };

    const handleAddMember = async (e) => {
        e.preventDefault();
        if (!hasPermission(user?.permissions, 'users.create')) {
            toast.error('You do not have permission to add team members');
            return;
        }

        const validationErrors = validateTeamMember(newMember);
        if (hasErrors(validationErrors)) {
            toast.error(Object.values(validationErrors)[0]);
            return;
        }

        setSavingMember(true);
        try {
            const memberData = {
                name: newMember.name,
                role: newMember.role,
                position: newMember.position,
                email: newMember.email,
                managerId: newMember.managerId ? Number(newMember.managerId) : null
            };

            await apiService.createUser(memberData);

            setNewMember({
                name: '',
                role: 'member',
                position: '',
                email: '',
                managerId: ''
            });
            setShowAddMember(false);

            await loadTeamMembers();
            toast.success('Team member added. A temporary password has been emailed to them.');
        } catch (error) {
            // A NetworkError here (timeout/offline) means the client gave up waiting, not that the
            // server did - user creation can legitimately outlive this request's timeout (see
            // createUser's comment in ApiService). Re-fetch so the list reflects what actually
            // happened server-side instead of leaving a stale "it failed" impression when it didn't.
            if (error instanceof NetworkError) {
                await loadTeamMembers();
                toast.error('Could not confirm the member was added - check the list below before retrying.');
            } else {
                reportApiError(toast, error, 'Error adding team member.');
            }
        } finally {
            setSavingMember(false);
        }
    };

    const handleResendTempPassword = async (member) => {
        try {
            await apiService.resendTempPassword(member.id);
            toast.success(`Temporary password resent to ${member.email}.`);
        } catch (error) {
            reportApiError(toast, error, 'Error resending temporary password.');
        }
    };

    const openEditMember = (member) => {
        setEditingMemberId(member.id);
        setEditMemberForm({
            name: member.name,
            email: member.email,
            position: member.position,
            managerId: member.managerId ? String(member.managerId) : ''
        });
    };

    const handleSaveMemberEdit = async (e) => {
        e.preventDefault();
        const validationErrors = validateTeamMember(editMemberForm);
        if (hasErrors(validationErrors)) {
            toast.error(Object.values(validationErrors)[0]);
            return;
        }

        setSavingMemberEdit(true);
        try {
            await apiService.updateUser(editingMemberId, {
                name: editMemberForm.name,
                email: editMemberForm.email,
                position: editMemberForm.position,
                managerId: editMemberForm.managerId ? Number(editMemberForm.managerId) : null
            });
            setEditingMemberId(null);
            await loadTeamMembers();
            toast.success('Team member updated.');
        } catch (error) {
            reportApiError(toast, error, 'Error updating team member.');
        } finally {
            setSavingMemberEdit(false);
        }
    };

    const handleToggleMemberActive = async (member) => {
        const deactivating = member.isActive;
        if (deactivating && !window.confirm(`Lock ${member.name} out of the system? They won't be able to sign in until reactivated.`)) {
            return;
        }

        try {
            if (deactivating) {
                await apiService.deactivateUser(member.id);
                toast.success(`${member.name} has been locked out.`);
            } else {
                await apiService.reactivateUser(member.id);
                toast.success(`${member.name}'s access has been restored.`);
            }
            await loadTeamMembers();
        } catch (error) {
            reportApiError(toast, error, `Error ${deactivating ? 'locking out' : 'reactivating'} team member.`);
        }
    };

    const updateTaskStatus = async (taskId, newStatus) => {
        try {
            await apiService.updateTaskStatus(taskId, newStatus);

            setTasks(prevTasks =>
                prevTasks.map(task =>
                    task.id === taskId ? { ...task, status: newStatus } : task
                )
            );

            if (newStatus === 'completed') {
                toast.success('Task marked as completed!');
            }
        } catch (error) {
            reportApiError(toast, error, 'Error updating task.');
        }
    };

    const deleteTask = async (taskId) => {
        if (!hasPermission(user?.permissions, 'tasks.delete')) {
            toast.error('You do not have permission to delete tasks');
            return;
        }

        if (!window.confirm('Are you sure you want to delete this task?')) {
            return;
        }

        try {
            await apiService.deleteTask(taskId);
            setTasks(prevTasks => prevTasks.filter(task => task.id !== taskId));
            toast.success('Task deleted successfully!');
        } catch (error) {
            reportApiError(toast, error, 'Error deleting task.');
        }
    };

    const deleteProject = async (projectId) => {
        if (!hasPermission(user?.permissions, 'projects.delete')) {
            toast.error('You do not have permission to delete projects');
            return;
        }

        if (!window.confirm('Are you sure you want to delete this project? This will also delete all associated tasks.')) {
            return;
        }

        try {
            await apiService.deleteProject(projectId);
            setProjects(prevProjects => prevProjects.filter(project => project.id !== projectId));
            setTasks(prevTasks => prevTasks.filter(task => task.projectId !== projectId));
            toast.success('Project deleted successfully!');
        } catch (error) {
            reportApiError(toast, error, 'Error deleting project.');
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

    const loadReportsMeta = async () => {
        try {
            const [exports, schedules] = await Promise.all([
                apiService.getRecentReportExports(),
                apiService.getReportSchedules(),
            ]);
            setRecentExports(exports || []);
            setReportSchedules(schedules || []);
        } catch (error) {
            setErrors(prev => ({ ...prev, reports: error.message }));
        }
    };

    // Tracked per report type/schedule (not one shared reports-tab-wide flag) so generating one
    // report's PDF doesn't visually disable every other card's buttons too.
    const [generatingReportType, setGeneratingReportType] = useState(null);
    const [schedulingReportType, setSchedulingReportType] = useState(null);
    const [cancellingScheduleId, setCancellingScheduleId] = useState(null);

    const generateReport = async (reportType) => {
        setGeneratingReportType(reportType);
        try {
            await apiService.exportReport(reportType, reportFormats[reportType] || 'Csv');
            await loadReportsMeta();
            toast.success('Report generated and downloaded.');
        } catch (error) {
            reportApiError(toast, error, 'Error generating report.');
        } finally {
            setGeneratingReportType(null);
        }
    };

    const scheduleReport = async (reportType) => {
        setSchedulingReportType(reportType);
        try {
            await apiService.createReportSchedule({ reportType, format: reportFormats[reportType] || 'Csv' });
            await loadReportsMeta();
            toast.success('Weekly schedule created.');
        } catch (error) {
            reportApiError(toast, error, 'Error scheduling report.');
        } finally {
            setSchedulingReportType(null);
        }
    };

    const cancelReportSchedule = async (id) => {
        setCancellingScheduleId(id);
        try {
            await apiService.deleteReportSchedule(id);
            await loadReportsMeta();
            toast.success('Schedule cancelled.');
        } catch (error) {
            reportApiError(toast, error, 'Error cancelling schedule.');
        } finally {
            setCancellingScheduleId(null);
        }
    };

    const isTabActive = (key) => activeTab === key;

    const navButtonClass = (key) =>
        `w-full flex items-center gap-3 px-3 py-2 rounded-[10px] text-sm font-medium transition-colors ${isTabActive(key)
            ? 'bg-blue-50 text-blue-700 font-semibold'
            : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
        }`;

    const navCounts = {
        reminders: remindersActiveCount,
        projects: dashboardStats.totalProjects,
        tasks: dashboardStats.totalTasks,
    };

    const renderNavGroups = (onNavigate) => (
        <>
            {NAV_GROUPS.map((group) => (
                <div key={group.label || group.items[0].key} className="mb-1">
                    {group.label && (
                        <p className="px-5 pt-4 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-gray-400">
                            {group.label}
                        </p>
                    )}
                    <div className="px-3 space-y-0.5">
                        {group.items.map(({ key, label, icon: Icon }) => {
                            const count = navCounts[key];
                            return (
                                <button key={key} onClick={() => onNavigate(key)} className={navButtonClass(key)}>
                                    <Icon className="h-[18px] w-[18px] flex-shrink-0" />
                                    {label}
                                    {!!count && (
                                        <span className="ml-auto text-xs font-medium text-gray-400">{count}</span>
                                    )}
                                </button>
                            );
                        })}
                    </div>
                </div>
            ))}
        </>
    );

    return (
        <div className="min-h-screen flex bg-gray-50">
            {/* Desktop sidebar */}
            <aside className="hidden md:flex w-64 flex-shrink-0 flex-col bg-white border-r border-gray-200 h-screen sticky top-0 overflow-y-auto">
                <div className="flex items-center gap-2.5 h-16 px-5 border-b border-gray-100 flex-shrink-0">
                    <div className="h-9 w-9 rounded-lg bg-blue-600 flex items-center justify-center text-white font-bold text-base flex-shrink-0">K</div>
                    <span className="text-base font-bold text-gray-900 tracking-tight">Khoi Pro</span>
                </div>

                <nav className="flex-1 py-3">
                    {renderNavGroups(setActiveTab)}
                </nav>

                <div className="border-t border-gray-100 py-3 flex-shrink-0">
                    <div className="px-3">
                        <button onClick={() => setActiveTab(SETTINGS_ITEM.key)} className={navButtonClass(SETTINGS_ITEM.key)}>
                            <SettingsIcon className="h-[18px] w-[18px] flex-shrink-0" />
                            {SETTINGS_ITEM.label}
                        </button>
                    </div>
                    <div className="flex items-center gap-2.5 px-5 pt-3">
                        <div className={`h-8 w-8 ${getAvatarColor(user?.name)} rounded-full flex items-center justify-center text-white text-xs font-semibold flex-shrink-0`}>
                            {(user?.name || '?').split(' ').filter(Boolean).map(n => n[0]).slice(0, 2).join('').toUpperCase()}
                        </div>
                        <div className="min-w-0">
                            <p className="text-sm font-medium text-gray-900 truncate leading-tight">{user?.name}</p>
                            <p className="text-xs text-gray-400 truncate capitalize">{user?.role}</p>
                        </div>
                    </div>
                </div>
            </aside>

            {/* Main column */}
            <div className="flex-1 flex flex-col min-w-0">
                {/* Top bar */}
                <header className="bg-white border-b border-gray-200 sticky top-0 z-40 flex-shrink-0">
                    <div className="flex justify-between items-center h-16 px-4 sm:px-6">
                        <div className="flex items-center md:hidden">
                            <div className="h-8 w-8 rounded-lg bg-blue-600 flex items-center justify-center text-white font-bold text-sm flex-shrink-0 mr-2">K</div>
                            <span className="text-lg font-bold text-gray-900 tracking-tight">Khoi Pro</span>
                        </div>

                        <div className="hidden md:block flex-1 max-w-md">
                            <div className="relative" ref={searchBoxRef}>
                                <Search className="h-4 w-4 absolute left-4 top-1/2 transform -translate-y-1/2 text-gray-400" />
                                <input
                                    ref={searchInputRef}
                                    type="text"
                                    placeholder="Search projects, tasks, people"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                    onFocus={() => { if (globalSearchResults) setGlobalSearchOpen(true); }}
                                    className="w-full max-w-xs pl-10 pr-14 py-2.5 bg-gray-50 border border-transparent rounded-[10px] text-sm placeholder-gray-400 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all"
                                />
                                <span className="hidden lg:inline-flex absolute right-3 top-1/2 -translate-y-1/2 items-center px-1.5 py-0.5 rounded-md border border-gray-200 bg-white text-[11px] font-mono font-medium text-gray-400">
                                    &#8984;K
                                </span>

                                {globalSearchOpen && (() => {
                                    const groups = globalSearchResults ? [
                                        { key: 'projects', label: 'Projects', items: globalSearchResults.projects },
                                        { key: 'tasks', label: 'Tasks', items: globalSearchResults.tasks },
                                        { key: 'people', label: 'People', items: globalSearchResults.people },
                                    ].filter((g) => g.items.length > 0) : [];
                                    return (
                                        <div className="absolute z-30 mt-1 w-full max-w-xs bg-white border border-gray-100 rounded-xl shadow-lg max-h-96 overflow-y-auto">
                                            {globalSearching && <div className="p-3 text-sm text-gray-400">Searching...</div>}
                                            {!globalSearching && groups.length === 0 && (
                                                <div className="p-3 text-sm text-gray-400">No matches for "{searchTerm}".</div>
                                            )}
                                            {!globalSearching && groups.map((group) => (
                                                <div key={group.key} className="py-1.5">
                                                    <div className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wider text-gray-400">{group.label}</div>
                                                    {group.items.map((item) => (
                                                        <button
                                                            key={item.id}
                                                            onClick={() => handleGlobalSearchResultClick(group.key, item)}
                                                            className="w-full text-left px-3 py-2 hover:bg-gray-50/80 transition-colors flex items-center justify-between"
                                                        >
                                                            <span className="text-sm text-gray-900 truncate">{item.title}</span>
                                                            {item.subtitle && <span className="text-xs text-gray-400 ml-2 flex-shrink-0 capitalize">{item.subtitle}</span>}
                                                        </button>
                                                    ))}
                                                </div>
                                            ))}
                                        </div>
                                    );
                                })()}
                            </div>
                        </div>

                        <div className="hidden md:flex items-center gap-3">
                            <button
                                onClick={() => setShowAddTask(true)}
                                className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
                            >
                                <Plus className="h-4 w-4" />
                                New task
                            </button>

                            {/* Notifications */}
                            <div className="relative">
                                <button
                                    onClick={() => { setShowNotifications(!showNotifications); setShowUserMenu(false); }}
                                    className="relative h-[34px] w-[34px] flex items-center justify-center rounded-[10px] border border-gray-200 text-gray-500 hover:text-gray-700 hover:bg-gray-50 transition-colors"
                                >
                                    <Bell className="h-5 w-5" />
                                    {notifications.filter(n => !n.isRead).length > 0 && (
                                        <span className="absolute top-1.5 right-1.5 h-4 w-4 bg-red-500 text-white text-[10px] font-semibold rounded-full flex items-center justify-center ring-2 ring-white">
                                            {notifications.filter(n => !n.isRead).length}
                                        </span>
                                    )}
                                </button>

                                {showNotifications && (
                                    <div className="absolute right-0 mt-2 w-80 bg-white rounded-xl shadow-xl border border-gray-100 z-50 overflow-hidden animate-fade-in">
                                        <div className="p-4 border-b border-gray-100">
                                            <h3 className="font-semibold text-gray-900">Notifications</h3>
                                        </div>
                                        <div className="max-h-64 overflow-y-auto">
                                            {notifications.length === 0 ? (
                                                <div className="p-6 text-center text-gray-400 text-sm">
                                                    No notifications
                                                </div>
                                            ) : (
                                                notifications.slice(0, 5).map((notification) => (
                                                    <div
                                                        key={notification.id}
                                                        className={`p-4 border-b border-gray-50 cursor-pointer hover:bg-gray-50 transition-colors ${!notification.isRead ? 'bg-blue-50/60' : ''}`}
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
                            <div className="relative">
                                <button
                                    onClick={() => { setShowUserMenu(!showUserMenu); setShowNotifications(false); }}
                                    className="flex items-center space-x-2 pl-1.5 pr-2 py-1.5 rounded-lg hover:bg-gray-100 transition-colors"
                                >
                                    <div className={`h-8 w-8 ${getAvatarColor(user?.name)} rounded-full flex items-center justify-center text-white text-xs font-semibold`}>
                                        {(user?.name || '?').split(' ').filter(Boolean).map(n => n[0]).slice(0, 2).join('').toUpperCase()}
                                    </div>
                                    <div className="text-sm text-left hidden lg:block">
                                        <p className="font-medium text-gray-900 leading-tight">{user?.name}</p>
                                        <RoleBadge role={user?.role} />
                                    </div>
                                    <ChevronDown className={`h-4 w-4 text-gray-400 transition-transform ${showUserMenu ? 'rotate-180' : ''}`} />
                                </button>

                                {showUserMenu && (
                                    <div className="absolute right-0 mt-2 w-56 bg-white rounded-xl shadow-xl border border-gray-100 z-50 overflow-hidden animate-fade-in">
                                        <div className="px-4 py-3 border-b border-gray-100">
                                            <p className="font-medium text-gray-900 text-sm">{user?.name}</p>
                                            <p className="text-xs text-gray-500 truncate">{user?.email}</p>
                                            <div className="mt-1.5"><RoleBadge role={user?.role} /></div>
                                        </div>
                                        <button
                                            onClick={logout}
                                            className="w-full flex items-center px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 transition-colors"
                                        >
                                            <LogOut className="h-4 w-4 mr-2" />
                                            Logout
                                        </button>
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* Mobile menu button */}
                        <div className="md:hidden">
                            <button
                                onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                                className="p-2 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
                            >
                                {mobileMenuOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
                            </button>
                        </div>
                    </div>
                </header>

                {/* Mobile Navigation Drawer */}
                {mobileMenuOpen && (
                    <div className="md:hidden fixed inset-0 z-40 flex">
                        <div
                            className="fixed inset-0 bg-black/40"
                            onClick={() => setMobileMenuOpen(false)}
                        />
                        <div className="relative w-72 max-w-[80%] h-full bg-white shadow-xl overflow-y-auto animate-slide-up">
                            <div className="flex items-center gap-2.5 h-16 px-5 border-b border-gray-100">
                                <div className="h-9 w-9 rounded-lg bg-blue-600 flex items-center justify-center text-white font-bold text-base flex-shrink-0">K</div>
                                <span className="text-base font-bold text-gray-900 tracking-tight">Khoi Pro</span>
                            </div>
                            <nav className="py-3">
                                {renderNavGroups((key) => { setActiveTab(key); setMobileMenuOpen(false); })}
                                <div className="border-t border-gray-100 mt-2 pt-3 px-3 space-y-0.5">
                                    <button
                                        onClick={() => { setActiveTab(SETTINGS_ITEM.key); setMobileMenuOpen(false); }}
                                        className={navButtonClass(SETTINGS_ITEM.key)}
                                    >
                                        <SettingsIcon className="h-[18px] w-[18px] flex-shrink-0" />
                                        {SETTINGS_ITEM.label}
                                    </button>
                                    <button
                                        onClick={logout}
                                        className="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium text-red-600 hover:bg-red-50 transition-colors"
                                    >
                                        <LogOut className="h-[18px] w-[18px] flex-shrink-0" />
                                        Logout
                                    </button>
                                </div>
                            </nav>
                        </div>
                    </div>
                )}

                {/* Main Content */}
                <main className="flex-1 overflow-y-auto p-6 sm:p-8">
                {/* Dashboard Tab */}
                {activeTab === 'dashboard' && (
                    <div className="space-y-6">
                        <div>
                            <h2 className="text-[27px] font-bold text-gray-900 tracking-tight">
                                {(() => {
                                    const hour = new Date().getHours();
                                    const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
                                    const firstName = (user?.name || '').split(' ')[0];
                                    return firstName ? `${greeting}, ${firstName}` : greeting;
                                })()}
                            </h2>
                            <p className="text-gray-500">Overview of all projects and tasks</p>
                        </div>

                        {loading.dashboard && <LoadingSpinner text="Loading dashboard..." />}

                        {errors.dashboard && (
                            <ErrorMessage message={errors.dashboard} onRetry={loadDashboardData} />
                        )}

                        {!loading.dashboard && !errors.dashboard && (() => {
                            // Widgets are ordered/shown per the user's own Settings > Dashboard Widgets
                            // choices (see DashboardWidgetSettings.js), constrained to whatever the admin
                            // has left enabled in the company-wide allow-list. Stat cards stay together in
                            // one grid (only which cards appear is configurable, not full interleaving with
                            // the full-width sections below) - a deliberate scope boundary, not an oversight.
                            const DeltaBadge = ({ value, invert = false, neutral = false }) => {
                                if (value === null || value === undefined || value === 0) return null;
                                const sign = value > 0 ? '+' : '';
                                const toneClass = neutral
                                    ? 'text-gray-500 bg-gray-100'
                                    : (invert ? value < 0 : value > 0) ? 'text-green-700 bg-green-50' : 'text-red-700 bg-red-50';
                                return (
                                    <span className={`text-xs font-semibold px-1.5 py-0.5 rounded-md ${toneClass}`}>
                                        {sign}{Math.round(value)}
                                    </span>
                                );
                            };

                            const STAT_CARDS = {
                                total_projects: (
                                    <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
                                        <div className="flex items-center">
                                            <div className="bg-blue-50 rounded-lg p-3 mr-3 flex-shrink-0">
                                                <CheckCircle className="h-6 w-6 text-blue-600" />
                                            </div>
                                            <div>
                                                <p className="text-sm font-medium text-gray-500">Total Projects</p>
                                                <p className="text-2xl font-bold text-gray-900">{dashboardStats.totalProjects}</p>
                                            </div>
                                        </div>
                                    </div>
                                ),
                                active_projects: (
                                    <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
                                        <div className="flex items-center">
                                            <div className="bg-green-50 rounded-lg p-3 mr-3 flex-shrink-0">
                                                <Clock className="h-6 w-6 text-green-600" />
                                            </div>
                                            <div>
                                                <p className="text-sm font-medium text-gray-500">Active Projects</p>
                                                <div className="flex items-baseline gap-2">
                                                    <p className="text-2xl font-bold text-gray-900">{dashboardStats.activeProjects}</p>
                                                    <DeltaBadge value={dashboardStats.activeProjectsDelta} />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                ),
                                total_tasks: (
                                    <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
                                        <div className="flex items-center">
                                            <div className="bg-amber-50 rounded-lg p-3 mr-3 flex-shrink-0">
                                                <AlertCircle className="h-6 w-6 text-amber-600" />
                                            </div>
                                            <div>
                                                <p className="text-sm font-medium text-gray-500">Total Tasks</p>
                                                <div className="flex items-baseline gap-2">
                                                    <p className="text-2xl font-bold text-gray-900">{dashboardStats.totalTasks}</p>
                                                    <DeltaBadge value={dashboardStats.totalTasksDelta} neutral />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                ),
                                overdue_tasks: (
                                    <div className={`bg-white p-5 rounded-[14px] border shadow-sm ${dashboardStats.overdueTasks > 0 ? 'border-[#DB4241]/30' : 'border-gray-100'}`}>
                                        <div className="flex items-center justify-between">
                                            <div className="flex items-center">
                                                <div className="bg-red-50 rounded-lg p-3 mr-3 flex-shrink-0">
                                                    <Flag className="h-6 w-6 text-red-600" />
                                                </div>
                                                <div>
                                                    <p className={`text-sm font-medium ${dashboardStats.overdueTasks > 0 ? 'text-red-600' : 'text-gray-500'}`}>Overdue Tasks</p>
                                                    <div className="flex items-baseline gap-2">
                                                        <p className="text-2xl font-bold text-gray-900">{dashboardStats.overdueTasks}</p>
                                                        <DeltaBadge value={dashboardStats.overdueTasksDelta} invert />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        {dashboardStats.overdueTasks > 0 && (
                                            <button
                                                onClick={() => { setActiveTab('tasks'); setFilterStatus('overdue'); }}
                                                className="mt-3 inline-flex items-center gap-1 text-sm font-semibold text-red-600 hover:text-red-700 transition-colors"
                                            >
                                                Review now <ArrowRight className="h-3.5 w-3.5" />
                                            </button>
                                        )}
                                    </div>
                                ),
                                completion_rate: (
                                    <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
                                        <div className="flex items-center">
                                            <div className="bg-purple-50 rounded-lg p-3 mr-3 flex-shrink-0">
                                                <Users className="h-6 w-6 text-purple-600" />
                                            </div>
                                            <div>
                                                <p className="text-sm font-medium text-gray-500">Completion Rate</p>
                                                <div className="flex items-baseline gap-2">
                                                    <p className="text-2xl font-bold text-gray-900">{Math.round(dashboardStats.completionRate)}%</p>
                                                    <DeltaBadge value={dashboardStats.completionRateDelta} />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                ),
                            };

                            const visibleKeys = widgetPrefs.filter((w) => w.isVisible).map((w) => w.widgetKey);
                            const isVisible = (key) => visibleKeys.includes(key) || widgetPrefs.length === 0;
                            const orderedStatKeys = widgetPrefs.length > 0
                                ? widgetPrefs.map((w) => w.widgetKey).filter((k) => STAT_CARDS[k] && isVisible(k))
                                : Object.keys(STAT_CARDS);
                            const sectionOrder = widgetPrefs.length > 0
                                ? widgetPrefs.map((w) => w.widgetKey)
                                : ['my_tasks', 'recent_tasks', 'recent_mentions', 'pending_timesheets', 'weekly_completion_chart', 'activity_feed'];

                            const myTaskGroups = {
                                today: myTasks.filter((t) => t.status !== 'completed' && t.dueDate && new Date(t.dueDate).toDateString() === new Date().toDateString()),
                                upcoming: myTasks.filter((t) => t.status !== 'completed' && (!t.dueDate || new Date(t.dueDate).toDateString() !== new Date().toDateString())),
                                done: myTasks.filter((t) => t.status === 'completed'),
                            };

                            const ACTIVITY_VERBS = {
                                Created: 'created',
                                Completed: 'completed',
                                MarkedPaid: 'marked paid',
                                StatusChanged: 'updated the status of',
                            };
                            const formatRelativeTime = (iso) => {
                                const diffMs = Date.now() - new Date(iso).getTime();
                                const mins = Math.round(diffMs / 60000);
                                if (mins < 1) return 'just now';
                                if (mins < 60) return `${mins}m ago`;
                                const hours = Math.round(mins / 60);
                                if (hours < 24) return `${hours}h ago`;
                                return `${Math.round(hours / 24)}d ago`;
                            };

                            const recentMentions = notifications.filter((n) => n.type === 'mention').slice(0, 5);

                            return (
                                <>
                                    {orderedStatKeys.length > 0 && (
                                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-6">
                                            {orderedStatKeys.map((key) => (
                                                <React.Fragment key={key}>{STAT_CARDS[key]}</React.Fragment>
                                            ))}
                                        </div>
                                    )}

                                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
                                    {sectionOrder.filter((k) => k === 'recent_tasks' && isVisible(k)).map(() => (
                                        <div key="recent_tasks" className="bg-white rounded-2xl border border-gray-100 shadow-sm">
                                            <div className="px-6 py-4 border-b border-gray-100">
                                                <h3 className="text-base font-semibold text-gray-900">Recent Tasks</h3>
                                            </div>
                                            <div className="divide-y divide-gray-100">
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
                                    ))}

                                    {sectionOrder.filter((k) => k === 'recent_mentions' && isVisible(k)).map(() => (
                                        <div key="recent_mentions" className="bg-white rounded-2xl border border-gray-100 shadow-sm">
                                            <div className="px-6 py-4 border-b border-gray-100">
                                                <h3 className="text-base font-semibold text-gray-900">Recent Mentions</h3>
                                            </div>
                                            <div className="divide-y divide-gray-100">
                                                {recentMentions.length === 0 ? (
                                                    <div className="px-6 py-8 text-center text-gray-500">No mentions yet</div>
                                                ) : (
                                                    recentMentions.map((n) => (
                                                        <div key={n.id} className="px-6 py-4 text-sm text-gray-700">
                                                            {n.message}
                                                        </div>
                                                    ))
                                                )}
                                            </div>
                                        </div>
                                    ))}

                                    {sectionOrder.filter((k) => k === 'pending_timesheets' && isVisible(k)).map(() => (
                                        <div key="pending_timesheets" className="bg-white rounded-2xl border border-gray-100 shadow-sm">
                                            <div className="px-6 py-4 border-b border-gray-100">
                                                <h3 className="text-base font-semibold text-gray-900">Pending Timesheets</h3>
                                            </div>
                                            <div className="divide-y divide-gray-100">
                                                {pendingTimesheets.length === 0 ? (
                                                    <div className="px-6 py-8 text-center text-gray-500">Nothing pending</div>
                                                ) : (
                                                    pendingTimesheets.map((t) => (
                                                        <div key={t.id} className="px-6 py-4 flex items-center justify-between text-sm">
                                                            <span className="text-gray-900">{t.userName} &middot; {new Date(t.periodStart).toLocaleDateString()}</span>
                                                            <StatusBadge status={t.status.toLowerCase()} />
                                                        </div>
                                                    ))
                                                )}
                                            </div>
                                        </div>
                                    ))}

                                    {sectionOrder.filter((k) => k === 'my_tasks' && isVisible(k)).map(() => (
                                        <div key="my_tasks" className="bg-white rounded-2xl border border-gray-100 shadow-sm">
                                            <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-4">
                                                <h3 className="text-base font-semibold text-gray-900">My Tasks</h3>
                                                <div className="flex gap-4 ml-2">
                                                    {['today', 'upcoming', 'done'].map((tab) => (
                                                        <button
                                                            key={tab}
                                                            onClick={() => setDashboardMyTasksTab(tab)}
                                                            className={`text-sm font-medium capitalize pb-0.5 ${dashboardMyTasksTab === tab ? 'text-blue-600 border-b-2 border-blue-600 font-semibold' : 'text-gray-500 hover:text-gray-700'}`}
                                                        >
                                                            {tab} <span className="text-gray-400 font-normal">{myTaskGroups[tab].length}</span>
                                                        </button>
                                                    ))}
                                                </div>
                                            </div>
                                            <div className="divide-y divide-gray-100">
                                                {myTaskGroups[dashboardMyTasksTab].length === 0 ? (
                                                    <div className="px-6 py-8 text-center text-gray-500">Nothing here</div>
                                                ) : (
                                                    myTaskGroups[dashboardMyTasksTab].slice(0, 6).map((t) => (
                                                        <div key={t.id} className="px-6 py-3 flex items-center justify-between">
                                                            <div className="min-w-0">
                                                                <p className="text-sm font-medium text-gray-900 truncate">{t.title}</p>
                                                                <p className="text-xs text-gray-500">{t.projectName || getProjectName(t.projectId)}</p>
                                                            </div>
                                                            <PriorityBadge priority={t.priority} />
                                                        </div>
                                                    ))
                                                )}
                                            </div>
                                        </div>
                                    ))}

                                    {sectionOrder.filter((k) => k === 'weekly_completion_chart' && isVisible(k)).map(() => {
                                        const dayLabels = ['M', 'T', 'W', 'T', 'F', 'S', 'S'];
                                        const maxCount = Math.max(1, ...weeklyCompletion);
                                        const todayIndex = (new Date().getDay() + 6) % 7;
                                        return (
                                            <div key="weekly_completion_chart" className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
                                                <h3 className="text-base font-semibold text-gray-900 mb-4">This Week</h3>
                                                <div className="flex items-end gap-2 h-24">
                                                    {weeklyCompletion.map((count, i) => (
                                                        <div key={i} className="flex-1 flex flex-col items-center gap-1.5">
                                                            <div
                                                                className={`w-full rounded-md ${i === todayIndex ? 'bg-blue-600' : 'bg-gray-200'}`}
                                                                style={{ height: `${Math.max(4, (count / maxCount) * 96)}px` }}
                                                                title={`${count} completed`}
                                                            />
                                                            <span className={`text-xs ${i === todayIndex ? 'font-semibold text-gray-900' : 'text-gray-400'}`}>{dayLabels[i]}</span>
                                                        </div>
                                                    ))}
                                                </div>
                                                <div className="flex items-baseline gap-2 pt-3 mt-2 border-t border-gray-100">
                                                    <span className="text-sm text-gray-500">Tasks completed</span>
                                                    <span className="ml-auto text-base font-semibold text-gray-900">{weeklyCompletion.reduce((a, b) => a + b, 0)}</span>
                                                </div>
                                            </div>
                                        );
                                    })}

                                    {sectionOrder.filter((k) => k === 'activity_feed' && isVisible(k)).map(() => (
                                        <div key="activity_feed" className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
                                            <h3 className="text-base font-semibold text-gray-900 mb-4">Activity</h3>
                                            {activityFeed.length === 0 ? (
                                                <div className="py-4 text-center text-gray-500 text-sm">No activity yet</div>
                                            ) : (
                                                <div className="space-y-3">
                                                    {activityFeed.map((a) => (
                                                        <div key={a.id} className="flex items-start gap-3">
                                                            <span className={`h-1.5 w-1.5 rounded-full mt-1.5 flex-shrink-0 ${a.action === 'Completed' ? 'bg-green-500' : a.action === 'MarkedPaid' ? 'bg-green-500' : a.action === 'Created' ? 'bg-blue-500' : 'bg-gray-400'}`} />
                                                            <div>
                                                                <p className="text-sm text-gray-700">
                                                                    <span className="font-medium text-gray-900">{a.actorNameSnapshot}</span>{' '}
                                                                    {ACTIVITY_VERBS[a.action] || 'updated'}{' '}
                                                                    <span className="font-medium text-gray-900">&quot;{a.entityNameSnapshot}&quot;</span>
                                                                    {a.details && <span className="text-gray-500"> &middot; {a.details}</span>}
                                                                </p>
                                                                <p className="text-xs text-gray-400">{formatRelativeTime(a.timestamp)}</p>
                                                            </div>
                                                        </div>
                                                    ))}
                                                </div>
                                            )}
                                        </div>
                                    ))}
                                    </div>
                                </>
                            );
                        })()}
                    </div>
                )}

                {/* Projects Tab */}
                {activeTab === 'projects' && (
                    <div className="space-y-6">
                        <div className="flex justify-between items-center">
                            <div>
                                <h2 className="text-[27px] font-bold text-gray-900 tracking-tight">Projects</h2>
                                <p className="text-gray-500">
                                    {projects.filter(p => p.status === 'active').length} active &middot; {projects.filter(p => p.status === 'active' && new Date(p.endDate) < new Date()).length} at risk
                                </p>
                            </div>
                            {hasPermission(user?.permissions, 'projects.create') && (
                                <button
                                    onClick={() => {
                                        setEditingProjectId(null);
                                        setNewProject(emptyProjectForm);
                                        setShowAddProject(true);
                                    }}
                                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
                                >
                                    <Plus className="h-5 w-5" />
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
                                        No projects found. {hasPermission(user?.permissions, 'projects.create') && 'Create your first project!'}
                                    </div>
                                ) : (
                                    projects.map((project) => (
                                        <div key={project.id} className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
                                            <div className="flex justify-between items-start mb-4">
                                                <h3 className="text-lg font-semibold text-gray-900">{project.name}</h3>
                                                <div className="flex space-x-2">
                                                    {hasPermission(user?.permissions, 'projects.edit') && (
                                                        <button
                                                            onClick={() => openEditProject(project)}
                                                            className="text-gray-400 hover:text-gray-600"
                                                        >
                                                            <Edit3 className="h-4 w-4" />
                                                        </button>
                                                    )}
                                                    {hasPermission(user?.permissions, 'projects.delete') && (
                                                        <button
                                                            onClick={() => deleteProject(project.id)}
                                                            className="text-red-400 hover:text-red-600"
                                                        >
                                                            <Trash2 className="h-4 w-4" />
                                                        </button>
                                                    )}
                                                </div>
                                            </div>
                                            <p className="text-gray-500 mb-4">{project.description}</p>
                                            <div className="space-y-3">
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
                                                {project.taskCount !== undefined && project.taskCount > 0 && (
                                                    <div>
                                                        <div className="flex items-center justify-between text-sm text-gray-500 mb-1.5">
                                                            <span className="flex items-center">
                                                                <FileText className="h-4 w-4 mr-1" />
                                                                {project.completedTaskCount || 0}/{project.taskCount} tasks completed
                                                            </span>
                                                            <span className="font-medium text-gray-700">
                                                                {Math.round(((project.completedTaskCount || 0) / project.taskCount) * 100)}%
                                                            </span>
                                                        </div>
                                                        <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
                                                            <div
                                                                className="h-full bg-blue-600 rounded-full"
                                                                style={{ width: `${Math.round(((project.completedTaskCount || 0) / project.taskCount) * 100)}%` }}
                                                            />
                                                        </div>
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
                                <h2 className="text-[27px] font-bold text-gray-900 tracking-tight">Tasks</h2>
                                <p className="text-gray-500">
                                    {tasks.length} task{tasks.length !== 1 ? 's' : ''} &middot; {tasks.filter(t => t.isOverdue).length} overdue
                                </p>
                            </div>
                            <button
                                onClick={() => setShowAddTask(true)}
                                className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
                            >
                                <Plus className="h-5 w-5" />
                                New Task
                            </button>
                        </div>

                        <div className="flex items-center gap-1.5 mb-6 flex-wrap">
                            {[
                                { key: 'all', label: 'All', count: dashboardStats.totalTasks },
                                { key: 'todo', label: 'To do', count: dashboardStats.todoTasks },
                                { key: 'in-progress', label: 'In progress', count: dashboardStats.inProgressTasks },
                                { key: 'blocked', label: 'Blocked', count: dashboardStats.blockedTasks },
                                { key: 'completed', label: 'Done', count: dashboardStats.completedTasks },
                                { key: 'overdue', label: 'Overdue', count: dashboardStats.overdueTasks },
                            ].map(({ key, label, count }) => (
                                <button
                                    key={key}
                                    onClick={() => setFilterStatus(key)}
                                    className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-[8px] text-[12.5px] font-semibold transition-colors ${
                                        filterStatus === key
                                            ? key === 'overdue'
                                                ? 'bg-red-50 text-red-700'
                                                : 'bg-blue-50 text-blue-700'
                                            : 'text-gray-500 hover:bg-gray-100'
                                    }`}
                                >
                                    {label}
                                    {typeof count === 'number' && <span className="tabular-nums">{count}</span>}
                                </button>
                            ))}
                        </div>

                        {loading.tasks && <LoadingSpinner text="Loading tasks..." />}

                        {errors.tasks && (
                            <ErrorMessage message={errors.tasks} onRetry={loadTasks} />
                        )}

                        {!loading.tasks && !errors.tasks && (
                            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
                                <div className="overflow-x-auto">
                                    <table className="min-w-full divide-y divide-gray-100">
                                        <thead className="bg-gray-50/80">
                                            <tr>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Task</th>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Project</th>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Assigned To</th>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Status</th>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Priority</th>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Due Date</th>
                                                <th className="px-6 py-3 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider">Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody className="bg-white divide-y divide-gray-100">
                                            {tasks.length === 0 ? (
                                                <tr>
                                                    <td colSpan="7" className="px-6 py-8 text-center text-gray-500">
                                                        No tasks found for the selected filter
                                                    </td>
                                                </tr>
                                            ) : (
                                                tasks.map((task) => (
                                                    <tr key={task.id} className={`hover:bg-gray-50/60 transition-colors ${task.isOverdue ? 'bg-red-50/60' : ''}`}>
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
                                                            <select
                                                                value={task.status}
                                                                onChange={(e) => updateTaskStatus(task.id, e.target.value)}
                                                                className="text-sm border border-gray-300 rounded-md px-2 py-1 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                                                            >
                                                                <option value="todo">To Do</option>
                                                                <option value="in-progress">In Progress</option>
                                                                <option value="blocked">Blocked</option>
                                                                <option value="completed">Completed</option>
                                                            </select>
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
                                                                {hasPermission(user?.permissions, 'tasks.delete') && (
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
                                <h2 className="text-[27px] font-bold text-gray-900 tracking-tight">Team</h2>
                                <p className="text-gray-500">{teamMembers.length} member{teamMembers.length !== 1 ? 's' : ''}</p>
                            </div>
                            <div className="flex items-center gap-4">
                                <div className="flex items-center bg-gray-100 rounded-[10px] p-1 text-sm font-medium">
                                    <button
                                        onClick={() => setTeamView('list')}
                                        className={`px-3 py-1.5 rounded-lg transition-colors ${teamView === 'list' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
                                    >
                                        List
                                    </button>
                                    <button
                                        onClick={() => setTeamView('orgchart')}
                                        className={`px-3 py-1.5 rounded-lg transition-colors ${teamView === 'orgchart' ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
                                    >
                                        Org Chart
                                    </button>
                                </div>
                                {hasPermission(user?.permissions, 'users.delete') && (
                                    <label className="flex items-center gap-2 text-sm text-gray-500 cursor-pointer select-none">
                                        <input
                                            type="checkbox"
                                            checked={showInactiveMembers}
                                            onChange={(e) => setShowInactiveMembers(e.target.checked)}
                                            className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                        />
                                        Show locked-out members
                                    </label>
                                )}
                                {hasPermission(user?.permissions, 'users.create') && (
                                    <button
                                        onClick={() => setShowAddMember(true)}
                                        className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
                                    >
                                        <Plus className="h-5 w-5" />
                                        Add Member
                                    </button>
                                )}
                            </div>
                        </div>

                        {loading.teamMembers && <LoadingSpinner text="Loading team members..." />}

                        {errors.teamMembers && (
                            <ErrorMessage message={errors.teamMembers} onRetry={loadTeamMembers} />
                        )}

                        {!loading.teamMembers && !errors.teamMembers && teamView === 'orgchart' && (
                            <OrgChartTree teamMembers={teamMembers} />
                        )}

                        {!loading.teamMembers && !errors.teamMembers && teamView === 'list' && (
                            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                                {teamMembers.length === 0 ? (
                                    <div className="col-span-full text-center py-8 text-gray-500">
                                        No team members found.
                                    </div>
                                ) : (
                                    teamMembers.map((member) => (
                                        <div key={member.id} className={`bg-white rounded-2xl border shadow-sm p-6 ${member.isActive ? 'border-gray-100' : 'border-gray-100 opacity-60'}`}>
                                            <div className="flex items-center mb-4">
                                                <div className={`h-12 w-12 ${getAvatarColor(member.name)} rounded-full flex items-center justify-center text-white text-sm font-semibold flex-shrink-0`}>
                                                    {(member.name || '?').split(' ').filter(Boolean).map(n => n[0]).slice(0, 2).join('').toUpperCase()}
                                                </div>
                                                <div className="ml-4">
                                                    <h3 className="text-lg font-semibold text-gray-900">{member.name}</h3>
                                                    <p className="text-gray-500">{member.position}</p>
                                                    {member.managerName && (
                                                        <p className="text-xs text-gray-400">Reports to {member.managerName}</p>
                                                    )}
                                                </div>
                                            </div>
                                            <div className="space-y-3">
                                                <div className="flex items-center gap-1.5 flex-wrap">
                                                    <RoleBadge role={member.role} />
                                                    {!member.isActive && (
                                                        <span className="inline-flex items-center px-[9px] py-[3px] rounded-[7px] text-[11.5px] font-semibold bg-[#F2F2F4] text-[#62626A]">
                                                            Locked out
                                                        </span>
                                                    )}
                                                    {member.isActive && member.mustChangePassword && (
                                                        <span className="inline-flex items-center px-[9px] py-[3px] rounded-[7px] text-[11.5px] font-semibold bg-[#FFEED6] text-[#874400]">
                                                            Pending setup
                                                        </span>
                                                    )}
                                                </div>
                                                <p className="text-sm text-gray-500">{member.email}</p>
                                                {(() => {
                                                    const openCount = tasks.filter(t => t.assignedToId === member.id && t.status !== 'completed').length;
                                                    // Open tasks relative to a 10-task "full load" line - not a real capacity
                                                    // setting anywhere, just a sensible fixed threshold for the bar/amber cue.
                                                    const workloadPct = Math.min(100, (openCount / 10) * 100);
                                                    const isHeavy = workloadPct >= 80;
                                                    return (
                                                        <div className="pt-1">
                                                            <div className="flex items-center justify-between mb-1">
                                                                <span className="text-xs text-gray-400 uppercase tracking-wide">Workload</span>
                                                                <span className="text-xs text-gray-500">{openCount} open</span>
                                                            </div>
                                                            <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden">
                                                                <div
                                                                    className={`h-full rounded-full ${isHeavy ? 'bg-amber-500' : 'bg-blue-600'}`}
                                                                    style={{ width: `${workloadPct}%` }}
                                                                />
                                                            </div>
                                                        </div>
                                                    );
                                                })()}
                                                <div className="grid grid-cols-3 gap-2 pt-3 border-t border-gray-100 text-center">
                                                    <div>
                                                        <p className="text-lg font-bold text-gray-900">{tasks.filter(t => t.assignedToId === member.id).length}</p>
                                                        <p className="text-[11px] text-gray-400 uppercase tracking-wide">Assigned</p>
                                                    </div>
                                                    <div>
                                                        <p className="text-lg font-bold text-green-600">{tasks.filter(t => t.assignedToId === member.id && t.status === 'completed').length}</p>
                                                        <p className="text-[11px] text-gray-400 uppercase tracking-wide">Completed</p>
                                                    </div>
                                                    <div>
                                                        <p className="text-lg font-bold text-red-600">{tasks.filter(t => t.assignedToId === member.id && t.isOverdue).length}</p>
                                                        <p className="text-[11px] text-gray-400 uppercase tracking-wide">Overdue</p>
                                                    </div>
                                                </div>
                                                {(hasPermission(user?.permissions, 'users.edit') || hasPermission(user?.permissions, 'users.delete')) && member.id !== user?.id && (
                                                    <div className="flex items-center gap-3 pt-3 border-t border-gray-100 text-sm">
                                                        {hasPermission(user?.permissions, 'users.edit') && (
                                                            <button
                                                                onClick={() => openEditMember(member)}
                                                                className="text-gray-600 hover:text-gray-900 font-medium"
                                                            >
                                                                Edit
                                                            </button>
                                                        )}
                                                        {hasPermission(user?.permissions, 'users.edit') && member.isActive && member.mustChangePassword && (
                                                            <button
                                                                onClick={() => handleResendTempPassword(member)}
                                                                className="text-blue-600 hover:text-blue-800 font-medium"
                                                            >
                                                                Resend temp password
                                                            </button>
                                                        )}
                                                        {hasPermission(user?.permissions, 'users.delete') && (
                                                            <button
                                                                onClick={() => handleToggleMemberActive(member)}
                                                                className={`font-medium ml-auto ${member.isActive ? 'text-red-600 hover:text-red-800' : 'text-green-600 hover:text-green-800'}`}
                                                            >
                                                                {member.isActive ? 'Lock out' : 'Reactivate'}
                                                            </button>
                                                        )}
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

                {/* Vault Tab */}
                {activeTab === 'vault' && (
                    <VaultPage apiService={apiService} user={user} teamMembers={teamMembers} />
                )}

                {/* Wiki Tab */}
                {activeTab === 'wiki' && (
                    <WikiPage apiService={apiService} user={user} teamMembers={teamMembers} deepLink={deepLink?.tab === 'wiki' ? deepLink : null} />
                )}

                {/* Library Tab */}
                {activeTab === 'library' && (
                    <LibraryPage apiService={apiService} user={user} teamMembers={teamMembers} deepLink={deepLink?.tab === 'library' ? deepLink : null} />
                )}

                {/* Ideas Tab */}
                {activeTab === 'ideas' && (
                    <IdeasPage apiService={apiService} user={user} />
                )}

                {/* Reminders Tab */}
                {activeTab === 'reminders' && (
                    <RemindersPage apiService={apiService} user={user} />
                )}

                {/* Finance Tab */}
                {activeTab === 'finance' && (
                    <InvoicesPage apiService={apiService} user={user} />
                )}

                {/* Settings Tab */}
                {activeTab === 'settings' && (
                    <div className="space-y-10">
                        <NotificationPreferences apiService={apiService} />
                        <DashboardWidgetSettings apiService={apiService} user={user} />
                        {hasPermission(user?.permissions, 'users.manage_roles') && (
                            <PermissionsManagement apiService={apiService} />
                        )}
                        {hasPermission(user?.permissions, 'groups.manage') && (
                            <GroupsManagement apiService={apiService} teamMembers={teamMembers} />
                        )}
                        {hasPermission(user?.permissions, 'audit.view') && (
                            <AuditLog apiService={apiService} />
                        )}
                    </div>
                )}

                {/* Reports Tab */}
                {activeTab === 'reports' && (
                    <div className="space-y-6">
                        <div>
                            <h2 className="text-[27px] font-bold text-gray-900 tracking-tight">Reports</h2>
                            <p className="text-gray-500">Generate a snapshot or schedule it weekly</p>
                        </div>

                        {hasPermission(user?.permissions, 'reports.view') ? (
                            <>
                                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                                    {[
                                        { type: 'ProjectSummary', icon: FileText, color: 'blue', title: 'Project Summary', desc: 'Overview of all projects, their status, and completion rates.' },
                                        { type: 'TeamPerformance', icon: Users, color: 'green', title: 'Team Performance', desc: 'Individual team member performance and task completion statistics.' },
                                        { type: 'OverdueTasks', icon: Flag, color: 'red', title: 'Overdue Tasks', desc: 'List of all overdue tasks with assignees and due dates.' },
                                    ].map(({ type, icon: Icon, color, title, desc }) => {
                                        const schedule = reportSchedules.find((s) => s.reportType === type);
                                        return (
                                            <div key={type} className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6 flex flex-col">
                                                <div className="flex items-center mb-4">
                                                    <div className={`bg-${color}-50 rounded-lg p-3 mr-3 flex-shrink-0`}>
                                                        <Icon className={`h-6 w-6 text-${color}-600`} />
                                                    </div>
                                                    <h3 className="text-lg font-semibold text-gray-900">{title}</h3>
                                                </div>
                                                <p className="text-gray-500 mb-4 flex-1">{desc}</p>

                                                <div className="flex gap-2 mb-3">
                                                    {['Csv', 'Pdf'].map((fmt) => (
                                                        <button
                                                            key={fmt}
                                                            onClick={() => setReportFormats((prev) => ({ ...prev, [type]: fmt }))}
                                                            className={`flex-1 text-xs font-semibold py-1.5 rounded-md transition-colors ${reportFormats[type] === fmt ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}
                                                        >
                                                            {fmt.toUpperCase()}
                                                        </button>
                                                    ))}
                                                </div>

                                                <div className="flex gap-2">
                                                    <button
                                                        onClick={() => generateReport(type)}
                                                        disabled={generatingReportType === type}
                                                        className="flex-1 inline-flex items-center justify-center gap-2 bg-blue-600 text-white py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                                                    >
                                                        {generatingReportType === type ? (
                                                            <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                                                        ) : (
                                                            <Download className="h-4 w-4" />
                                                        )}
                                                        {generatingReportType === type ? 'Generating...' : 'Generate'}
                                                    </button>
                                                    {schedule ? (
                                                        <button
                                                            onClick={() => cancelReportSchedule(schedule.id)}
                                                            disabled={cancellingScheduleId === schedule.id}
                                                            title={`Scheduled weekly, next run ${new Date(schedule.nextRunAt).toLocaleDateString()}`}
                                                            className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-3 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50"
                                                        >
                                                            {cancellingScheduleId === schedule.id ? 'Cancelling...' : 'Weekly · Cancel'}
                                                        </button>
                                                    ) : (
                                                        <button
                                                            onClick={() => scheduleReport(type)}
                                                            disabled={schedulingReportType === type}
                                                            className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-3 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50"
                                                        >
                                                            {schedulingReportType === type ? 'Scheduling...' : 'Schedule'}
                                                        </button>
                                                    )}
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>

                                <div className="bg-white rounded-2xl border border-gray-100 shadow-sm">
                                    <div className="px-6 py-4 border-b border-gray-100">
                                        <h3 className="text-base font-semibold text-gray-900">Recent Exports</h3>
                                    </div>
                                    <div className="divide-y divide-gray-100">
                                        {recentExports.length === 0 ? (
                                            <div className="px-6 py-8 text-center text-gray-500">No exports yet</div>
                                        ) : (
                                            recentExports.map((exp) => (
                                                <div key={exp.id} className="px-6 py-3 flex items-center justify-between">
                                                    <div>
                                                        <p className="text-sm font-medium text-gray-900">{exp.reportType} &middot; {exp.format.toUpperCase()}</p>
                                                        <p className="text-xs text-gray-500">{exp.generatedByName} &middot; {new Date(exp.generatedAt).toLocaleString()} &middot; {(exp.fileSizeBytes / 1024).toFixed(0)} KB</p>
                                                    </div>
                                                    <button
                                                        onClick={() => apiService.downloadReportExport(exp.id, `${exp.reportType}.${exp.format === 'Pdf' ? 'pdf' : 'csv'}`)}
                                                        className="text-blue-600 hover:text-blue-800 text-sm font-medium"
                                                    >
                                                        Download
                                                    </button>
                                                </div>
                                            ))
                                        )}
                                    </div>
                                </div>
                            </>
                        ) : (
                            <div className="bg-gray-50 rounded-xl border border-gray-100 p-8 text-center">
                                <Shield className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                                <p className="text-gray-500">You don't have permission to access reports.</p>
                            </div>
                        )}
                    </div>
                )}
                </main>
            </div>

            {/* Modals */}
            {/* Add Project Modal */}
            {showAddProject && (
                <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-2xl shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
                            <h3 className="text-base font-semibold text-gray-900">{editingProjectId !== null ? 'Edit Project' : 'Add New Project'}</h3>
                            <button type="button" onClick={closeProjectModal} className="text-gray-400 hover:text-gray-600 rounded-lg p-1">
                                <X className="h-5 w-5" />
                            </button>
                        </div>
                        <form onSubmit={handleAddProject}>
                            <div className="px-6 py-5 space-y-4">
                                <input
                                    type="text"
                                    placeholder="Project Name"
                                    value={newProject.name}
                                    onChange={(e) => setNewProject({ ...newProject, name: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <textarea
                                    placeholder="Description"
                                    value={newProject.description}
                                    onChange={(e) => setNewProject({ ...newProject, description: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    rows="3"
                                />
                                <select
                                    value={newProject.priority}
                                    onChange={(e) => setNewProject({ ...newProject, priority: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                >
                                    <option value="low">Low Priority</option>
                                    <option value="medium">Medium Priority</option>
                                    <option value="high">High Priority</option>
                                </select>
                                {editingProjectId !== null && (
                                    <select
                                        value={newProject.status}
                                        onChange={(e) => setNewProject({ ...newProject, status: e.target.value })}
                                        className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    >
                                        <option value="active">Active</option>
                                        <option value="inactive">Inactive</option>
                                        <option value="completed">Completed</option>
                                    </select>
                                )}
                                <input
                                    type="date"
                                    value={newProject.startDate}
                                    onChange={(e) => setNewProject({ ...newProject, startDate: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <input
                                    type="date"
                                    value={newProject.endDate}
                                    onChange={(e) => setNewProject({ ...newProject, endDate: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <input
                                    type="text"
                                    placeholder="Tags (comma separated)"
                                    value={newProject.tags}
                                    onChange={(e) => setNewProject({ ...newProject, tags: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                />
                                <div>
                                    <label className="block text-sm text-gray-600 mb-1.5">Team members</label>
                                    <div className="border border-gray-200 rounded-[10px] max-h-40 overflow-y-auto divide-y divide-gray-100">
                                        {teamMembers.length === 0 ? (
                                            <p className="px-3.5 py-2.5 text-sm text-gray-400">No team members yet.</p>
                                        ) : (
                                            teamMembers.map((member) => (
                                                <label key={member.id} className="flex items-center gap-2.5 px-3.5 py-2 text-sm cursor-pointer hover:bg-gray-50/60 transition-colors">
                                                    <input
                                                        type="checkbox"
                                                        checked={newProject.teamMemberIds.includes(member.id)}
                                                        onChange={(e) => setNewProject((prev) => ({
                                                            ...prev,
                                                            teamMemberIds: e.target.checked
                                                                ? [...prev.teamMemberIds, member.id]
                                                                : prev.teamMemberIds.filter((id) => id !== member.id),
                                                        }))}
                                                        className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                                                    />
                                                    <span className="text-gray-900">{member.name}</span>
                                                    <span className="text-gray-400 text-xs">{member.position}</span>
                                                </label>
                                            ))
                                        )}
                                    </div>
                                </div>
                            </div>
                            <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={closeProjectModal}
                                    disabled={savingProject}
                                    className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50"
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    disabled={savingProject}
                                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                                >
                                    {savingProject && (
                                        <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                                    )}
                                    {savingProject ? 'Saving...' : editingProjectId !== null ? 'Save Changes' : 'Add Project'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Add Task Modal */}
            {showAddTask && (
                <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-2xl shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
                            <h3 className="text-base font-semibold text-gray-900">Add New Task</h3>
                            <button type="button" onClick={() => setShowAddTask(false)} className="text-gray-400 hover:text-gray-600 rounded-lg p-1">
                                <X className="h-5 w-5" />
                            </button>
                        </div>
                        <form onSubmit={handleAddTask}>
                            <div className="px-6 py-5 space-y-4">
                                <select
                                    value={newTask.projectId}
                                    onChange={(e) => setNewTask({ ...newTask, projectId: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
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
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <textarea
                                    placeholder="Description"
                                    value={newTask.description}
                                    onChange={(e) => setNewTask({ ...newTask, description: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    rows="3"
                                />
                                <select
                                    value={newTask.priority}
                                    onChange={(e) => setNewTask({ ...newTask, priority: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                >
                                    <option value="low">Low Priority</option>
                                    <option value="medium">Medium Priority</option>
                                    <option value="high">High Priority</option>
                                </select>
                                <select
                                    value={newTask.assignedToId}
                                    onChange={(e) => setNewTask({ ...newTask, assignedToId: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
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
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <input
                                    type="text"
                                    placeholder="Tags (comma separated)"
                                    value={newTask.tags}
                                    onChange={(e) => setNewTask({ ...newTask, tags: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                />
                            </div>
                            <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => setShowAddTask(false)}
                                    disabled={savingTask}
                                    className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50"
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    disabled={savingTask}
                                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                                >
                                    {savingTask && (
                                        <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                                    )}
                                    {savingTask ? 'Adding...' : 'Add Task'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Add Member Modal */}
            {showAddMember && (
                <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-2xl shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
                            <h3 className="text-base font-semibold text-gray-900">Add Team Member</h3>
                            <button type="button" onClick={() => setShowAddMember(false)} className="text-gray-400 hover:text-gray-600 rounded-lg p-1">
                                <X className="h-5 w-5" />
                            </button>
                        </div>
                        <form onSubmit={handleAddMember}>
                            <div className="px-6 py-5 space-y-4">
                                <input
                                    type="text"
                                    placeholder="Full Name"
                                    value={newMember.name}
                                    onChange={(e) => setNewMember({ ...newMember, name: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <select
                                    value={newMember.role}
                                    onChange={(e) => setNewMember({ ...newMember, role: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                >
                                    <option value="member">Member</option>
                                    <option value="manager">Manager</option>
                                    {hasPermission(user?.permissions, 'users.manage_roles') && <option value="admin">Admin</option>}
                                </select>
                                <input
                                    type="text"
                                    placeholder="Position"
                                    value={newMember.position}
                                    onChange={(e) => setNewMember({ ...newMember, position: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <input
                                    type="email"
                                    placeholder="Email"
                                    value={newMember.email}
                                    onChange={(e) => setNewMember({ ...newMember, email: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <select
                                    value={newMember.managerId}
                                    onChange={(e) => setNewMember({ ...newMember, managerId: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                >
                                    <option value="">Reports to (no manager)</option>
                                    {teamMembers.map((m) => (
                                        <option key={m.id} value={m.id}>{m.name}</option>
                                    ))}
                                </select>
                                <p className="text-xs text-gray-500">
                                    A temporary password will be generated and emailed to this address. They'll be asked to set their own password on first login.
                                </p>
                            </div>
                            <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => setShowAddMember(false)}
                                    disabled={savingMember}
                                    className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50"
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    disabled={savingMember}
                                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                                >
                                    {savingMember && (
                                        <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />
                                    )}
                                    {savingMember ? 'Adding...' : 'Add Member'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {editingMemberId && (
                <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
                    <div className="bg-white rounded-2xl shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
                        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
                            <h3 className="text-base font-semibold text-gray-900">Edit Team Member</h3>
                            <button type="button" onClick={() => setEditingMemberId(null)} className="text-gray-400 hover:text-gray-600 rounded-lg p-1">
                                <X className="h-5 w-5" />
                            </button>
                        </div>
                        <form onSubmit={handleSaveMemberEdit}>
                            <div className="px-6 py-5 space-y-4">
                                <input
                                    type="text"
                                    placeholder="Full Name"
                                    value={editMemberForm.name}
                                    onChange={(e) => setEditMemberForm({ ...editMemberForm, name: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <input
                                    type="text"
                                    placeholder="Position"
                                    value={editMemberForm.position}
                                    onChange={(e) => setEditMemberForm({ ...editMemberForm, position: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <input
                                    type="email"
                                    placeholder="Email"
                                    value={editMemberForm.email}
                                    onChange={(e) => setEditMemberForm({ ...editMemberForm, email: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                    required
                                />
                                <select
                                    value={editMemberForm.managerId}
                                    onChange={(e) => setEditMemberForm({ ...editMemberForm, managerId: e.target.value })}
                                    className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                                >
                                    <option value="">Reports to (no manager)</option>
                                    {teamMembers.filter((m) => m.id !== editingMemberId).map((m) => (
                                        <option key={m.id} value={m.id}>{m.name}</option>
                                    ))}
                                </select>
                            </div>
                            <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => setEditingMemberId(null)}
                                    disabled={savingMemberEdit}
                                    className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50"
                                >
                                    Cancel
                                </button>
                                <button
                                    type="submit"
                                    disabled={savingMemberEdit}
                                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                                >
                                    {savingMemberEdit ? 'Saving...' : 'Save changes'}
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
        <ToastProvider>
            <AuthProvider>
                <div className="App">
                    <UpdateAvailableBanner />
                    <OfflineBanner />
                    <AuthGuard />
                </div>
            </AuthProvider>
        </ToastProvider>
    );
};

// Auth Guard Component
const AuthGuard = () => {
    const { user, loading } = useAuth();
    const [authView, setAuthView] = useState('login'); // 'login' | 'forgot'
    // Set when a login attempt reports MustChangePassword (temp/admin-issued password, or an account
    // retroactively forced to reset) - same ResetPasswordForm as the emailed-link path below, just
    // reached without a URL round-trip since the token came straight back from /auth/login.
    const [forcedResetToken, setForcedResetToken] = useState(null);

    // The emailed reset link points at /reset-password?token=... - this app has no router, so a
    // direct hit on that path is detected here (same manual technique as Wiki/Library share deep
    // links) and takes priority over auth state, since resetting a password doesn't require being
    // logged in.
    const resetToken = window.location.pathname === '/reset-password'
        ? new URLSearchParams(window.location.search).get('token')
        : null;

    const backToLogin = () => {
        if (window.location.pathname === '/reset-password') {
            window.history.replaceState({}, '', '/');
        }
        setForcedResetToken(null);
        setAuthView('login');
    };

    if (resetToken) {
        return <ResetPasswordForm token={resetToken} onBackToLogin={backToLogin} />;
    }

    if (forcedResetToken) {
        return <ResetPasswordForm token={forcedResetToken} onBackToLogin={backToLogin} />;
    }

    if (loading) {
        return (
            <div className="min-h-screen flex items-center justify-center">
                <LoadingSpinner text="Loading application..." />
            </div>
        );
    }

    if (user) {
        return <ProjectManagementSystem />;
    }

    return authView === 'forgot'
        ? <ForgotPasswordForm onBackToLogin={backToLogin} />
        : <LoginForm onForgotPassword={() => setAuthView('forgot')} onMustChangePassword={setForcedResetToken} />;
};

export default App;