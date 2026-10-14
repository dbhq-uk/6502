// What the NES's page says that is the NES's own, kept out of the template so
// the tests can read the same values.

/**
 * The parts of the NES the emulation leaves out, each with the issue that
 * tracks it (the design, "What is left out"; issues 61 to 66 and 69, filed on 6
 * October 2026, one a part, titled "NES: <the part>"). In the order the
 * registry's notes give them.
 */
export const NOT_MODELLED = [
  { part: 'the Dendy and the other Famicom clones', issue: 61 },
  { part: 'the Famicom Disk System', issue: 62 },
  { part: 'the Famicom\'s expansion audio', issue: 63 },
  { part: 'the Zapper and other peripherals, the four-player adapters among them', issue: 64 },
  { part: 'the unlicensed mappers, and the licensed ones beyond these six', issue: 65 },
  { part: 'the analogue quirks of the picture, such as NTSC\'s colour fringes', issue: 69 },
  { part: 'saving the battery-backed RAM', issue: 66 },
];

/** The two other NES issues the page links: the speed (Dan's decision) and the 3D models (their own pull request). */
export const ELSEWHERE = { speed: 67, models: 68 };
