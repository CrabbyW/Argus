/**
 * The Argus mark, inline.
 *
 * `brand/README.md` says the in-UI variant is the monochrome one (`argus-mark-mono.svg`),
 * drawn in `currentColor`: the header sits on light and dark backgrounds and the coloured
 * mark would be wrong on one of them. Inlined rather than loaded as an `<img>` so it can
 * inherit that colour at all — an external SVG cannot see the page's text colour.
 *
 * The geometry is `brand/argus-mark-mono.svg` unchanged. The rules there forbid altering
 * proportions, so this takes a single `size` and scales the whole 64-unit viewBox.
 */
export function ArgusMark({ size = 24, className }: { size?: number; className?: string }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 64 64"
      width={size}
      height={size}
      className={className}
      role="img"
      aria-label="Argus"
      focusable="false"
    >
      <g fill="currentColor">
        <rect x="26" y="6" width="12" height="12" rx="3.5" />
        <rect x="17" y="21" width="12" height="12" rx="3.5" opacity=".8" />
        <rect x="35" y="21" width="12" height="12" rx="3.5" opacity=".8" />
        <rect x="8" y="36" width="12" height="12" rx="3.5" opacity=".55" />
        <rect x="26" y="36" width="12" height="12" rx="3.5" />
        <rect x="44" y="36" width="12" height="12" rx="3.5" opacity=".55" />
        <rect x="8" y="51" width="12" height="7" rx="3" opacity=".3" />
        <rect x="44" y="51" width="12" height="7" rx="3" opacity=".3" />
      </g>
    </svg>
  );
}
