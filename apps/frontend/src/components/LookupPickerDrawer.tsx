import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  DrawerBody,
  DrawerFooter,
  DrawerHeader,
  DrawerHeaderTitle,
  Input,
  OverlayDrawer,
  Text,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { CheckmarkRegular, DismissRegular, SearchRegular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  // The body scrolls, the search does not: `overflow: hidden` here keeps the drawer's own height
  // and hands scrolling to the list below, which is what makes the sticky header possible.
  body: { display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' },

  // Stays put while the values scroll under it. A lookup can run to hundreds of rows, and a
  // search box that scrolls away is unreachable exactly when the list is long enough to need it.
  searchBar: {
    position: 'sticky',
    top: 0,
    zIndex: 1,
    paddingBottom: '10px',
    backgroundColor: tokens.colorNeutralBackground1,
  },

  list: {
    overflowY: 'auto',
    display: 'flex',
    flexDirection: 'column',
    rowGap: '2px',
    // Room for the last row's focus ring, which a flush edge would clip.
    paddingBottom: '4px',
  },

  // A row is a button: it is clicked, focused and reached by keyboard like one, and looking like
  // a list item is a matter of alignment, not of being a different element.
  option: {
    justifyContent: 'flex-start',
    width: '100%',
    textAlign: 'left',
    fontWeight: tokens.fontWeightRegular,
  },
  optionSelected: {
    backgroundColor: tokens.colorNeutralBackground1Selected,
    fontWeight: tokens.fontWeightSemibold,
  },
  // "All" clears the filter, so it is set apart from the values it sits above.
  clearOption: { borderBottom: `1px solid ${tokens.colorNeutralStroke2}`, paddingBottom: '6px' },

  empty: { color: tokens.colorNeutralForeground3, padding: '12px 4px' },

  footer: { display: 'flex', alignItems: 'center', gap: '8px', width: '100%' },
  spacer: { flexGrow: 1 },
  count: { color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase200 },
});

interface Props {
  /** The lookup being picked from, e.g. "Machine" — shown as the drawer's title. */
  label: string;
  items: { id: number; name: string }[];
  /** What is selected when the panel opens. One id, or several where the facet allows it. */
  selected: number[];
  /**
   * Several values may be picked. The panel behaves the same either way — nothing is applied
   * until Search — but a single-choice list replaces the previous pick instead of adding to it.
   */
  multiple?: boolean;
  /** What the empty choice is called, e.g. "All machines". */
  clearLabel: string;
  onApply: (selected: number[]) => void;
  onClose: () => void;
}

export function LookupPickerDrawer({
  label,
  items,
  selected,
  multiple = false,
  clearLabel,
  onApply,
  onClose,
}: Props) {
  const styles = useStyles();
  const [search, setSearch] = useState('');
  const searchRef = useRef<HTMLInputElement>(null);

  /**
   * Only a multi-select panel stages its answer. With several values to pick, one that closed on
   * the first click could never be used to choose the second, so the choice is held here until
   * Search. A single-value panel has nothing to wait for: the click *is* the answer, and making
   * it confirm one value would add a step where there was none.
   */
  const [staged, setStaged] = useState<number[]>(selected);

  // The drawer is opened to choose something, and choosing starts with typing. Focusing the
  // search means the keyboard is already in the right place when the panel finishes opening.
  useEffect(() => {
    searchRef.current?.focus();
  }, []);

  const matches = useMemo(() => {
    const term = search.trim().toLowerCase();

    return term ? items.filter((item) => item.name.toLowerCase().includes(term)) : items;
  }, [items, search]);

  function choose(ids: number[]) {
    if (multiple) {
      setStaged(ids);
      return;
    }

    onApply(ids);
    onClose();
  }

  function toggle(id: number) {
    if (!multiple) {
      choose(staged.includes(id) ? [] : [id]);
      return;
    }

    choose(
      staged.includes(id) ? staged.filter((value) => value !== id) : [...staged, id],
    );
  }

  return (
    <OverlayDrawer open position="end" onOpenChange={(_, data) => !data.open && onClose()}>
      <DrawerHeader>
        <DrawerHeaderTitle
          action={
            <Button
              appearance="subtle"
              icon={<DismissRegular />}
              aria-label="Close"
              onClick={onClose}
            />
          }
        >
          {label}
        </DrawerHeaderTitle>
      </DrawerHeader>

      <DrawerBody>
        <div className={styles.body}>
          <div className={styles.searchBar}>
            <Input
              ref={searchRef}
              placeholder={`Search ${label.toLowerCase()}...`}
              value={search}
              contentBefore={<SearchRegular />}
              onChange={(_, data) => setSearch(data.value)}
            />
          </div>

          <div className={styles.list} role={multiple ? 'group' : undefined}>
            <Button
              appearance="subtle"
              className={mergeClasses(
                styles.option,
                styles.clearOption,
                staged.length === 0 ? styles.optionSelected : undefined,
              )}
              icon={staged.length === 0 ? <CheckmarkRegular /> : undefined}
              onClick={() => choose([])}
            >
              {clearLabel}
            </Button>

            {matches.length === 0 ? (
              <Text className={styles.empty}>Nothing matches "{search}".</Text>
            ) : (
              matches.map((item) => {
                const isStaged = staged.includes(item.id);

                return (
                  <Button
                    key={item.id}
                    appearance="subtle"
                    className={mergeClasses(
                      styles.option,
                      isStaged ? styles.optionSelected : undefined,
                    )}
                    icon={isStaged ? <CheckmarkRegular /> : undefined}
                    aria-pressed={isStaged}
                    onClick={() => toggle(item.id)}
                  >
                    {item.name}
                  </Button>
                );
              })
            )}
          </div>
        </div>
      </DrawerBody>

      {/* Only where the answer is held back. A single-value panel has already applied the click
          by the time it closes, so a footer confirming it would be a button that does nothing. */}
      {multiple && (
        <DrawerFooter>
          <div className={styles.footer}>
            <Text className={styles.count}>
              {staged.length === 0 ? clearLabel : `${staged.length} selected`}
            </Text>

            <div className={styles.spacer} />

            <Button appearance="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              icon={<SearchRegular />}
              onClick={() => {
                onApply(staged);
                onClose();
              }}
            >
              Search
            </Button>
          </div>
        </DrawerFooter>
      )}
    </OverlayDrawer>
  );
}
