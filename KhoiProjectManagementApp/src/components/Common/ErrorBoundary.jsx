// src/components/Common/ErrorBoundary.js
// Catches render-time crashes anywhere below it - without this, an uncaught error in any component
// unmounts the whole React tree and leaves a blank white screen with no way back except a manual
// browser refresh. Error boundaries only catch render/lifecycle errors, not event-handler rejections
// (those go through the toast system instead - see ToastContext/apiError.js).
import React from 'react';
import { AlertTriangle, RefreshCw, Home } from 'lucide-react';

class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error, info) {
    console.error('Unhandled error caught by ErrorBoundary:', error, info?.componentStack);
  }

  handleReload = () => {
    window.location.reload();
  };

  handleGoHome = () => {
    localStorage.removeItem('khoi_last_tab');
    window.location.href = '/';
  };

  render() {
    if (!this.state.error) return this.props.children;

    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
        <div className="max-w-md w-full bg-white rounded-2xl border border-gray-100 shadow-sm p-8 text-center">
          <div className="mx-auto h-14 w-14 rounded-2xl bg-red-50 flex items-center justify-center mb-5">
            <AlertTriangle className="h-7 w-7 text-red-500" />
          </div>
          <h1 className="text-lg font-semibold text-gray-900 mb-2">Something went wrong</h1>
          <p className="text-sm text-gray-500 mb-6">
            An unexpected error occurred and this page couldn't continue. Your work in other tabs
            hasn't been affected - try reloading, or head back to the dashboard.
          </p>
          <div className="flex items-center justify-center gap-3">
            <button
              onClick={this.handleGoHome}
              className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
            >
              <Home className="h-4 w-4" />
              Dashboard
            </button>
            <button
              onClick={this.handleReload}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
            >
              <RefreshCw className="h-4 w-4" />
              Reload
            </button>
          </div>
          {import.meta.env.DEV && (
            <details className="mt-6 text-left">
              <summary className="text-xs text-gray-400 cursor-pointer">Technical details</summary>
              <pre className="mt-2 text-[11px] text-red-600 bg-red-50 rounded-lg p-3 overflow-auto max-h-40 whitespace-pre-wrap">
                {this.state.error?.message}
                {'\n'}
                {this.state.error?.stack}
              </pre>
            </details>
          )}
        </div>
      </div>
    );
  }
}

export default ErrorBoundary;
