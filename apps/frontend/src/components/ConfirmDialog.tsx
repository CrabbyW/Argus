import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  makeStyles,
  tokens,
} from '@fluentui/react-components';

/**
 * The confirm button is red, not the theme's blue. Every dialog this component opens asks about
 * something destructive — decommissioning an installation, removing a lookup row or a user — and
 * a blue primary button reads as "the safe default" on the one press that is not undoable from
 * the screen it is on. Fluent v9 has no destructive appearance, so it is the red palette applied
 * to the primary button's own shape.
 */
const useStyles = makeStyles({
  destructive: {
    backgroundColor: tokens.colorPaletteRedBackground3,
    color: tokens.colorNeutralForegroundOnBrand,
    border: 'none',
    ':hover': {
      backgroundColor: tokens.colorPaletteRedForeground1,
      color: tokens.colorNeutralForegroundOnBrand,
    },
    ':hover:active': {
      backgroundColor: tokens.colorPaletteRedForeground3,
      color: tokens.colorNeutralForegroundOnBrand,
    },
  },
});

interface Props {
  title: string;
  /** What exactly is about to happen — name the record, not just "this item". */
  message: string;
  confirmLabel?: string;
  isBusy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Replaces `window.confirm`, which renders an OS dialog with the page's origin in it and
 * ignores the app's theme entirely.
 */
export function ConfirmDialog({
  title,
  message,
  confirmLabel = 'Delete',
  isBusy = false,
  onConfirm,
  onCancel,
}: Props) {
  const styles = useStyles();

  return (
    <Dialog open onOpenChange={(_, data) => !data.open && onCancel()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{title}</DialogTitle>
          <DialogContent>{message}</DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onCancel} disabled={isBusy}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              className={styles.destructive}
              onClick={onConfirm}
              disabled={isBusy}
            >
              {confirmLabel}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
