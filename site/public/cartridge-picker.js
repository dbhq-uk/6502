// The cartridge slot a console's page shares: the file button, the drop target,
// the size refusal, the read in the tab, and the order in which the bundled
// cartridge and the visitor's file go in. Moved out of nes.js, unchanged in
// what it does, when the Atari 2600 became the second console to need it (the
// Atari 2600 plan's task 13); nes.js re-exports what its tests import.
//
// A FILE IS READ IN THE TAB and never sent anywhere. A file larger than the
// largest the machine loads is refused before it is read, by its size, with
// the limit the machine's host gives (each host's MaxRomBytes, its library's
// Cartridge.MaxFileSize), never a figure typed in a page.

const fmt = (n) => n.toLocaleString('en-GB');

/** An error's message as a sentence, whatever was thrown. */
export const messageOf = (error) => (typeof error?.message === 'string' && error.message) || String(error) || 'it could not be read';

/** The line refusing a file over `limit` bytes, or null when it fits. */
export function sizeRefusal(file, limit) {
  if (file.size <= limit) return null;
  return `${file.name} is ${fmt(file.size)} bytes, more than the ${fmt(limit)} bytes of the largest file this machine loads, so it was not read.`;
}

/** Reads a chosen file, unless it is over `limit` bytes: { ok, bytes } or { ok: false, line }. */
export async function readRom(file, limit) {
  const refusal = sizeRefusal(file, limit);
  if (refusal) return { ok: false, line: refusal };
  try {
    return { ok: true, bytes: new Uint8Array(await file.arrayBuffer()) };
  } catch (error) {
    return { ok: false, line: `${file.name} could not be read: ${messageOf(error)}` };
  }
}

/**
 * The first cartridge: the bundled one, `bundled` (a promise of its bytes, or
 * of null), is tried with `use` before `openPicker` opens the picker, so a
 * file the visitor chooses is never replaced by it. Without one, or if it will
 * not load, the picker opens and `waitForFile` waits for the visitor's.
 */
export async function firstCartridge(bundled, use, openPicker, waitForFile) {
  const data = await bundled;
  const loaded = Boolean(data) && use(data);
  openPicker();
  if (!loaded) await waitForFile();
}

/**
 * The file button: `choose` opens the hidden file input `file`, and a file
 * chosen goes to put(file). The input is emptied after each choice, so the
 * same file can be chosen again.
 */
export function picker(choose, file, put) {
  choose.disabled = false;
  choose.addEventListener('click', () => file.click());
  file.addEventListener('change', () => {
    if (file.files.length > 0) put(file.files[0]);
    file.value = '';
  });
}

/**
 * The page as a drop target: a file dropped anywhere goes to panel.putRom once
 * the machine is up (the page sets it when the picker opens). Before that it is
 * refused here, with a line naming `name`, rather than the browser leaving the
 * page to show the file.
 */
export function dropTarget(panel, say, name) {
  document.addEventListener('dragover', (event) => {
    if ([...(event.dataTransfer?.types ?? [])].includes('Files')) event.preventDefault();
  });
  document.addEventListener('drop', (event) => {
    if (!event.dataTransfer?.files.length) return;
    event.preventDefault();
    if (panel.putRom) panel.putRom(event.dataTransfer.files[0]);
    else if (panel.dataset.state === 'ready') say('Press Start first, then drop the file on the page again.', 'ready');
    else if (panel.dataset.state === 'loading') say(`The ${name} is still loading. Drop the file on the page again once it runs.`, 'loading');
  });
}

/** The bytes at `url`, or an error naming the answer when it is not a success. */
export async function bytes(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url} answered ${response.status}`);
  return new Uint8Array(await response.arrayBuffer());
}
