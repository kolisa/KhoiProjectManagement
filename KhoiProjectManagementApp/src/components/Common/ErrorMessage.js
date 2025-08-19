import React from 'react';

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

export default ErrorMessage;