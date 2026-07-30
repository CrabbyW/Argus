import { useEffect, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  Spinner,
  Switch,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { api } from '../api/client';
import type { InstallationUpsert, LookupItem } from '../api/types';
import { repositoryTypeNames } from '../api/types';
import type { Lookups } from '../hooks/useLookups';

const useStyles = makeStyles({
  form: { display: 'flex', flexDirection: 'column', rowGap: '12px' },
  row: { display: 'flex', gap: '12px', flexWrap: 'wrap' },
  half: { flex: '1 1 220px' },
  repos: { margin: 0, paddingLeft: '20px', color: tokens.colorNeutralForeground3 },
});

interface Props {
  /** null = create a new installation. */
  installationId: number | null;
  lookups: Lookups;
  onClose: () => void;
  onSaved: () => void;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

const blankForm: InstallationUpsert = {
  machineId: 0,
  applicationId: 0,
  appStageId: 0,
  processorArchitectureId: 0,
  dnsEndpointId: null,
  rootPath: '/',
  physicalPath: '',
  tags: '',
  isActive: true,
  validFromDate: today(),
  validToDate: null,
};

export function InstallationDialog({ installationId, lookups, onClose, onSaved }: Props) {
  const styles = useStyles();

  const [form, setForm] = useState<InstallationUpsert>(blankForm);
  const [repositories, setRepositories] = useState<{ id: number; label: string }[]>([]);
  const [isLoading, setIsLoading] = useState(installationId !== null);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (installationId === null) {
      setForm(blankForm);
      setRepositories([]);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);

    api
      .getInstallation(installationId)
      .then((detail) => {
        setForm({
          machineId: detail.machineId,
          applicationId: detail.applicationId,
          appStageId: detail.appStageId,
          processorArchitectureId: detail.processorArchitectureId,
          dnsEndpointId: detail.dnsEndpointId ?? null,
          rootPath: detail.rootPath,
          physicalPath: detail.physicalPath ?? '',
          tags: detail.tags ?? '',
          isActive: detail.isActive,
          validFromDate: detail.validFromDate,
          validToDate: detail.validToDate ?? null,
        });

        setRepositories(
          detail.appRepositories.map((repo) => ({
            id: repo.id,
            label: `${repositoryTypeNames[repo.repositoryType] ?? 'Unknown'} — ${repo.repositoryUrl}`,
          })),
        );
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load the installation.'))
      .finally(() => setIsLoading(false));
  }, [installationId]);

  function patch(next: Partial<InstallationUpsert>) {
    setForm((current) => ({ ...current, ...next }));
  }

  async function handleSave() {
    setError(null);
    setIsSaving(true);

    try {
      const payload: InstallationUpsert = {
        ...form,
        // Empty strings mean "not set" for the optional text fields.
        physicalPath: form.physicalPath?.trim() ? form.physicalPath : null,
        tags: form.tags?.trim() ? form.tags : null,
        validToDate: form.validToDate ? form.validToDate : null,
      };

      if (installationId === null) {
        await api.createInstallation(payload);
      } else {
        await api.updateInstallation(installationId, payload);
      }

      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save the installation.');
    } finally {
      setIsSaving(false);
    }
  }

  function lookupDropdown(
    label: string,
    items: LookupItem[],
    selectedId: number | null | undefined,
    onSelect: (id: number | null) => void,
    options: { required?: boolean; allowEmpty?: boolean } = {},
  ) {
    const selected = items.find((item) => item.id === selectedId);

    return (
      <Field label={label} required={options.required} className={styles.half}>
        <Dropdown
          placeholder={options.allowEmpty ? '(none)' : `Select ${label.toLowerCase()}`}
          selectedOptions={selectedId ? [String(selectedId)] : []}
          value={selected?.name ?? ''}
          onOptionSelect={(_, data) => onSelect(data.optionValue ? Number(data.optionValue) : null)}
        >
          {options.allowEmpty && <Option value="">(none)</Option>}
          {items.map((item) => (
            <Option key={item.id} value={String(item.id)}>
              {item.name}
            </Option>
          ))}
        </Dropdown>
      </Field>
    );
  }

  const isValid =
    form.machineId > 0 &&
    form.applicationId > 0 &&
    form.appStageId > 0 &&
    form.processorArchitectureId > 0 &&
    form.rootPath.trim().length > 0;

  return (
    <Dialog open onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>
            {installationId === null ? 'New installation' : 'Edit installation'}
          </DialogTitle>

          <DialogContent>
            {isLoading ? (
              <Spinner label="Loading..." />
            ) : (
              <div className={styles.form}>
                {error && (
                  <MessageBar intent="error">
                    <MessageBarBody>{error}</MessageBarBody>
                  </MessageBar>
                )}

                <div className={styles.row}>
                  {lookupDropdown('Machine', lookups.machines, form.machineId, (id) =>
                    patch({ machineId: id ?? 0 }), { required: true })}
                  {lookupDropdown('Application', lookups.applications, form.applicationId, (id) =>
                    patch({ applicationId: id ?? 0 }), { required: true })}
                </div>

                <div className={styles.row}>
                  {lookupDropdown('Stage', lookups.appStages, form.appStageId, (id) =>
                    patch({ appStageId: id ?? 0 }), { required: true })}
                  {lookupDropdown(
                    'Architecture',
                    lookups.processorArchitectures,
                    form.processorArchitectureId,
                    (id) => patch({ processorArchitectureId: id ?? 0 }),
                    { required: true },
                  )}
                </div>

                <div className={styles.row}>
                  {lookupDropdown('DNS endpoint', lookups.dnsEndpoints, form.dnsEndpointId, (id) =>
                    patch({ dnsEndpointId: id }), { allowEmpty: true })}

                  <Field label="Root path" required className={styles.half}>
                    <Input
                      value={form.rootPath}
                      onChange={(_, data) => patch({ rootPath: data.value })}
                      placeholder="/"
                    />
                  </Field>
                </div>

                <Field label="Physical path">
                  <Input
                    value={form.physicalPath ?? ''}
                    onChange={(_, data) => patch({ physicalPath: data.value })}
                    placeholder="c:\inetpub\myapp"
                  />
                </Field>

                <Field label="Tags" hint="Free text for now; becomes its own table in PHASE2.">
                  <Input
                    value={form.tags ?? ''}
                    onChange={(_, data) => patch({ tags: data.value })}
                    placeholder="web;prod"
                  />
                </Field>

                <div className={styles.row}>
                  <Field label="Valid from" required className={styles.half}>
                    <Input
                      type="date"
                      value={form.validFromDate}
                      onChange={(_, data) => patch({ validFromDate: data.value })}
                    />
                  </Field>

                  <Field label="Valid to" hint="Empty = still valid." className={styles.half}>
                    <Input
                      type="date"
                      value={form.validToDate ?? ''}
                      onChange={(_, data) => patch({ validToDate: data.value || null })}
                    />
                  </Field>
                </div>

                <Switch
                  checked={form.isActive}
                  onChange={(_, data) => patch({ isActive: data.checked })}
                  label="Currently serving (IsActive)"
                />

                {repositories.length > 0 && (
                  <Field label="Application repositories">
                    <ul className={styles.repos}>
                      {repositories.map((repo) => (
                        <li key={repo.id}>{repo.label}</li>
                      ))}
                    </ul>
                  </Field>
                )}
              </div>
            )}
          </DialogContent>

          <DialogActions>
            <Button appearance="secondary" onClick={onClose} disabled={isSaving}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              onClick={() => void handleSave()}
              disabled={isSaving || isLoading || !isValid}
            >
              {isSaving ? <Spinner size="tiny" label="Saving..." /> : 'Save'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
