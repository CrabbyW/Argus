/**
 * The sign-in screen's look: an animated dot grid rendered on a WebGL canvas, a vignette over
 * it, and a dark card carrying the form.
 *
 * Presentational only. It owns the two field values and nothing else — the caller supplies
 * `onSubmit` and reports back through `error` and `isSubmitting`, so the auth logic stays in
 * `auth/AuthContext` where the rest of the app can see it.
 *
 * Deliberately dark whatever the app theme says. This is the one full-bleed screen in Argus and
 * the design is built on the dot grid glowing out of black; a light variant would be a different
 * design, not a tinted one. The colours here are therefore literal, not `tokens.*` — everything
 * behind the sign-in boundary uses the theme normally.
 */

import { useEffect, useId, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { makeStyles, tokens } from '@fluentui/react-components';
import { ArgusMark } from '../ArgusMark';

/* ────────────────────────── the dot grid ────────────────────────── */

/**
 * Drawn with the WebGL2 API directly rather than through Three.js.
 *
 * The scene is one full-screen quad and one fragment shader, which is the whole of what a
 * renderer would be doing here — and the alternative was the shape the design arrived in: a
 * `<script>` tag injecting Three r128 from a public CDN at runtime. That would put a
 * third-party script with no integrity hash on the one screen that handles credentials, and
 * break sign-in entirely on a machine that cannot reach the CDN. Argus is an internal tool
 * that has to work on an isolated network, so neither is acceptable.
 *
 * The shader below is the original unchanged, minus one uniform it declared but never read.
 */

const VERTEX_SHADER = `#version 300 es
precision mediump float;

in vec2 a_position;

uniform vec2 u_resolution;

out vec2 fragCoord;

void main() {
  gl_Position = vec4(a_position, 0.0, 1.0);
  fragCoord = (a_position + 1.0) * 0.5 * u_resolution;
  fragCoord.y = u_resolution.y - fragCoord.y;
}
`;

const FRAGMENT_SHADER = `#version 300 es
precision mediump float;

in vec2 fragCoord;

uniform float u_time;
uniform float u_opacities[10];
uniform vec3 u_colors[6];
uniform float u_total_size;
uniform float u_dot_size;
uniform vec2 u_resolution;

out vec4 fragColor;

float PHI = 1.61803398874989484820459;

float random(vec2 xy) {
    return fract(tan(distance(xy * PHI, xy) * 0.5) * xy.x);
}

void main() {
    vec2 st = fragCoord.xy;
    st.x -= abs(floor((mod(u_resolution.x, u_total_size) - u_dot_size) * 0.5));
    st.y -= abs(floor((mod(u_resolution.y, u_total_size) - u_dot_size) * 0.5));

    float opacity = step(0.0, st.x) * step(0.0, st.y);

    vec2 st2 = vec2(int(st.x / u_total_size), int(st.y / u_total_size));

    float frequency = 5.0;
    float show_offset = random(st2);
    float rand = random(st2 * floor((u_time / frequency) + show_offset + frequency));
    opacity *= u_opacities[int(rand * 10.0)];
    opacity *= 1.0 - step(u_dot_size / u_total_size, fract(st.x / u_total_size));
    opacity *= 1.0 - step(u_dot_size / u_total_size, fract(st.y / u_total_size));

    vec3 color = u_colors[int(show_offset * 6.0)];

    float animation_speed_factor = 3.0;
    vec2 center_grid = u_resolution / 2.0 / u_total_size;
    float dist_from_center = distance(center_grid, st2);

    float timing_offset_intro = dist_from_center * 0.01 + (random(st2) * 0.15);

    opacity *= step(timing_offset_intro, u_time * animation_speed_factor);
    opacity *= clamp((1.0 - step(timing_offset_intro + 0.1, u_time * animation_speed_factor)) * 1.25, 1.0, 1.25);

    fragColor = vec4(color, opacity);
    fragColor.rgb *= fragColor.a;
}
`;

/** The ten brightness steps a dot picks from, weighted towards the dim end. */
const OPACITIES = new Float32Array([0.3, 0.3, 0.3, 0.5, 0.5, 0.5, 0.8, 0.8, 0.8, 1.0]);

/** Six white entries: the shader indexes a palette, and this design's palette is one colour. */
const COLORS = new Float32Array([1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]);

const GRID_SPACING = 20.0;
const DOT_SIZE = 6.0;

/**
 * Where the intro sweep has finished and every dot is on screen. Used as the timestamp of the
 * single frame drawn when animation is unwelcome.
 */
const SETTLED_TIME = 2.0;

function compileShader(gl: WebGL2RenderingContext, type: number, source: string): WebGLShader | null {
  const shader = gl.createShader(type);
  if (!shader) return null;

  gl.shaderSource(shader, source);
  gl.compileShader(shader);

  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    console.error('Dot grid shader failed to compile:', gl.getShaderInfoLog(shader));
    gl.deleteShader(shader);
    return null;
  }

  return shader;
}

