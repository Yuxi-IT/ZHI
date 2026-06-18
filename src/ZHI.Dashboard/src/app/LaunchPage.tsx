import { useState, useEffect, useCallback } from 'react';
import { Button, Modal, useOverlayState, Card, TextField, Label, Input, Form, Spinner } from '@heroui/react';
import { EmptyState } from '../components/empty-state';
import { ThemeSwitcher } from '../components/ThemeSwitcher';
import { LangSwitcher } from '../components/LangSwitcher';
import { ConfigFormFields, DEFAULT_CONFIG, type ZhiConfig } from '../components/ConfigSections';
import { Plus, TrashBin, CircleCheckFill, CircleXmarkFill, CircleFill, Play } from '@gravity-ui/icons';
import type { WorldMeta } from '../types';
import { useT } from '../i18n/I18nContext';

interface Props {
  onWorldStart: (name: string) => void;
}

export function LaunchPage({ onWorldStart }: Props) {
  const [worlds, setWorlds] = useState<WorldMeta[]>([]);
  const [loading, setLoading] = useState(true);
  const [createName, setCreateName] = useState('');
  const [createSeed, setCreateSeed] = useState('');
  const [createDesc, setCreateDesc] = useState('');
  const [createConfig, setCreateConfig] = useState<ZhiConfig>({ ...DEFAULT_CONFIG });
  const [creating, setCreating] = useState(false);
  const { t } = useT();

  const createModal = useOverlayState({ defaultOpen: false });

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
        config: createConfig,
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
      createModal.close();
      setCreateName('');
      setCreateSeed('');
      setCreateDesc('');
      setCreateConfig({ ...DEFAULT_CONFIG });
      await loadWorlds();
    } catch (err) {
      console.error('Failed to create world:', err);
    }
    setCreating(false);
  };

  const handleDelete = async (name: string) => {
    if (!confirm(t('launch.deleteConfirm', { name }))) return;
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
          <div className="flex items-center gap-4">
            <div>
              <h1 className="text-lg font-semibold text-zhi-text tracking-wide">ZHI</h1>
              <p className="text-xs text-zhi-muted mt-0.5">{t('launch.subtitle')}</p>
            </div>
            <ThemeSwitcher />
            <LangSwitcher />
          </div>
          <Button
            variant="primary"
            size="sm"
            className="text-white"
            onPress={() => { setCreateConfig({ ...DEFAULT_CONFIG }); createModal.open(); }}
          >
            <Plus className="size-3.5" />
            {t('launch.newWorld')}
          </Button>
        </div>
      </header>

      {/* World list */}
      <main className="flex-1 px-6 py-8 max-w-5xl mx-auto w-full">
        {loading ? (
          <div className="flex items-center justify-center py-16 gap-2">
            <Spinner size="sm" />
            <span className="text-zhi-muted text-xs animate-pulse">{t('launch.loading')}</span>
          </div>
        ) : worlds.length === 0 ? (
          <div className="flex items-center justify-center py-16">
            <EmptyState>
              <EmptyState.Title>{t('launch.noWorlds')}</EmptyState.Title>
              <EmptyState.Description>{t('launch.noWorldsDesc')}</EmptyState.Description>
              <EmptyState.Content>
                <Button variant="outline" size="sm" onPress={() => { setCreateConfig({ ...DEFAULT_CONFIG }); createModal.open(); }}>
                  <Plus className="size-3.5" />
                  {t('launch.createWorld')}
                </Button>
              </EmptyState.Content>
            </EmptyState>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {worlds.map((w) => (
              <Card
                key={w.name}
                variant="secondary"
                className={`bg-zhi-panel border cursor-pointer transition-colors hover:border-zhi-muted ${
                  w.status === 'running' ? 'border-green-700/50' :
                  w.status === 'crashed' ? 'border-red-700/50' :
                  'border-zhi-border'
                }`}
                onClick={() => w.status !== 'running' && handleStart(w.name)}
              >
                <Card.Header>
                  <div className="flex items-center justify-between w-full">
                    <Card.Title className="text-lg text-zhi-text truncate">{w.name}</Card.Title>
                    <StatusBadge status={w.status} />
                  </div>
                </Card.Header>

                <Card.Content>
                  <div className="text-[14px] text-zhi-muted space-y-1">
                    <Row label={t('launch.gen')} value={w.total_generations} />
                    <Row label={t('launch.deaths')} value={w.total_deaths} />
                    {w.seed != null && <Row label={t('launch.seed')} value={w.seed} mono />}
                  </div>
                </Card.Content>

                <Card.Footer>
                  <div className="flex items-center justify-between w-full">
                    <span className="text-[12px] text-zhi-muted">
                      {formatDate(w.created_at)}
                    </span>
                    <div className="flex items-center gap-1">
                      {w.status !== 'running' && (
                      <Button
                        isIconOnly
                        variant="ghost"
                        className="text-blue-400 hover:text-blue-300 min-w-0 w-5 h-5"
                        aria-label={t('launch.startAria', { name: w.name })}
                        onPress={() => handleStart(w.name)}
                      >
                        <Play className="size-4" />
                      </Button>
                      )}
                      <Button
                        isIconOnly
                        variant="ghost"
                        className="text-red-400 hover:text-red-300 min-w-0 w-5 h-5"
                        aria-label={t('launch.deleteAria', { name: w.name })}
                        onPress={() => { handleDelete(w.name); }}
                      >
                        <TrashBin className="size-4" />
                      </Button>
                    </div>
                  </div>
                </Card.Footer>
              </Card>
            ))}
          </div>
        )}
      </main>

      {/* Create Modal */}
      <Modal>
        <Modal.Backdrop
          variant="opaque"
          isDismissable
          isOpen={createModal.isOpen}
          onOpenChange={(open: boolean) => {
            if (open) createModal.open();
            else createModal.close();
          }}
        >
          <Modal.Container placement="center" size="lg">
            <Modal.Dialog className="max-h-[85vh]">
              <Modal.CloseTrigger />
              <Modal.Header>
                <Modal.Heading className="text-zhi-text text-sm">{t('launch.createTitle')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body className="overflow-y-auto">
                <Form className="space-y-3" onSubmit={(e: React.FormEvent) => { e.preventDefault(); handleCreate(); }}>
                  <TextField isRequired>
                    <Label className="text-[10px] text-zhi-muted">{t('launch.name')}</Label>
                    <Input
                      className="bg-zhi-bg border border-zhi-border px-2.5 py-1.5 text-xs text-zhi-text"
                      placeholder={t('launch.namePlaceholder')}
                      value={createName}
                      onChange={(e: React.ChangeEvent<HTMLInputElement>) => setCreateName(e.target.value)}
                      autoFocus
                    />
                  </TextField>

                  <div className="flex gap-3">
                    <TextField className="flex-1">
                      <Label className="text-[10px] text-zhi-muted">{t('launch.seedLabel')}</Label>
                      <Input
                        type="number"
                        className="bg-zhi-bg border border-zhi-border px-2.5 py-1.5 text-xs text-zhi-text font-mono"
                        placeholder={t('launch.seedPlaceholder')}
                        value={createSeed}
                        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setCreateSeed(e.target.value)}
                      />
                    </TextField>
                    <TextField className="flex-1">
                      <Label className="text-[10px] text-zhi-muted">{t('launch.desc')}</Label>
                      <Input
                        className="bg-zhi-bg border border-zhi-border px-2.5 py-1.5 text-xs text-zhi-text"
                        placeholder={t('launch.descPlaceholder')}
                        value={createDesc}
                        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setCreateDesc(e.target.value)}
                      />
                    </TextField>
                  </div>

                  <ConfigFormFields config={createConfig} update={(section, key, value) => {
                    setCreateConfig(prev => ({ ...prev, [section]: { ...prev[section], [key]: value } }));
                  }} />
                </Form>
              </Modal.Body>
              <Modal.Footer>
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-zhi-muted"
                  onPress={() => createModal.close()}
                >
                  {t('launch.cancel')}
                </Button>
                <Button
                  variant="primary"
                  size="sm"
                  className="text-white disabled:opacity-50"
                  isDisabled={!createName.trim() || creating}
                  onPress={handleCreate}
                >
                  {creating ? t('launch.creating') : t('launch.createAndStart')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const Icon = status === 'running' ? CircleCheckFill :
    status === 'crashed' ? CircleXmarkFill : CircleFill;
  const color = status === 'running' ? 'text-green-400' :
    status === 'crashed' ? 'text-red-400' : 'text-zhi-muted';
  return (
    <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-medium inline-flex items-center gap-1 ${color}`}>
      <Icon className="size-2" />
      {status}
    </span>
  );
}

function Row({ label, value, mono }: { label: string; value: string | number; mono?: boolean }) {
  return (
    <div className="flex justify-between">
      <span>{label}</span>
      <span className={`text-zhi-text ${mono ? 'font-mono' : ''}`}>{value}</span>
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
