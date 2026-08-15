/**
 * Drifting light beams on a canvas, behind whatever is passed as children.
 *
 * Ported from a Tailwind + shadcn + `motion` component, because Argus is none of those: the
 * styling is Griffel (`makeStyles`) like the rest of the app, and the one animation the original
 * used `motion` for — an overlay breathing between two opacities — is four lines of CSS
 * keyframes. A React animation library, Tailwind and a `cn` helper would all have arrived for
 * that single fade.
 *
 * The drawing is the original's, with its sizing corrected. It called `ctx.scale(dpr, dpr)` on
 * every resize, which multiplies rather than replaces — two resizes on a 2× display and the
 * beams are drawn at four times the size — and it seeded the beams from `canvas.width`, the
 * device-pixel buffer, while drawing in CSS pixels. Here the transform is set outright and every
 * measurement is in CSS pixels.
 */

import { useEffect, useRef } from 'react';
import type { CSSProperties, ReactNode, Ref } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';

interface Beam {
  x: number;
  y: number;
  width: number;
  length: number;
  angle: number;
  speed: number;
  opacity: number;
  hue: number;
  pulse: number;
  pulseSpeed: number;
}

/** How strongly the beams read against the background. */
type Intensity = 'subtle' | 'medium' | 'strong';

const OPACITY_BY_INTENSITY: Record<Intensity, number> = {
  subtle: 0.7,
  medium: 0.85,
  strong: 1,
};

/**
 * How many beams a window of this width gets.
 *
 * A fixed count spreads thin on a wide monitor and crowds a narrow one — the original's 30 was
 * chosen against one screen. Tying it to the width keeps the density the same at any size, and
 * the bounds stop a phone from drawing a solid wall or a 4K screen from paying for beams nobody
 * can pick out.
 */
function beamCountFor(width: number) {
  return Math.max(18, Math.min(48, Math.round(width / 60)));
}

function createBeam(width: number, height: number): Beam {
  return {
    x: Math.random() * width * 1.5 - width * 0.25,
    y: Math.random() * height * 1.5 - height * 0.25,
    width: 30 + Math.random() * 60,
    length: height * 2.5,
    // All of them lean the same way, a few degrees apart: the beams are one weather, not a
    // scattering.
    angle: -35 + Math.random() * 10,
    speed: 0.6 + Math.random() * 1.2,
    opacity: 0.12 + Math.random() * 0.16,
    // Cyan through to violet — the brand blue sits inside this range, so the page stays Argus's
    // rather than becoming a generic gradient.
    hue: 190 + Math.random() * 70,
    pulse: Math.random() * Math.PI * 2,
    pulseSpeed: 0.02 + Math.random() * 0.03,
  };
}

/** Sends a beam that has drifted off the top back to the bottom, in one of three columns. */
function resetBeam(beam: Beam, index: number, count: number, width: number, height: number) {
  const spacing = width / 3;
  const column = index % 3;

  beam.y = height + 100;
  beam.x = column * spacing + spacing / 2 + (Math.random() - 0.5) * spacing * 0.5;
  beam.width = 100 + Math.random() * 100;
  beam.speed = 0.5 + Math.random() * 0.4;
  beam.hue = 190 + (index * 70) / count;
  beam.opacity = 0.2 + Math.random() * 0.1;
}

function drawBeam(context: CanvasRenderingContext2D, beam: Beam, intensity: Intensity) {
  context.save();
  context.translate(beam.x, beam.y);
  context.rotate((beam.angle * Math.PI) / 180);

  const pulsingOpacity =
    beam.opacity * (0.8 + Math.sin(beam.pulse) * 0.2) * OPACITY_BY_INTENSITY[intensity];

  // Transparent at both ends, full in the middle: a beam passing through rather than a bar
  // stopping somewhere.
  const gradient = context.createLinearGradient(0, 0, 0, beam.length);

  gradient.addColorStop(0, `hsla(${beam.hue}, 85%, 65%, 0)`);
  gradient.addColorStop(0.1, `hsla(${beam.hue}, 85%, 65%, ${pulsingOpacity * 0.5})`);
  gradient.addColorStop(0.4, `hsla(${beam.hue}, 85%, 65%, ${pulsingOpacity})`);
  gradient.addColorStop(0.6, `hsla(${beam.hue}, 85%, 65%, ${pulsingOpacity})`);
  gradient.addColorStop(0.9, `hsla(${beam.hue}, 85%, 65%, ${pulsingOpacity * 0.5})`);
  gradient.addColorStop(1, `hsla(${beam.hue}, 85%, 65%, 0)`);

  context.fillStyle = gradient;
  context.fillRect(-beam.width / 2, 0, beam.width, beam.length);
  context.restore();
}