/**
 * Every failure path here ends in "draw nothing". The background is decoration, so a machine
 * without WebGL2, or with a driver that rejects the shader, gets the plain black backdrop and a
 * sign-in form that works exactly as well.
 */
function DotGrid({ className, animate }: { className?: string; animate: boolean }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const gl = canvas.getContext('webgl2', {
      alpha: true,
      antialias: false,
      depth: false,
      stencil: false,
    });
    if (!gl) return;

    const vertexShader = compileShader(gl, gl.VERTEX_SHADER, VERTEX_SHADER);
    const fragmentShader = compileShader(gl, gl.FRAGMENT_SHADER, FRAGMENT_SHADER);
    const program = gl.createProgram();

    if (!vertexShader || !fragmentShader || !program) return;

    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      console.error('Dot grid program failed to link:', gl.getProgramInfoLog(program));
      return;
    }

    gl.useProgram(program);

    // Two triangles covering clip space, so the fragment shader runs once per pixel.
    const vao = gl.createVertexArray();
    gl.bindVertexArray(vao);

    const buffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);

    const positionLocation = gl.getAttribLocation(program, 'a_position');
    gl.enableVertexAttribArray(positionLocation);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

    // Uniform state belongs to the program, so the constants are sent once.
    gl.uniform1fv(gl.getUniformLocation(program, 'u_opacities'), OPACITIES);
    gl.uniform3fv(gl.getUniformLocation(program, 'u_colors'), COLORS);
    gl.uniform1f(gl.getUniformLocation(program, 'u_total_size'), GRID_SPACING);
    gl.uniform1f(gl.getUniformLocation(program, 'u_dot_size'), DOT_SIZE);

    const timeLocation = gl.getUniformLocation(program, 'u_time');
    const resolutionLocation = gl.getUniformLocation(program, 'u_resolution');

    // Additive over transparent black: the dots light the page up rather than painting over it.
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE);
    gl.clearColor(0, 0, 0, 0);

    const draw = (seconds: number) => {
      gl.clear(gl.COLOR_BUFFER_BIT);
      gl.uniform1f(timeLocation, seconds);
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    };

    const resize = () => {
      const width = canvas.clientWidth || window.innerWidth;
      const height = canvas.clientHeight || window.innerHeight;

      // Capped at 2: past that the grid gains no visible detail and a phone pays for it in
      // fragments. The grid coordinate space stays at twice the CSS size whatever the device
      // ratio is, which is what fixes the dots at their designed spacing on screen.
      const ratio = Math.min(window.devicePixelRatio || 1, 2);

      canvas.width = Math.max(1, Math.round(width * ratio));
      canvas.height = Math.max(1, Math.round(height * ratio));

      gl.viewport(0, 0, canvas.width, canvas.height);
      gl.uniform2f(resolutionLocation, width * 2, height * 2);

      // The animated path redraws on the next frame anyway; the still one has to be told.
      if (!animate) draw(SETTLED_TIME);
    };

    resize();

    const observer = new ResizeObserver(resize);
    observer.observe(canvas);

    let frameId = 0;

    if (animate) {
      const startedAt = performance.now();

      const renderFrame = () => {
        frameId = requestAnimationFrame(renderFrame);
        draw((performance.now() - startedAt) / 1000);
      };

      renderFrame();
    }

    return () => {
      cancelAnimationFrame(frameId);
      observer.disconnect();

      gl.deleteProgram(program);
      gl.deleteShader(vertexShader);
      gl.deleteShader(fragmentShader);
      gl.deleteBuffer(buffer);
      gl.deleteVertexArray(vao);
      // Note: no WEBGL_lose_context here. StrictMode remounts this effect onto the same canvas,
      // and a canvas whose context has been lost hands the lost one straight back.
    };
  }, [animate]);

  return <canvas ref={canvasRef} className={className} aria-hidden="true" />;
}

