// The comparison behind the sortable tables, kept apart from the page code so a
// test can import it. Same origin, no inline script: the CSP allows this file.

/**
 * How two cell values order. A blank value always goes last, in both
 * directions, so sorting by year never puts the machines with no year first.
 * Two numbers compare as numbers; anything else compares as text, the British way.
 */
export function compareCells(x, y, ascending) {
  const blankX = x === "";
  const blankY = y === "";
  if (blankX || blankY) return blankX === blankY ? 0 : blankX ? 1 : -1;
  const numeric = !Number.isNaN(Number(x)) && !Number.isNaN(Number(y));
  const order = numeric ? Number(x) - Number(y) : x.localeCompare(y, "en-GB");
  return ascending ? order : -order;
}
