import { useCallback } from 'react';
import { Toast, ToastBody, ToastTitle, useToastController } from '@fluentui/react-components';

/** Shared by the <Toaster> in App.tsx and every dispatcher below. */
export const TOASTER_ID = 'argus-toaster';

/**
 * Saving used to be silent — the dialog closed and nothing told you whether the write landed.
 */
export function useAppToast() {
  const { dispatchToast } = useToastController(TOASTER_ID);

  const success = useCallback(
    (title: string) =>
      dispatchToast(
        <Toast>
          <ToastTitle>{title}</ToastTitle>
        </Toast>,
        { intent: 'success' },
      ),
    [dispatchToast],
  );

  const error = useCallback(
    (title: string, detail?: string) =>
      dispatchToast(
        <Toast>
          <ToastTitle>{title}</ToastTitle>
          {detail && <ToastBody>{detail}</ToastBody>}
        </Toast>,
        { intent: 'error', timeout: 8000 },
      ),
    [dispatchToast],
  );

  return { success, error };
}
