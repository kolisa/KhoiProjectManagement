// src/components/Wiki/WikiPage.js
import React, { useState, useEffect, useRef } from 'react';
import { Plus, BookOpen, FileText, ChevronRight, Search, X, GripVertical } from 'lucide-react';
import SpaceTree from '../Spaces/SpaceTree';
import WikiPageDetail from './WikiPageDetail';
import WikiPageEditor from './WikiPageEditor';
import { hasSpaceLevel } from '../../utils/spaceLevel';

const SEARCH_DEBOUNCE_MS = 350;

const WikiPage = ({ apiService, user, deepLink }) => {
  const [selectedSpace, setSelectedSpace] = useState(null);
  // breadcrumb: array of { id, title } - null parentPageId means Space root
  const [breadcrumb, setBreadcrumb] = useState([]);
  const [pages, setPages] = useState([]);
  const [loadingPages, setLoadingPages] = useState(false);
  const [error, setError] = useState(null);
  const [selectedPageId, setSelectedPageId] = useState(null);
  const [creatingUnderParentId, setCreatingUnderParentId] = useState(undefined); // undefined = not creating
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState(null);
  const [searching, setSearching] = useState(false);
  const searchDebounceRef = useRef(null);

  const currentParentPageId = breadcrumb.length > 0 ? breadcrumb[breadcrumb.length - 1].id : null;

  // A shared link jumps straight to the linked Space/page - if the recipient doesn't actually have
  // Read access, getSpace/the page load below fails exactly the same way normal browsing would (the
  // link is a shortcut, not a permission bypass). Sets selectedPageId directly rather than via a ref
  // consumed by the space-change effect below - that ref-timing approach broke under React 18
  // StrictMode's dev-mode double effect invocation, which replayed the "reset to null" branch after
  // the deep link had already applied. Splitting "load pages" (an effect, runs on every space change)
  // from "reset the open page" (only ever triggered by an explicit user navigation action, never as a
  // blanket side effect) removes the race entirely - nothing auto-resets what this effect just set.
  useEffect(() => {
    if (!deepLink?.spaceId) return;
    (async () => {
      try {
        const space = await apiService.getSpace(Number(deepLink.spaceId));
        setSelectedSpace(space);
        if (deepLink.pageId) setSelectedPageId(Number(deepLink.pageId));
      } catch (err) {
        setError(err.message);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deepLink]);

  const loadPages = async (spaceId, parentPageId) => {
    setLoadingPages(true);
    try {
      const result = await apiService.getWikiPages(spaceId, parentPageId);
      setPages(result || []);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoadingPages(false);
    }
  };

  useEffect(() => {
    if (selectedSpace) {
      loadPages(selectedSpace.id, currentParentPageId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedSpace, breadcrumb]);

  const handleSelectSpace = (space) => {
    setSelectedSpace(space);
    setBreadcrumb([]);
    setSelectedPageId(null);
  };

  const handleDrillInto = (page) => {
    setBreadcrumb([...breadcrumb, { id: page.id, title: page.title }]);
    setSelectedPageId(null);
  };

  const handleBreadcrumbClick = (index) => {
    setBreadcrumb(breadcrumb.slice(0, index + 1));
    setSelectedPageId(null);
  };

  // Debounced full-text search - fires SEARCH_DEBOUNCE_MS after the user stops typing, not on every
  // keystroke. Results are already permission-filtered server-side (WikiService.SearchPagesAsync).
  useEffect(() => {
    if (searchDebounceRef.current) clearTimeout(searchDebounceRef.current);
    if (!searchQuery.trim()) {
      setSearchResults(null);
      return;
    }
    searchDebounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const results = await apiService.searchWiki(searchQuery.trim());
        setSearchResults(results || []);
      } catch (err) {
        setError(err.message);
      } finally {
        setSearching(false);
      }
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(searchDebounceRef.current);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchQuery]);

  const handleSearchResultClick = async (result) => {
    try {
      const space = await apiService.getSpace(result.spaceId);
      setSelectedSpace(space);
      setBreadcrumb([]);
      setSelectedPageId(result.id);
      setSearchQuery('');
      setSearchResults(null);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleCreate = async (data) => {
    await apiService.createWikiPage({
      title: data.title,
      spaceId: selectedSpace.id,
      parentPageId: creatingUnderParentId ?? null,
      contentMarkdown: data.contentMarkdown,
    });
    setCreatingUnderParentId(undefined);
    await loadPages(selectedSpace.id, currentParentPageId);
  };

  const canWrite = selectedSpace && hasSpaceLevel(selectedSpace.myEffectiveLevel, 'Write');

  // Drag-and-drop reorders within the currently-visible sibling list; "Move to" re-parents up the
  // visible ancestry (root or any breadcrumb ancestor). Moving to an arbitrary branch not currently in
  // view isn't supported - a deliberate scope cut given the page list shows one level at a time rather
  // than a full expandable tree.
  const [dragIndex, setDragIndex] = useState(null);

  const handleDrop = async (targetIndex) => {
    if (dragIndex === null || dragIndex === targetIndex) {
      setDragIndex(null);
      return;
    }
    const reordered = [...pages];
    const [moved] = reordered.splice(dragIndex, 1);
    reordered.splice(targetIndex, 0, moved);
    setPages(reordered);
    setDragIndex(null);
    try {
      await apiService.reorderWikiPages(selectedSpace.id, currentParentPageId, reordered.map((p) => p.id));
    } catch (err) {
      setError(err.message);
      await loadPages(selectedSpace.id, currentParentPageId);
    }
  };

  const handleMove = async (page, targetParentId) => {
    try {
      await apiService.moveWikiPage(page.id, targetParentId);
      await loadPages(selectedSpace.id, currentParentPageId);
    } catch (err) {
      setError(err.message);
    }
  };

  const moveTargets = selectedSpace
    ? [{ id: null, label: `${selectedSpace.name} (root)` }, ...breadcrumb.map((b) => ({ id: b.id, label: b.title }))]
        .filter((t) => t.id !== currentParentPageId)
    : [];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-gray-900 flex items-center">
          <BookOpen className="h-7 w-7 mr-2 text-gray-700" />
          Wiki
        </h2>
        <p className="text-gray-600">Team documentation and knowledge base</p>
      </div>

      <div className="relative max-w-xl">
        <Search className="h-4 w-4 absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="Search wiki pages..."
          className="w-full border border-gray-300 rounded-lg pl-9 pr-9 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
        />
        {searchQuery && (
          <button
            onClick={() => { setSearchQuery(''); setSearchResults(null); }}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
            aria-label="Clear search"
          >
            <X className="h-4 w-4" />
          </button>
        )}

        {searchResults !== null && (
          <div className="absolute z-20 mt-1 w-full bg-white border border-gray-100 rounded-xl shadow-lg max-h-96 overflow-y-auto">
            {searching && <div className="p-3 text-sm text-gray-400">Searching...</div>}
            {!searching && searchResults.length === 0 && (
              <div className="p-3 text-sm text-gray-400">No pages match "{searchQuery}".</div>
            )}
            {!searching && searchResults.map((r) => (
              <button
                key={r.id}
                onClick={() => handleSearchResultClick(r)}
                className="w-full text-left p-3 hover:bg-gray-50/60 transition-colors border-b border-gray-100 last:border-b-0"
              >
                <div className="text-sm font-medium text-gray-900">{r.title}</div>
                <div className="text-xs text-gray-500 mb-1">{r.spaceName}</div>
                <div className="text-xs text-gray-600">{r.snippet}</div>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="md:col-span-1 bg-white rounded-2xl border border-gray-100 shadow-sm p-3">
          <SpaceTree apiService={apiService} selectedSpaceId={selectedSpace?.id} onSelect={handleSelectSpace} />
        </div>

        <div className="md:col-span-3 space-y-4">
          {!selectedSpace && (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-8 text-center text-gray-400">
              Select a wiki space on the left to browse its pages.
            </div>
          )}

          {selectedSpace && creatingUnderParentId !== undefined && (
            <WikiPageEditor
              isNew={true}
              draftKey={`khoi_draft_wiki_new_${selectedSpace.id}_${creatingUnderParentId ?? 'root'}`}
              onSave={handleCreate}
              onCancel={() => setCreatingUnderParentId(undefined)}
            />
          )}

          {selectedSpace && creatingUnderParentId === undefined && !selectedPageId && (
            <>
              <div className="flex justify-between items-center">
                <div className="flex items-center text-sm text-gray-500 space-x-1">
                  <button onClick={() => setBreadcrumb([])} className="hover:text-blue-600 font-medium">
                    {selectedSpace.name}
                  </button>
                  {breadcrumb.map((crumb, i) => (
                    <React.Fragment key={crumb.id}>
                      <ChevronRight className="h-4 w-4" />
                      <button onClick={() => handleBreadcrumbClick(i)} className="hover:text-blue-600">
                        {crumb.title}
                      </button>
                    </React.Fragment>
                  ))}
                </div>
                {canWrite && (
                  <button
                    onClick={() => setCreatingUnderParentId(currentParentPageId)}
                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
                  >
                    <Plus className="h-4 w-4" />
                    New Page
                  </button>
                )}
              </div>

              {error && <div className="text-red-600 text-sm">{error}</div>}
              {loadingPages && <div className="text-gray-400">Loading pages...</div>}

              {!loadingPages && (
                <div className="bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100">
                  {pages.length === 0 && (
                    <div className="p-6 text-center text-gray-400">No pages here yet.</div>
                  )}
                  {pages.map((page, index) => (
                    <div
                      key={page.id}
                      draggable={canWrite}
                      onDragStart={() => setDragIndex(index)}
                      onDragOver={(e) => e.preventDefault()}
                      onDrop={() => handleDrop(index)}
                      className={`p-4 hover:bg-gray-50/60 transition-colors flex justify-between items-center ${canWrite ? 'cursor-move' : ''} ${dragIndex === index ? 'opacity-40' : ''}`}
                    >
                      {canWrite && <GripVertical className="h-4 w-4 mr-1 text-gray-300 flex-shrink-0" />}
                      <button
                        onClick={() => setSelectedPageId(page.id)}
                        className="flex items-center text-left flex-1 min-w-0"
                      >
                        <FileText className="h-4 w-4 mr-2 text-gray-400 flex-shrink-0" />
                        <span className="font-medium text-gray-900 truncate">{page.title}</span>
                        {page.labels?.length > 0 && (
                          <span className="ml-2 flex space-x-1 flex-shrink-0">
                            {page.labels.map((l) => (
                              <span key={l} className="inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium bg-gray-100 text-gray-700">{l}</span>
                            ))}
                          </span>
                        )}
                      </button>
                      <div className="flex items-center space-x-3 flex-shrink-0 ml-2">
                        {canWrite && moveTargets.length > 0 && (
                          <select
                            value=""
                            onChange={(e) => handleMove(page, e.target.value === 'null' ? null : Number(e.target.value))}
                            className="text-xs border border-gray-300 rounded-md px-1.5 py-1 text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                          >
                            <option value="" disabled>Move to...</option>
                            {moveTargets.map((t) => (
                              <option key={t.id ?? 'root'} value={t.id ?? 'null'}>{t.label}</option>
                            ))}
                          </select>
                        )}
                        <button
                          onClick={() => handleDrillInto(page)}
                          className="text-xs text-blue-600 hover:text-blue-800"
                        >
                          Sub-pages →
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </>
          )}

          {selectedPageId && (
            <WikiPageDetail
              apiService={apiService}
              pageId={selectedPageId}
              myEffectiveLevel={selectedSpace.myEffectiveLevel}
              currentUserId={user?.id}
              onDeleted={() => { setSelectedPageId(null); loadPages(selectedSpace.id, currentParentPageId); }}
              onAddSubPage={(parentId) => { setSelectedPageId(null); setCreatingUnderParentId(parentId); }}
            />
          )}
        </div>
      </div>
    </div>
  );
};

export default WikiPage;
