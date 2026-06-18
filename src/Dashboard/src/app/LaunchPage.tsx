import { useState, useEffect, useCallback } from 'react';
import { Button } from '@heroui/react';
import type { WorldMeta } from '../types';

interface Props {
  onWorldStart: (name: string) => void;
}

export function LaunchPage({ onWorldStart }: Props) {
  const [worlds, setWorlds] = useState<WorldMeta[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createSeed, setCreateSeed] = useState('');
  const [createDesc, setCreateDesc] = useState('');
  const [creating, setCreating] = useState(false);

  const loadWorlds = useCallback(async () => {
    try {
      const res = await fetch('/api/worlds');
      const data = await res.json();
      setWorlds(data);
    } catch { }
    setLoading(false);
  }, []);

  useEffect(() => { loadWorlds(); }, [loadWorlds]);

  const handleStart = async (name: string) => {
    try {
      await fetch('/api/world/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
      });
      onWorldStart(name);
    } catch (err) {
      console.error('Failed to start world:', err);
    }
  };

  const handleCreate = async () => {
    if (!createName.trim()) return;
    setCreating(true);
    try {
      const body: Record<string, unknown> = {
        name: createName.trim(),
        description: createDesc.trim(),
      };
      const seedNum = parseInt(createSeed, 10);
      if (!isNaN(seedNum)) body.seed = seedNum;
      else body.seed = null;

      const res = await fetch('/api/world/create', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        const err = await res.json();
        alert(err.error || 'Failed to create world');
        return;
      }
      setShowCreate(false);
      setCreateName('');
      setCreateSeed('');
      setCreateDesc('');
      await loadWorlds();
    } catch (err) {
      console.error('Failed to create world:', err);
    }
    setCreating(false);
  };

  const handleDelete = async (name: string) => {
    if (!confirm(`Delete world "${name}"? This cannot be undone.`)) return;
    try {
      await fetch('/api/world/delete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name }),
      });
      await loadWorlds();
    } catch (err) {
      console.error('Failed to delete world:', err);
    }
  };

  return (
    <div className="min-h-screen bg-zhi-bg flex flex-col">
      {/* Header */}
      <header className="border-b border-zhi-border px-6 py-5">
        <div className="max-w-5xl mx-auto flex items-center justify-between">
          <div>
            <h1 className="text-lg font-semibold text-zhi-text tracking-wide">ZHI</h1>
            <p className="text-xs text-zhi-muted mt-0.5">Zero-Hypothesis Intelligence Ecosystem</p>
          </div>
          <Button
            className="bg-zhi-accent text-white text-xs font-medium px-4 py-1.5 rounded-md hover:opacity-90 transition-opacity"
            onPress={() => setShowCreate(true)}
          >
            + New World
          </Button>
        </div>
      </header>

      {/* World list */}
      <main className="flex-1 px-6 py-8 max-w-5xl mx-auto w-full">
        {loading ? (
          <p className="text-zhi-muted text-xs animate-pulse text-center py-16">Loading worlds...</p>
        ) : worlds.length === 0 ? (
          <div className="text-center py-16">
            <p className="text-zhi-muted text-sm mb-3">No worlds yet</p>
            <p className="text-zhi-muted text-xs">
              Create a new world to begin the simulation.
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {worlds.map((w) => (
              <div
                key={w.name}
                className="bg-zhi-panel border border-zhi-border rounded-lg p-4 hover:border-zhi-muted transition-colors cursor-pointer group"
                onClick={() => w.status !== 'running' && handleStart(w.name)}
              >
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-sm font-medium text-zhi-text truncate">{w.name}</h3>
                  <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-medium ${
                    w.status === 'running' ? 'bg-green-900/40 text-green-400' :
                    w.status === 'crashed' ? 'bg-red-900/40 text-red-400' :
                    'bg-zhi-border text-zhi-muted'
                  }`}>
                    {w.status}
                  </span>
                </div>

                <div className="text-[10px] text-zhi-muted space-y-0.5 mb-2">
                  <div className="flex justify-between">
                    <span>Gen</span><span className="text-zhi-text">{w.total_generations}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>Deaths</span><span className="text-zhi-text">{w.total_deaths}</span>
                  </div>
                  {w.seed != null && (
                    <div className="flex justify-between">
                      <span>Seed</span><span className="text-zhi-text font-mono">{w.seed}</span>
                    </div>
                  )}
                </div>

                <div className="flex items-center justify-between mt-3 pt-2 border-t border-zhi-border">
                  <span className="text-[10px] text-zhi-muted">
                    {formatDate(w.created_at)}
                  </span>
                  <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    {w.status !== 'running' && (
                      <span className="text-[10px] text-zhi-accent font-medium">Start →</span>
                    )}
                    <button
                      className="text-[10px] text-red-400 hover:text-red-300 ml-1.5"
                      onClick={(e) => { e.stopPropagation(); handleDelete(w.name); }}
                    >
                      Del
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>

      {/* Create modal */}
      {showCreate && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50" onClick={() => setShowCreate(false)}>
          <div className="bg-zhi-panel border border-zhi-border rounded-lg p-6 w-full max-w-sm" onClick={(e) => e.stopPropagation()}>
            <h2 className="text-sm font-semibold text-zhi-text mb-4">New World</h2>

            <div className="space-y-3">
              <div>
                <label className="text-[10px] text-zhi-muted block mb-1">Name</label>
                <input
                  type="text"
                  className="w-full bg-zhi-bg border border-zhi-border rounded px-2.5 py-1.5 text-xs text-zhi-text outline-none focus:border-zhi-accent"
                  placeholder="my-world"
                  value={createName}
                  onChange={(e) => setCreateName(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                  autoFocus
                />
              </div>

              <div>
                <label className="text-[10px] text-zhi-muted block mb-1">Seed (optional, leave empty for random)</label>
                <input
                  type="number"
                  className="w-full bg-zhi-bg border border-zhi-border rounded px-2.5 py-1.5 text-xs text-zhi-text outline-none focus:border-zhi-accent font-mono"
                  placeholder="42"
                  value={createSeed}
                  onChange={(e) => setCreateSeed(e.target.value)}
                />
              </div>

              <div>
                <label className="text-[10px] text-zhi-muted block mb-1">Description (optional)</label>
                <input
                  type="text"
                  className="w-full bg-zhi-bg border border-zhi-border rounded px-2.5 py-1.5 text-xs text-zhi-text outline-none focus:border-zhi-accent"
                  placeholder="Testing hypothermia..."
                  value={createDesc}
                  onChange={(e) => setCreateDesc(e.target.value)}
                />
              </div>
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <Button
                className="text-xs text-zhi-muted px-3 py-1 rounded bg-zhi-border hover:bg-zhi-muted transition-colors"
                onPress={() => setShowCreate(false)}
              >
                Cancel
              </Button>
              <Button
                className="text-xs bg-zhi-accent text-white px-3 py-1 rounded hover:opacity-90 transition-opacity disabled:opacity-50"
                onPress={handleCreate}
                isDisabled={!createName.trim() || creating}
              >
                {creating ? 'Creating...' : 'Create & Start'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso.slice(0, 10);
  }
}
