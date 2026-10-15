// The region control a console's page shares: the NTSC and PAL radios, which
// show the region the machine runs in, and a change of them, which the page
// carries out and may refuse. Moved out of nes.js, unchanged in what it does,
// when the Atari 2600 became the second console to need it (the Atari 2600
// plan's task 13). What a change does is each page's own: the NES restarts in
// the other region, the Atari 2600 switches at once with no restart. So is the
// sentence under the control, which says what happened: the page writes it.

/** Shows `region` on the radios: the one whose value it is is checked, the others not. */
export function checkRegion(radios, region) {
  for (const r of radios) r.checked = r.value === region;
}

/**
 * Enables the radios and carries out a change of them: change(value) is called
 * when a radio is chosen whose value is not current(), and returns false to
 * refuse it, which puts the radios back on current().
 */
export function wireRegion(radios, current, change) {
  for (const r of radios) {
    r.disabled = false;
    r.addEventListener('change', () => {
      if (!r.checked || r.value === current()) return;
      if (change(r.value) === false) checkRegion(radios, current());
    });
  }
}
