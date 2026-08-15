import { useLayoutEffect, useRef, useState } from 'react';
import { makeStyles, tokens } from '@fluentui/react-components';
import { BeamsBackground } from '../components/ui/beams-background';
import { ArgusMark } from '../components/ArgusMark';

/**
 * Where the name comes from. The one screen in Argus that holds no data.
 *
 * It is one screenful and never scrolls: the text is nine short sentences, and a page that makes
 * someone scroll for nine sentences has been laid out as a document when it is a plaque. That
 * constraint does the design work here — the name and its four notes are placed in the window,
 * not stacked down it, and everything that did not earn its place is gone.
 *
 * Czech, like the story it retells. The grids stay in English because their words are the
 * model's — Machine, Stage, DNS endpoint — and those are the words used at work in either
 * language.
 */
const useStyles = makeStyles({
  /**
   * Full-bleed and exactly one screen.
   *
   * `100vw` alone would only make it wide, not aligned — it would still start at the content's
   * left edge and hang off the right. The negative margin pulls it back by the distance from the
   * content edge to the window edge, which is what `50% - 50vw` measures whatever `main`'s
   * padding and its 2200px cap happen to be. `svh` rather than `vh`: on a phone `vh` counts the
   * strip behind the browser's own toolbar, so the page would be a bar too tall and scroll by
   * exactly that much.
   */
  bleed: {
    width: '100vw',
    marginLeft: 'calc(50% - 50vw)',
    marginTop: '-24px',
    marginBottom: '-24px',
    // Height comes from the measurement below; this is what it falls back to for the first paint.
    height: 'calc(100svh - 69px)',
    boxSizing: 'border-box',
    // The promise of no scrolling, kept even on a window too short for the type below.
    overflow: 'hidden',
  },

  page: {
    boxSizing: 'border-box',
    width: '100%',
    maxWidth: '980px',
    // Centred in what is left of the window by its own margins. The deeper bottom padding sits
    // the text above the geometric middle — the eye reads the optical centre as a little higher,
    // and the beams travel upwards, so the deeper margin belongs below.
    margin: 'auto',
    padding: 'clamp(24px, 4vh, 56px) clamp(24px, 5vw, 56px) clamp(56px, 13vh, 150px)',
    display: 'flex',
    flexDirection: 'column',
    rowGap: 'clamp(28px, 5vh, 56px)',
  },

  // Mark and name on one line, reading left to right as a signature does. The rule
  // under it closes the masthead off from the notes — hairline and dim, so it divides without
  // becoming a third thing to look at beside the name.
  masthead: {
    display: 'flex',
    alignItems: 'baseline',
    // On the page's axis, like the closing line at the other end of it. The mark and the name
    // stay one unit — centred together, not each on its own row.
    justifyContent: 'center',
    gap: 'clamp(14px, 2vw, 22px)',
    paddingBottom: 'clamp(18px, 2.5vh, 28px)',
    borderBottom: '1px solid rgba(255, 255, 255, 0.22)',
  },
  markBox: { alignSelf: 'center', display: 'flex', color: 'hsl(200, 85%, 72%)' },
  /**
   * The name, lettered rather than set: wide tracking, one weight, no colour of its own.
   * `textIndent` repeats the tracking because letter-spacing is added after the last letter too.
   */
  title: {
    margin: 0,
    fontSize: 'clamp(30px, 4.4vw, 52px)',
    lineHeight: 1,
    fontWeight: tokens.fontWeightSemibold,
    letterSpacing: '0.2em',
    // Tracking is added after the last letter too, so the word carries a trailing gap its left
    // side does not have. Pulled back off the right rather than indented from the left: an indent
    // shifts the whole word instead of cancelling the gap, which on a centred line pushes it
    // further off the axis rather than onto it.
    marginRight: '-0.2em',
  },
  /**
   * The four notes side by side rather than one after another.
   *
   * Each is two lines long. Down a column they read as a list of headings with the text as an
   * afterthought, and they need the height the page does not have; in two columns the eye takes
   * a pair at a time and the whole thing is one glance. `auto-fit` collapses that to one column
   * when the window is too narrow to hold two, with no breakpoint to maintain.
   */
  notes: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
    columnGap: 'clamp(28px, 4vw, 64px)',
    rowGap: 'clamp(22px, 3.5vh, 40px)',
  },
  note: { display: 'flex', flexDirection: 'column', rowGap: '8px' },
  /**
   * A label on the note, not a smaller title. Four headings at heading size would each argue with
   * the name above them; at label size they are the structure and nothing more.
   */
  heading: {
    margin: 0,
    fontSize: '12px',
    fontWeight: tokens.fontWeightRegular,
    letterSpacing: '0.16em',
    textTransform: 'uppercase',
    color: 'rgba(255, 255, 255, 0.42)',
  },
  body: {
    margin: 0,
    fontSize: 'clamp(15px, 1.05vw, 17px)',
    // Looser than the app's default: light text on a dark, moving ground closes up otherwise.
    lineHeight: 1.7,
    color: 'rgba(255, 255, 255, 0.78)',
  },

  // The one line the page exists to leave behind, so it is the only thing set in full white and
  // the only thing given a rule to itself.
  closing: {
    margin: 0,
    paddingTop: 'clamp(20px, 3vh, 32px)',
    // The same hairline as the one under the masthead, in every respect: a pair of rules framing
    // the notes has to read as a pair, and two different greys — or two different lengths — read
    // as two unrelated marks.
    borderTop: '1px solid rgba(255, 255, 255, 0.22)',
    // One line, always. It is the page's last word and a wrap turns it into a small paragraph —
    // so the size is tied to the window's width instead of the `ch` measure the notes use, and
    // the line is told not to break. `min-content` sizing keeps that from widening the flex
    // column it sits in.
    fontSize: 'clamp(10px, 2.1vw, 20px)',
    lineHeight: 1.6,
    color: '#fff',
    whiteSpace: 'nowrap',
    minWidth: 0,
    // Centred on the page, unlike everything above it: the notes are set flush left because they
    // are read in columns, this one line is read as a closing statement and sits on the axis of
    // the page. Centred with `textAlign`, never with auto side margins — those shrink a flex item
    // to its content, which took the rule above with them and left it as long as the sentence.
    textAlign: 'center',
  },
  strong: { fontWeight: tokens.fontWeightSemibold },

  // The signature, on the bottom edge of the screen. Small and dim on purpose: it belongs to the
  // page but is not part of what the page says, and anything louder would take attention from the
  // closing line.
  credit: {
    margin: 0,
    padding: '0 24px clamp(14px, 2.5vh, 26px)',
    fontSize: '11px',
    letterSpacing: '0.1em',
    textAlign: 'center',
    color: 'rgba(255, 255, 255, 0.3)',
  },
});