/* ────────────────────────── the form ────────────────────────── */

const useStyles = makeStyles({
  page: {
    position: 'relative',
    width: '100%',
    minHeight: '100vh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '16px',
    // No global box-sizing reset in `index.css`, so the padding would otherwise be added to the
    // 100% width and push the layout past the viewport.
    boxSizing: 'border-box',
    overflow: 'hidden',
    backgroundColor: '#000',
    color: '#fff',
    fontFamily: tokens.fontFamilyBase,
  },
  // Offsets written out rather than `inset: 0` — Griffel passes `inset` through untouched and
  // the atomic class never lands.
  //
  // The explicit 100%/100% is not redundant with them. A canvas is a replaced element, so with
  // `width: auto` the four zero offsets do not stretch it: it takes its intrinsic size, which is
  // the drawing buffer this effect is itself setting. That is a loop — the observer resizes the
  // buffer, the buffer resizes the layout box, the observer fires again — and it grew the canvas
  // on every pass until the browser cut the observer off.
  canvas: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    display: 'block',
    zIndex: 0,
  },
  vignette: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    zIndex: 1,
    background: 'radial-gradient(circle at center, rgba(0,0,0,0.75) 0%, rgba(0,0,0,0) 100%)',
    pointerEvents: 'none',
  },
  card: {
    position: 'relative',
    zIndex: 2,
    width: '100%',
    maxWidth: '400px',
    padding: '32px',
    boxSizing: 'border-box',
    borderRadius: '12px',
    border: '1px solid #222',
    backgroundColor: '#121212',
    boxShadow: '0 10px 40px rgba(0,0,0,0.8)',
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    textAlign: 'center',
  },
  mark: {
    width: '51px',
    height: '51px',
    marginBottom: '12px',
    borderRadius: '50%',
    border: '1px solid #333',
    backgroundColor: '#111',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    // The brand blue the mark carried before this screen — the mark's own per-square opacities
    // turn the one colour back into the shades it used to have.
    color: tokens.colorBrandForeground1,
    flexShrink: 0,
  },
  title: {
    margin: 0,
    fontSize: '1.35rem',
    fontWeight: 600,
    letterSpacing: '-0.025em',
  },
  subtitle: {
    margin: '4px 0 0',
    fontSize: '0.85rem',
    lineHeight: 1.5,
    color: '#888',
  },
  form: {
    width: '100%',
    marginTop: '20px',
    display: 'flex',
    flexDirection: 'column',
    rowGap: '10px',
  },
  // Every control in the card — both inputs, the button, the error box — is laid out on the same
  // box: `border-box` sizing, full width, the same 1px border and the same horizontal padding.
  // Without that they each resolve to a different rendered width (padding and border are added
  // outside `width: 100%` when nothing resets `box-sizing`, and the file has no global reset), so
  // their left and right edges, and the borders drawn on them, do not line up.
  input: {
    width: '100%',
    boxSizing: 'border-box',
    padding: '0.65rem 0.85rem',
    borderRadius: '6px',
    border: '1px solid #333',
    backgroundColor: '#000',
    color: '#fff',
    fontSize: '0.875rem',
    fontFamily: 'inherit',
    outlineStyle: 'none',
    '::placeholder': { color: '#666' },
    // State changes move the colour only — the 1px solid is repeated verbatim (Griffel's types
    // take `border` but not a bare `borderColor`), so nothing nudges the field's geometry as it
    // is hovered or focused.
    ':hover': { border: '1px solid #444' },
    // The design's inputs drop the focus ring; something has to replace it, or the form is
    // unusable from the keyboard. This is that, in the design's own palette.
    ':focus': {
      border: '1px solid #777',
      boxShadow: '0 0 0 3px rgba(255,255,255,0.08)',
    },
    ':disabled': { color: '#777', cursor: 'not-allowed' },
  },
  submit: {
    width: '100%',
    boxSizing: 'border-box',
    marginTop: '4px',
    padding: '0.65rem 0.85rem',
    borderRadius: '6px',
    // Transparent rather than `none`, so the button is exactly as wide and as tall as the fields
    // above it instead of 2px short in each direction.
    border: '1px solid transparent',
    backgroundColor: '#ededed',
    color: '#000',
    fontWeight: 500,
    fontSize: '0.875rem',
    fontFamily: 'inherit',
    cursor: 'pointer',
    ':hover': { backgroundColor: '#fff' },
    ':focus-visible': {
      outline: '2px solid #fff',
      outlineOffset: '2px',
    },
    ':disabled': {
      backgroundColor: '#2a2a2a',
      color: '#777',
      cursor: 'not-allowed',
    },
  },
  error: {
    width: '100%',
    boxSizing: 'border-box',
    marginTop: '16px',
    padding: '0.6rem 0.85rem',
    borderRadius: '6px',
    border: '1px solid rgba(248,81,73,0.4)',
    backgroundColor: 'rgba(248,81,73,0.12)',
    color: '#ff9d96',
    fontSize: '0.8rem',
    lineHeight: 1.45,
    textAlign: 'left',
  },
  srOnly: {
    position: 'absolute',
    width: '1px',
    height: '1px',
    padding: 0,
    margin: '-1px',
    overflow: 'hidden',
    clip: 'rect(0, 0, 0, 0)',
    whiteSpace: 'nowrap',
  },
});

