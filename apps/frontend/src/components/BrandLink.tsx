import { useEffect, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { makeStyles, tokens } from '@fluentui/react-components';
import { ArgusMark } from './ArgusMark';

/** Clicks this far apart or closer count as one run; a slower click starts counting again. */
const MAX_GAP_MS = 500;

/** How many in a row it takes. */
const CLICKS = 10;

/** How long the bubble stays before it fades out on its own. */
const VISIBLE_MS = 2600;

const useStyles = makeStyles({
  // The link keeps the header's layout; the wrapper only gives the bubble something to be
  // positioned against.
  root: { position: 'relative', display: 'inline-flex' },

  bubble: {
    position: 'absolute',
    top: 'calc(100% + 8px)',
    left: 0,
    zIndex: 20,
    padding: '6px 10px',
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow8,
    fontSize: '22px',
    lineHeight: 1,
    // Decoration: it must never sit between the user and the header behind it.
    pointerEvents: 'none',
    userSelect: 'none',
    animationName: {
      from: { opacity: 0, transform: 'translateY(-4px) scale(0.9)' },
      to: { opacity: 1, transform: 'translateY(0) scale(1)' },
    },
    animationDuration: tokens.durationNormal,
    animationTimingFunction: tokens.curveDecelerateMid,
    '@media (prefers-reduced-motion: reduce)': { animationName: 'none' },
  },

  // The little tail, drawn as a rotated square tucked under the bubble's top edge.
  tail: {
    position: 'absolute',
    top: '-4px',
    left: '14px',
    width: '7px',
    height: '7px',
    transform: 'rotate(45deg)',
    borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
});

/**
 * The header's mark and wordmark, linking home — and an easter egg: ten quick clicks on it pop a
 * bubble with a crab in it.
 *
 * The link still navigates on every one of those clicks, which is why this is safe to hang off
 * it: the target is the installations grid, so a run of clicks that does not reach ten has done
 * nothing but go home, exactly as clicking the logo always does.
 */
export function BrandLink({ className, markClassName }: { className?: string; markClassName?: string }) {
  const styles = useStyles();

  const [showCrab, setShowCrab] = useState(false);

  // Refs, not state: the count and the timestamp are read and written inside one click handler and
  // nothing renders from them, so keeping them in state would re-render the header ten times.
  const count = useRef(0);
  const lastClick = useRef(0);
  const hideTimer = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(hideTimer.current), []);

  function handleClick() {
    const now = Date.now();

    count.current = now - lastClick.current <= MAX_GAP_MS ? count.current + 1 : 1;
    lastClick.current = now;

    if (count.current < CLICKS) {
      return;
    }

    count.current = 0;
    setShowCrab(true);

    window.clearTimeout(hideTimer.current);
    hideTimer.current = window.setTimeout(() => setShowCrab(false), VISIBLE_MS);
  }

  return (
    <div className={styles.root}>
      <RouterLink to="/installations" className={className} onClick={handleClick}>
        <ArgusMark size={24} className={markClassName} />
        Argus
      </RouterLink>

      {showCrab && (
        <div className={styles.bubble} aria-hidden="true">
          <span className={styles.tail} />
          🦀
        </div>
      )}
    </div>
  );
}