const useStyles = makeStyles({
  root: {
    position: 'relative',
    width: '100%',
    minHeight: '100%',
    overflow: 'hidden',
    // Dark whatever the app theme says, as on the sign-in screen: the beams are light, and light
    // is only visible against something dark.
    backgroundColor: '#0a0a0b',
    color: '#fff',
    // No corner radius of its own: this is used as a full-bleed backdrop, and a rounded corner
    // against the edge of the window would look like a card that failed to fit. A caller wanting
    // it inside a card can add one through `className`.
  },
  canvas: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    display: 'block',
    // Softens the beams into light rather than painted stripes. `filter` on the canvas element
    // instead of `ctx.filter`: the context filter is redone per frame on the CPU, this one is
    // composited once.
    filter: 'blur(15px)',
  },
  // The original's `motion.div`, as keyframes. It breathes over ten seconds, which is slow enough
  // that nothing on the page competes with it for attention.
  veil: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    backgroundColor: 'rgba(10, 10, 11, 0.05)',
    backdropFilter: 'blur(30px)',
    animationName: {
      '0%, 100%': { opacity: 0.05 },
      '50%': { opacity: 0.15 },
    },
    animationDuration: '10s',
    animationTimingFunction: 'ease-in-out',
    animationIterationCount: 'infinite',
    // Someone who asked their machine for less movement gets the still version of the same look.
    '@media (prefers-reduced-motion: reduce)': { animationName: 'none', opacity: 0.1 },
  },
  /**
   * The layer the children sit on. Full height and a flex column, so a caller can both centre a
   * block in the backdrop (`margin: auto`) and pin one to its foot — with a box only as tall as
   * its contents, "the bottom of the page" would mean the bottom of the text.
   */
  content: {
    position: 'relative',
    zIndex: 1,
    height: '100%',
    display: 'flex',
    flexDirection: 'column',
  },
});

export function BeamsBackground({
  className,
  intensity = 'strong',
  children,
  ref,
  style,
}: {
  className?: string;
  intensity?: Intensity;
  children?: ReactNode;
  /** The outer element, for a caller that has to measure or size it. */
  ref?: Ref<HTMLDivElement>;
  style?: CSSProperties;
}) {
  const styles = useStyles();
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const context = canvas.getContext('2d');
    if (!context) return;

    let width = 0;
    let height = 0;
    let beams: Beam[] = [];

    const resize = () => {
      const ratio = Math.min(window.devicePixelRatio || 1, 2);

      width = canvas.clientWidth || window.innerWidth;
      height = canvas.clientHeight || window.innerHeight;

      canvas.width = Math.max(1, Math.round(width * ratio));
      canvas.height = Math.max(1, Math.round(height * ratio));

      // Set, not multiplied — see the note at the top of the file.
      context.setTransform(ratio, 0, 0, ratio, 0, 0);

      beams = Array.from({ length: beamCountFor(width) }, () => createBeam(width, height));
    };

    resize();

    const observer = new ResizeObserver(resize);
    observer.observe(canvas);

    // Movement is the whole of this design, so reduced motion gets one still frame rather than
    // no beams at all.
    const stillOnly = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const paint = () => {
      context.clearRect(0, 0, width, height);

      beams.forEach((beam, index) => {
        beam.y -= beam.speed;
        beam.pulse += beam.pulseSpeed;

        if (beam.y + beam.length < -100) {
          resetBeam(beam, index, beams.length, width, height);
        }

        drawBeam(context, beam, intensity);
      });
    };

    let frameId = 0;

    if (stillOnly) {
      paint();
    } else {
      const renderFrame = () => {
        frameId = requestAnimationFrame(renderFrame);
        paint();
      };

      renderFrame();
    }

    return () => {
      cancelAnimationFrame(frameId);
      observer.disconnect();
    };
  }, [intensity]);

  return (
    <div ref={ref} style={style} className={mergeClasses(styles.root, className)}>
      <canvas ref={canvasRef} className={styles.canvas} aria-hidden="true" />
      <div className={styles.veil} aria-hidden="true" />
      <div className={styles.content}>{children}</div>
    </div>
  );
}
