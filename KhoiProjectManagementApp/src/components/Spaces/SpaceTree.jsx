// src/components/Spaces/SpaceTree.js
// Recursive tree navigation for Spaces - shared by the Vault (browsing categories) and Wiki
// (browsing wiki Spaces) tabs, since both are the same underlying hierarchical container.
import React, { useState, useEffect } from 'react';
import { ChevronRight, ChevronDown, Folder } from 'lucide-react';

const SpaceTreeNode = ({ apiService, space, selectedSpaceId, onSelect, depth }) => {
  const [expanded, setExpanded] = useState(false);
  const [children, setChildren] = useState(null);
  const [loadingChildren, setLoadingChildren] = useState(false);

  const toggleExpand = async () => {
    if (!expanded && children === null) {
      setLoadingChildren(true);
      try {
        const result = await apiService.getSpaces(space.id);
        setChildren(result || []);
      } catch (error) {
        console.error('Failed to load child spaces:', error);
        setChildren([]);
      } finally {
        setLoadingChildren(false);
      }
    }
    setExpanded(!expanded);
  };

  return (
    <div>
      <div
        className={`flex items-center py-1.5 px-2 rounded-md cursor-pointer text-sm transition-colors ${
          selectedSpaceId === space.id ? 'bg-blue-50 text-blue-700 font-semibold' : 'hover:bg-gray-100 text-gray-700'
        }`}
        style={{ paddingLeft: `${depth * 16 + 8}px` }}
      >
        <button onClick={toggleExpand} className="mr-1 flex-shrink-0" aria-label="Toggle">
          {expanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
        </button>
        <Folder className="h-4 w-4 mr-1.5 flex-shrink-0 text-gray-400" />
        <span className="truncate" onClick={() => onSelect(space)}>{space.name}</span>
      </div>
      {expanded && (
        <div>
          {loadingChildren && (
            <div className="text-xs text-gray-400" style={{ paddingLeft: `${(depth + 1) * 16 + 8}px` }}>
              Loading...
            </div>
          )}
          {children && children.map((child) => (
            <SpaceTreeNode
              key={child.id}
              apiService={apiService}
              space={child}
              selectedSpaceId={selectedSpaceId}
              onSelect={onSelect}
              depth={depth + 1}
            />
          ))}
          {children && children.length === 0 && !loadingChildren && (
            <div className="text-xs text-gray-400 italic" style={{ paddingLeft: `${(depth + 1) * 16 + 8}px` }}>
              No sub-spaces
            </div>
          )}
        </div>
      )}
    </div>
  );
};

const SpaceTree = ({ apiService, selectedSpaceId, onSelect }) => {
  const [roots, setRoots] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const loadRoots = async () => {
      setLoading(true);
      try {
        const result = await apiService.getSpaces(null);
        setRoots(result || []);
        setError(null);
        // Master-detail panes should never rest on an empty "select something" placeholder -
        // preselect the first category the moment the list loads, same as a real click.
        if (!selectedSpaceId && result && result.length > 0) {
          onSelect(result[0]);
        }
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    loadRoots();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiService]);

  if (loading) return <div className="text-sm text-gray-400 p-2">Loading spaces...</div>;
  if (error) return <div className="text-sm text-red-500 p-2">Error: {error}</div>;
  if (roots.length === 0) {
    return <div className="text-sm text-gray-400 p-2 italic">No spaces available to you yet.</div>;
  }

  return (
    <div>
      {roots.map((space) => (
        <SpaceTreeNode
          key={space.id}
          apiService={apiService}
          space={space}
          selectedSpaceId={selectedSpaceId}
          onSelect={onSelect}
          depth={0}
        />
      ))}
    </div>
  );
};

export default SpaceTree;
