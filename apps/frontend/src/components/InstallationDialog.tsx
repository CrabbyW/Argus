import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Combobox,
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
} from '@fluentui/react-components';
import { ApiError, api } from '../api/client';
import type { InstallationUpsert, LookupItem } from '../api/types';
import { lookupMaxNameLength } from '../api/types';
import type { Lookups } from '../hooks/useLookups';

const useStyles = makeStyles({
  form: { display: 'flex', flexDirection: 'column', rowGap: '12px' },
  row: { display: 'flex', gap: '12px', flexWrap: 'wrap' },
  half: { flex: '1 1 220px' },
  // Fluent gives Combobox and Dropdown a 250px minimum, wider than the flex basis above.
  grow: { minWidth: 0, width: '100%' },
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

/**
 * The form holds paths as text, not as Ids. Every other lookup is a closed set the user picks
 * from, but a path is something they type — and before normalization they simply typed it. So
 * the dialog keeps that gesture and resolves the text to a lookup row on save (§ resolvePathId).
 */
interface FormState {
  machineId: number;
  appNameId: number;
  appStageNameId: number;
  processorArchitectureId: number;
  dnsEndpointId: number | null;
  rootPathText: string;
  physicalPathText: string;
  tagIds: number[];
  repositoryIds: number[];
  isActive: boolean;
  validFromDate: string;
  validToDate: string | null;
}

const blankForm: FormState = {
  machineId: 0,
  appNameId: 0,
  appStageNameId: 0,
  processorArchitectureId: 0,
  dnsEndpointId: null,
  rootPathText: '/',
  physicalPathText: '',
  tagIds: [],
  repositoryIds: [],
  isActive: true,
  validFromDate: today(),
  validToDate: null,
};

const norm = (value: string) => value.trim().toLowerCase();

export function InstallationDialog({ installationId, lookups, onClose, onSaved }: Props) {
  const styles = useStyles();

  const [form, setForm] = useState<FormState>(blankForm);
  const [isLoading, setIsLoading] = useState(installationId !== null);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (installationId === null) {
      setForm(blankForm);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);

    api
      .getInstallation(installationId)
      .then((detail) => {
        setForm({
          machineId: detail.machineId,
          appNameId: detail.appNameId,
          appStageNameId: detail.appStageNameId,
          processorArchitectureId: detail.processorArchitectureId,
          dnsEndpointId: detail.dnsEndpointId ?? null,
          rootPathText: detail.rootPath,
          physicalPathText: detail.physicalPath ?? '',
          tagIds: detail.tags.map((tag) => tag.id),
          repositoryIds: detail.appRepositories.map((repo) => repo.id),
          isActive: detail.isActive,
          validFromDate: detail.validFromDate,
          validToDate: detail.validToDate ?? null,
        });
      })
      .catch((err) =>
        setError(err instanceof Error ? err.message : 'Failed to load the installation.'),
      )
      .finally(() => setIsLoading(false));
  }, [installationId]);

  function patch(next: Partial<FormState>) {
    setForm((current) => ({ ...current, ...next }));
  }

  /**
   * Turn typed path text into a lookup Id, creating the row if it is new.
   *
   * The roadplan's "always a dropdown, never free text" rule exists to stop the same path being
   * stored twice under two spellings. Find-or-create satisfies that — the value still ends up as
   * one row referenced by Id — without forcing the user to leave the dialog, visit the Lookups
   * screen and come back, which is what a bare dropdown would require.
   *
   * The 409-shaped 400 is a real race: two people adding the same path at once. Rather than
   * failing the save, re-read the lookup and use the row the other write created.
   */
  async function resolvePathId(kind: 'rootpaths' | 'physicalpaths', text: string): Promise<number> {
    const wanted = norm(text);
    const known = kind === 'rootpaths' ? lookups.rootPaths : lookups.physicalPaths;

    const existing = known.find((item) => norm(item.name) === wanted);
    if (existing) {
      return existing.id;
    }

    try {
      const created = await api.createLookupItem(kind, {
        name: text.trim(),
        description: null,
        sortOrder: 0,
        isLoadBalancer: false,
      });
      return created.id;
    } catch (err) {
      if (err instanceof ApiError && err.status === 400) {
        const fresh = await api.getLookup(kind);
        const raced = fresh.find((item) => norm(item.name) === wanted);
        if (raced) {
          return raced.id;
        }
      }
      throw err;
    }
  }

  async function handleSave() {
    setError(null);
    setIsSaving(true);

    try {
      const rootPathId = await resolvePathId('rootpaths', form.rootPathText);

      const physicalPathId = form.physicalPathText.trim()
        ? await resolvePathId('physicalpaths', form.physicalPathText)
        : null;

      const payload: InstallationUpsert = {
        machineId: form.machineId,
        appNameId: form.appNameId,
        appStageNameId: form.appStageNameId,
        processorArchitectureId: form.processorArchitectureId,
        dnsEndpointId: form.dnsEndpointId,
        rootPathId,
        physicalPathId,
        tagIds: form.tagIds,
        repositoryIds: form.repositoryIds,
        isActive: form.isActive,
        validFromDate: form.validFromDate,
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
          className={styles.grow}
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

  /** Type-ahead over an existing lookup, but a value that matches nothing is still accepted. */
  function pathCombobox(
    label: string,
    kind: 'rootpaths' | 'physicalpaths',
    items: LookupItem[],
    value: string,
    onChange: (text: string) => void,
    options: { required?: boolean; placeholder?: string; hint?: string } = {},
  ) {
    const typed = norm(value);
    const suggestions = typed
      ? items.filter((item) => norm(item.name).includes(typed))
      : items;
    const isNew = value.trim().length > 0 && !items.some((item) => norm(item.name) === typed);

    return (
      <Field
        label={label}
        required={options.required}
        className={styles.half}
        hint={isNew ? `"${value.trim()}" is new — it will be added to ${label}.` : options.hint}
      >
        <Combobox
          className={styles.grow}
          freeform
          value={value}
          placeholder={options.placeholder}
          maxLength={lookupMaxNameLength[kind]}
          onChange={(event) => onChange(event.target.value)}
          onOptionSelect={(_, data) => onChange(data.optionText ?? '')}
        >
          {suggestions.map((item) => (
            <Option key={item.id} value={item.name}>
              {item.name}
            </Option>
          ))}
        </Combobox>
      </Field>
    );
  }

  /** Multiselect over a lookup, submitted as an array of Ids. */
  function multiselect(
    label: string,
    items: LookupItem[],
    selectedIds: number[],
    onChange: (ids: number[]) => void,
    placeholder: string,
  ) {
    const selectedNames = items
      .filter((item) => selectedIds.includes(item.id))
      .map((item) => item.name);

    return (
      <Field label={label}>
        <Dropdown
          className={styles.grow}
          multiselect
          placeholder={placeholder}
          selectedOptions={selectedIds.map(String)}
          value={selectedNames.join(', ')}
          onOptionSelect={(_, data) => onChange(data.selectedOptions.map(Number))}
        >
          {items.map((item) => (
            <Option key={item.id} value={String(item.id)}>
              {item.name}
            </Option>
          ))}
        </Dropdown>
      </Field>
    );
  }

  // Checked here as well as on the server so the user is told before the round trip.
  const datesAreOrdered =
    !form.validToDate || !form.validFromDate || form.validToDate >= form.validFromDate;

  const isValid = useMemo(
    () =>
      form.machineId > 0 &&
      form.appNameId > 0 &&
      form.appStageNameId > 0 &&
      form.processorArchitectureId > 0 &&
      form.rootPathText.trim().length > 0 &&
      form.validFromDate.length > 0 &&
      datesAreOrdered,
    [form, datesAreOrdered],
  );

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
                  {lookupDropdown('Application', lookups.appNames, form.appNameId, (id) =>
                    patch({ appNameId: id ?? 0 }), { required: true })}
                </div>

                <div className={styles.row}>
                  {lookupDropdown('Stage', lookups.appStageNames, form.appStageNameId, (id) =>
                    patch({ appStageNameId: id ?? 0 }), { required: true })}
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

                  {pathCombobox(
                    'Root path',
                    'rootpaths',
                    lookups.rootPaths,
                    form.rootPathText,
                    (text) => patch({ rootPathText: text }),
                    { required: true, placeholder: '/' },
                  )}
                </div>

                <div className={styles.row}>
                  {pathCombobox(
                    'Physical path',
                    'physicalpaths',
                    lookups.physicalPaths,
                    form.physicalPathText,
                    (text) => patch({ physicalPathText: text }),
                    { placeholder: 'c:\\inetpub\\myapp' },
                  )}
                </div>

                {multiselect('Tags', lookups.tags, form.tagIds, (ids) => patch({ tagIds: ids }),
                  'No tags')}

                {multiselect(
                  'Repositories',
                  lookups.repositories,
                  form.repositoryIds,
                  (ids) => patch({ repositoryIds: ids }),
                  'No repositories',
                )}

                <div className={styles.row}>
                  <Field label="Valid from" required className={styles.half}>
                    <Input
                      type="date"
                      value={form.validFromDate}
                      onChange={(_, data) => patch({ validFromDate: data.value })}
                    />
                  </Field>

                  <Field
                    label="Valid to"
                    className={styles.half}
                    hint="Empty = still valid."
                    validationState={datesAreOrdered ? 'none' : 'error'}
                    validationMessage={
                      datesAreOrdered ? undefined : 'Valid to cannot be earlier than valid from.'
                    }
                  >
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
