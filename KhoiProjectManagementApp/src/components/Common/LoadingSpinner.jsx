// src/components/Common/LoadingSpinner.js
import React from 'react';

const LoadingSpinner = ({ text = "Loading..." }) => (
  <div className="flex justify-center items-center py-8">
    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
    <span className="ml-2 text-gray-600">{text}</span>
  </div>
);

export default LoadingSpinner;