export interface ModernLoginProps {
  /** Rejections are the caller's to catch and surface through `error`. */
  onSubmit: (username: string, password: string) => void | Promise<void>;
  error?: string | null;
  isSubmitting?: boolean;
  title?: string;
  subtitle?: string;
}

export function ModernLogin({
  onSubmit,
  error = null,
  isSubmitting = false,
  title = 'Sign in to Argus',
  subtitle = 'Installation inventory.',
}: ModernLoginProps) {
  const styles = useStyles();
  const fieldId = useId();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  // A grid of dots flickering at random is the kind of movement the setting exists for, so the
  // background is rendered once and left still rather than dropped.
  const [animate, setAnimate] = useState(
    () => !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
  );

  useEffect(() => {
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    const onChange = (event: MediaQueryListEvent) => setAnimate(!event.matches);

    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, []);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    void onSubmit(username, password);
  }

  return (
    <div className={styles.page}>
      <DotGrid className={styles.canvas} animate={animate} />
      <div className={styles.vignette} />

      <div className={styles.card}>
        <div className={styles.mark}>
          <ArgusMark size={28} />
        </div>

        <h1 className={styles.title}>{title}</h1>
        <p className={styles.subtitle}>{subtitle}</p>

        <form onSubmit={handleSubmit} className={styles.form}>
          {/* Labelled, but the design shows placeholders only — so the labels are for
              screen readers alone rather than absent. */}
          <label className={styles.srOnly} htmlFor={`${fieldId}-username`}>
            Username
          </label>
          <input
            id={`${fieldId}-username`}
            className={styles.input}
            type="text"
            placeholder="Username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            autoComplete="username"
            disabled={isSubmitting}
            autoFocus
            required
          />

          <label className={styles.srOnly} htmlFor={`${fieldId}-password`}>
            Password
          </label>
          <input
            id={`${fieldId}-password`}
            className={styles.input}
            type="password"
            placeholder="Password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            disabled={isSubmitting}
            required
          />

          <button
            type="submit"
            className={styles.submit}
            disabled={isSubmitting || !username || !password}
          >
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>

        {error && (
          <div className={styles.error} role="alert">
            {error}
          </div>
        )}
      </div>
    </div>
  );
}
