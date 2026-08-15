/**
 * A DNS endpoint is stored as a host name and nothing else — `paha.ga.local`, not
 * `https://paha.ga.local/api/`. The two are the same endpoint, and letting both into the lookup
 * means the same machine appears twice in every dropdown built from it, with only one of the two
 * matching anything a colleague filters by.
 *
 * The value gets pasted out of a browser's address bar more often than it gets typed, so a URL is
 * normalized to its host rather than rejected: scheme, credentials, port, path, query and
 * fragment are all dropped, and what is left is lower-cased — DNS is case-insensitive, so a
 * capital letter would otherwise create a second row for a host that already exists.
 *
 * Deliberately not `new URL()`: the common input here has no scheme, which URL rejects outright,
 * and prefixing one to get around that turns a typo into a plausible-looking host.
 */
export function toDnsName(value: string): string {
  let rest = value.trim();

  if (!rest) {
    return '';
  }

  rest = rest.replace(/^[a-z][a-z0-9+.-]*:\/\//i, '');
  // Anything before an `@` is a user-info section, which is not part of the host.
  rest = rest.replace(/^[^/@]*@/, '');
  rest = rest.split(/[/?#]/)[0];
  rest = rest.replace(/:\d+$/, '');
  // A fully-qualified name may be written with the root's trailing dot; the lookup stores it
  // without, so `host.local.` and `host.local` do not become two rows.
  rest = rest.replace(/\.+$/, '');

  return rest.toLowerCase();
}
