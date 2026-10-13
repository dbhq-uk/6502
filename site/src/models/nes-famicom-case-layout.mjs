// The NES's case for its outside 3D model, and the words the page says about
// it: its caption and its accessible name for each console, and what a click
// on its buttons does.
//
// The figures are measured, not typed: tools/nes-model/case_measure.py measured
// the design patent's drawings and photographs of an NTSC NES-001 and a PAL
// NESE-001, and wrote ./nes-famicom-case-parts.mjs, which this module hands on
// to the model and the page. The journal for 5 and 6 October 2026 has every
// figure. The size is the published one, which is not Nintendo's.
//
// One case for both consoles: the PAL console's differs in its words (a third
// line on its front, its rear in German, two labels underneath), which the
// parts module gives by region. Millimetres in the case frame: from the case's
// left rear corner at table level, x to the right as seen from the front, y
// towards the front, z up. A browser module: no Node imports, because the
// model's bundle includes it.

import * as P from './nes-famicom-case-parts.mjs';

export const { CASE, PROFILE, DOOR, VENTS, BUTTONS, LED, PORTS, REAR, LABELS, UNDERSIDE, FEET } = P;

/** The consoles the model draws, the first shown until the page names another. */
export const REGIONS = ['ntsc', 'pal'];

/** Each console, as the caption names it. */
export const CONSOLES = {
  ntsc: { name: 'an NTSC NES-001', sold: 'North America', short: 'NTSC' },
  pal: { name: 'a PAL NESE-001', sold: 'Europe', short: 'PAL' },
};

/** How long RESET stays down when the machine is reset, in milliseconds. */
export const PRESS_MS = 140;

/** The model's accessible name, for each console. */
export const LABELS_FOR = {
  ntsc: '3D model of the outside of an NTSC NES-001. Click POWER on it to start the machine, and RESET to reset it.',
  pal: '3D model of the outside of a PAL NESE-001. Click POWER on it to start the machine, and RESET to reset it.',
};

/** What the model's buttons do, said under it. */
export const HELP = 'POWER on the model does what the page\'s Start does, and RESET on it resets the machine, as the page\'s Reset does. The page has no power-off, so once the machine runs, POWER stays in. The power light is lit while the machine runs. The lockout chip is not emulated, so the light never blinks, as a real console\'s does when it refuses a cartridge.';

/** The words on the case that are its own and not the other console's, for the caption. */
const own = (region) => {
  const other = REGIONS.find((r) => r !== region);
  return LABELS[region].filter((l) => !LABELS[other].some((o) => o.words === l.words && o.face === l.face));
};
const cm = (mm) => (mm / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** The model's text alternative and caption for one console, with every size read from the parts module. */
export function describe(region) {
  const c = CONSOLES[region];
  if (!c) throw new Error(`the case model draws ${REGIONS.join(' and ')}, not ${region}`);
  const words = own(region).filter((l) => l.face !== 'bottom').map((l) => l.words);
  const stickers = own(region).filter((l) => l.face === 'bottom').length;
  const rear = REAR[region].filter((r) => r.face === 'rear').map((r) => r.label);
  const side = REAR[region].filter((r) => r.face !== 'rear').map((r) => r.label);
  const list = (xs) => (xs.length < 2 ? xs.join('') : `${xs.slice(0, -1).join(', ')} and ${xs.at(-1)}`);
  const pal = region === 'pal'
    ? ` It is the NTSC console's case with the PAL console's own words, read from photographs of a PAL console: ${list(words)}, and ${stickers === 2 ? 'two' : stickers} labels underneath. The PAL console photographed has yellowed; the model draws it as made, in the same greys.`
    : '';
  return `Model, not a photograph, of ${c.name}, the front-loading console sold in ${c.sold}: its case drawn in three.js, ${cm(CASE.width)} by ${cm(CASE.depth)} cm and ${cm(CASE.height)} cm high, the published size, which is not Nintendo's own. On it are the cartridge door, the vents, the POWER and RESET buttons, the power light, the two controller ports, the connectors on the rear (${list(rear)}) and on the right side (${list(side)}), the underside with its expansion cover, and the feet. Its words are drawn in this site's own type, not in Nintendo's lettering. The places were measured from photographs and the design patent's drawings; the note below says how well each is known.${pal} While the machine above runs, the power light is lit and POWER is in.`;
}

/** What the model's status line says, about the machine and its buttons. */
export const STATUS = {
  idle: 'The model is running. The machine above is not running: click POWER on the model, or press Start above, to start it.',
  running: 'The model is running, and so is the machine: the power light is lit and POWER is in.',
  starting: 'POWER pressed Start above: the NES is downloading, and starts once it has.',
  loading: 'The NES is already starting: it runs once its download is done.',
  cannot: 'The NES cannot start on this page: the line under its screen says why.',
  noPowerOff: 'The machine is already on, and this page has no power-off: POWER stays in. The page\'s Power off and on button starts the game again.',
  notRunning: 'The machine is not running, so RESET did nothing: click POWER, or press Start above, first.',
  reset: 'RESET reset the machine, as the page\'s Reset does: the game started again.',
};

/**
 * A click on one of the model's buttons, on the page whose machine panel is
 * `panel` (public/nes.js's [data-nes], with panel.nes). POWER presses the
 * page's own Start button, so it does exactly what Start does, while Start can
 * be pressed; once the machine runs it does nothing, as the page has no
 * power-off. RESET calls panel.nes.reset() while the machine runs, and before
 * that does nothing to any machine. Returns what the status line says.
 */
export function press(name, panel) {
  const nes = panel?.nes;
  const running = nes?.running?.() === true;
  if (name === 'POWER') {
    if (running) return STATUS.noPowerOff;
    const start = panel?.querySelector('[data-nes-start]');
    if (start && !start.disabled && !start.hidden) {
      start.click();
      return STATUS.starting;
    }
    return panel?.dataset?.state === 'loading' ? STATUS.loading : STATUS.cannot;
  }
  if (name === 'RESET') {
    if (!running) return STATUS.notRunning;
    nes.reset();
    return STATUS.reset;
  }
  throw new Error(`the case has no button ${name}`);
}
