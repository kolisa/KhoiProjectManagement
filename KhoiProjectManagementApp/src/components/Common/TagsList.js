// src/components/Common/TagsList.js
import React from 'react';
import { Tag } from 'lucide-react';

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

export default TagsList;