const NOTES = [
  {
    heading: 'Kdo to byl',
    body: 'Obr se stovkou očí po celém těle. Nikdy nespal celý — část očí vždycky hlídala. Přezdívalo se mu Panoptés, „Všudevidoucí“.',
  },
  {
    heading: 'Strážce Héry',
    body: 'Bohyně Héra si ho vybrala jako svého hlídače. Trpělivý, neúnavný, nepodplatitelný — nic mu neuniklo.',
  },
  {
    heading: 'Odkaz',
    body: 'Héra jeho sto očí přenesla na ocas páva, kde jsou dodnes. Symbol věčné bdělosti.',
  },
  {
    heading: 'Jak skončil',
    body: 'Jeho příběh uzavřel bůh Hermés — uspal ho vyprávěním a hudbou. I dokonalý strážce nakonec podlehl.',
  },
];

export function AboutPage() {
  const styles = useStyles();

  /**
   * The exact height left under the header, measured rather than assumed.
   *
   * The shell's header is sticky and its height depends on the tabs, the font and whether the row
   * has wrapped — subtracting a guessed constant is wrong by a few pixels, and a few pixels is
   * the difference between one screen and a scrollbar. Measuring the gap from the top of this
   * element to the bottom of the window costs one layout pass and is right whatever the header
   * turns out to be.
   */
  const stageRef = useRef<HTMLDivElement>(null);
  const [height, setHeight] = useState<number | null>(null);

  useLayoutEffect(() => {
    const stage = stageRef.current;
    if (!stage) return;

    const measure = () => setHeight(window.innerHeight - stage.getBoundingClientRect().top);

    measure();
    window.addEventListener('resize', measure);

    return () => window.removeEventListener('resize', measure);
  }, []);

  return (
    <BeamsBackground
      ref={stageRef}
      className={styles.bleed}
      intensity="medium"
      style={height === null ? undefined : { height: `${height}px` }}
    >
      <div className={styles.page}>
        <header className={styles.masthead}>
          <span className={styles.markBox}>
            <ArgusMark size={40} />
          </span>
          <h1 className={styles.title}>ARGUS</h1>
        </header>

        <div className={styles.notes}>
          {NOTES.map((note) => (
            <section key={note.heading} className={styles.note}>
              <h2 className={styles.heading}>{note.heading}</h2>
              <p className={styles.body}>{note.body}</p>
            </section>
          ))}
        </div>

        <p className={styles.closing}>
          <span className={styles.strong}>Argus</span> nese jeho jméno záměrně. Vidí všechno, co se
          děje pod povrchem.
        </p>
      </div>

      {/* Outside the page block, on the foot of the backdrop itself — a colophon, which belongs to
          the sheet rather than to what is written on it. */}
      <p className={styles.credit}>Made by CrunchyCrabby 2026</p>
    </BeamsBackground>
  );
}
