// src/components/Wiki/WikiPage.js
import React, { useState, useEffect } from 'react';
import { Plus, BookOpen, FileText, ChevronRight } from 'lucide-react';
import SpaceTree from '../Spaces/SpaceTree';
import WikiPageDetail from './WikiPageDetail';
import WikiPageEditor from './WikiPageEditor';
import { hasSpaceLevel } from '../../utils/spaceLevel';

const WikiPage = ({ apiService, user, deepLink }) => {
  const [selectedSpace, setSelectedSpace] = useState(null);
  // breadcrumb: array of { id, title } - null parentPageId means Space root
  const [breadcrumb, setBreadcrumb] = useState([]);
  const [pages, setPages] = useState([]);
  const [loadingPages, setLoadingPages] = useState(false);
  const [error, setError] = useState(null);
  const [selectedPageId, setSelectedPageId] = useState(null);
  const [creatingUnderParentId, setCreatingUnderParentId] = useState(undefined); // undefined = not creating

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

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-3xl font-bold text-gray-900 flex items-center">
          <BookOpen className="h-7 w-7 mr-2 text-gray-700" />
          Wiki
        </h2>
        <p className="text-gray-600">Team documentation and knowledge base</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="md:col-span-1 bg-white rounded-lg shadow p-3">
          <SpaceTree apiService={apiService} selectedSpaceId={selectedSpace?.id} onSelect={handleSelectSpace} />
        </div>

        <div className="md:col-span-3 space-y-4">
          {!selectedSpace && (
            <div className="bg-white rounded-lg shadow p-8 text-center text-gray-400">
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
                    className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center"
                  >
                    <Plus className="h-5 w-5 mr-2" />
                    New Page
                  </button>
                )}
              </div>

              {error && <div className="text-red-600 text-sm">{error}</div>}
              {loadingPages && <div className="text-gray-400">Loading pages...</div>}

              {!loadingPages && (
                <div className="bg-white rounded-lg shadow divide-y">
                  {pages.length === 0 && (
                    <div className="p-6 text-center text-gray-400">No pages here yet.</div>
                  )}
                  {pages.map((page) => (
                    <div key={page.id} className="p-4 hover:bg-gray-50 flex justify-between items-center">
                      <button
                        onClick={() => setSelectedPageId(page.id)}
                        className="flex items-center text-left flex-1"
                      >
                        <FileText className="h-4 w-4 mr-2 text-gray-400" />
                        <span className="font-medium text-gray-900">{page.title}</span>
                      </button>
                      <button
                        onClick={() => handleDrillInto(page)}
                        className="text-xs text-blue-600 hover:text-blue-800 ml-2"
                      >
                        Sub-pages →
                      </button>
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
