// The KIM-1's teletype, checked in a real browser, on the KIM-1 page that
// scripts/browser-check.mjs has already opened and watches for console
// errors, failed requests and CSP violations.
//
// With the TTY switch off, a key typed in the teletype's field reaches the
// teletype (the wiring echoes it to the paper) and presses nothing on the
// keypad. Then the switch goes on, RS is clicked and RUB OUT pressed at 110
// baud, the Teletype Model 33's speed: the paper must print KIM and the
// address, and an address typed after it must be opened. Then the speed goes
// to 1200 baud and the session in machines/kim-1/teletype.json is played
// through the page's own controls, keypad keys clicked and teletype keys typed
// into the field (Backspace for RUB OUT, the LF button for LF, Enter for CR),
// and the paper's last lines checked after every step against the file, the
// same file Kim1TeletypeTests runs. 1200 baud keeps the session to seconds;
// at 110 it takes the best part of a minute of the machine's time. Tab must
// leave the field, and the switch going off must hand the monitor back to the
// digits, which show the cell the session last opened (the last line the file
// promises; Kim1TeletypeTests checks the same on the machine).
//
// Keypad keys and teletype keys are queued separately, but the page's machine
// holds teletype keys back until the keypad taps before them are done
// (Kim1Keystrokes.TypeAtTeletype), as a person presses RS and then RUB OUT, so
// this script need not wait between them.
import { loadTeletype, parseKeys, parseTeletype } from '../src/lib/machines.mjs';

const lineMatches = (expected, shown) => expected.length === shown.length && [...expected].every((c, i) => (c === 'x' ? /[0-9A-F]/.test(shown[i]) : c === shown[i]));

export async function checkKim1Teletype({ page, problems, timeout }) {
  const panel = page.locator('[data-kim1]');
  const field = page.locator('[data-kim1-type]');
  const tty = page.locator('[data-kim1-tty]');
  await tty.scrollIntoViewIfNeeded();
  const paperText = async () => (await panel.getAttribute('data-paper')) ?? '';
  const lastLines = async (n) => (await paperText()).split('\n').slice(-n);

  // Waits until nothing is queued and the monitor has stopped printing, and
  // the paper ends with the lines expected (x any hex digit).
  const settled = async (prints) => {
    await page.waitForFunction(
      (want) => {
        const p = document.querySelector('[data-kim1]');
        if (p.dataset.ttyIdle !== '1' || p.dataset.pending !== '0') return false;
        const got = (p.dataset.paper ?? '').split('\n').slice(-want.length);
        return got.length === want.length && want.every((w, i) => w.length === got[i].length && [...w].every((c, k) => (c === 'x' ? /[0-9A-F]/.test(got[i][k]) : c === got[i][k])));
      },
      prints,
      { timeout, polling: 100 },
    ).catch(() => {});
    const shown = await lastLines(prints.length);
    return { shown, pass: shown.length === prints.length && prints.every((w, i) => lineMatches(w, shown[i])) };
  };

  // Types keys written as teletype.json writes them, through the page's own controls.
  const type = async (keys) => {
    for (const part of parseTeletype(keys)) {
      if (part.text) {
        await field.focus();
        await page.keyboard.type(part.text, { delay: 20 });
      } else if (part.key === 'SPACE') {
        await field.focus();
        await page.keyboard.type(' ');
      } else if (part.key === 'CR') {
        await field.focus();
        await page.keyboard.press('Enter');
      } else if (part.key === 'RUBOUT') {
        await field.focus();
        await page.keyboard.press('Backspace');
      } else {
        await page.locator(`[data-kim1-send="${part.key}"]`).click();
      }
    }
  };

  // With the switch off, the field types at the teletype and not at the keypad:
  // the key reaches the paper, no keypad tap is queued, and none is announced.
  const shownBefore = await panel.getAttribute('data-display');
  await page.evaluate(() => {
    window.kim1KeyEvents = 0;
    document.querySelector('[data-kim1]').addEventListener('kim1:key', () => window.kim1KeyEvents++);
  });
  await page.locator('[data-kim1-clear]').click();
  await field.focus();
  await page.keyboard.type('1');
  await page.waitForFunction(() => document.querySelector('[data-kim1]').dataset.paper === '1', null, { timeout: 10_000, polling: 100 }).catch(() => {});
  const echoed = await paperText();
  const shownAfter = await panel.getAttribute('data-display');
  const pending = await panel.getAttribute('data-pending');
  const tapped = await page.evaluate(() => window.kim1KeyEvents);
  console.log(`teletype, switch off: typed 1 in the field; the paper shows "${echoed}", the digits "${shownBefore}" then "${shownAfter}", keys pending ${pending}, keypad taps announced ${tapped}`);
  if (echoed !== '1') problems.push(`teletype: with the switch off, a key typed was not echoed to the paper ("${echoed}")`);
  if (pending !== '0' || tapped !== 0) problems.push(`teletype: a key typed at the teletype pressed the keypad (${pending} keys pending, ${tapped} taps announced)`);
  if (shownAfter !== shownBefore) problems.push(`teletype: a key typed at the teletype changed the digits from "${shownBefore}" to "${shownAfter}"`);

  // Tab leaves the field: the page never keeps the keyboard.
  await field.focus();
  await page.keyboard.press('Tab');
  const left = await page.evaluate(() => document.activeElement?.matches('[data-kim1-type]') === false);
  console.log(`teletype: Tab from the field moves focus on: ${left}`);
  if (!left) problems.push('teletype: Tab did not leave the field');

  // The switch on, RS, RUB OUT, at 110 baud.
  const t0 = Date.now();
  await page.locator('[data-kim1-clear]').click();
  await page.locator('[data-kim1-mode]').check();
  await page.locator('[data-key="RS"]').click();
  await page.locator('[data-kim1-send="RUBOUT"]').click();
  const logOn = await settled(['KIM', 'xxxx xx']);
  console.log(`teletype at ${await page.locator('[data-kim1-baud]').inputValue()} baud: RS and RUB OUT printed ${JSON.stringify(logOn.shown)} (${Date.now() - t0} ms)`);
  if (!logOn.pass) problems.push(`teletype at 110 baud: RS and RUB OUT printed ${JSON.stringify(logOn.shown)}, not KIM and an address`);
  const t1 = Date.now();
  await type('0002[SPACE]');
  const opened = await settled(['xxxx xx 0002', '0002 xx']);
  console.log(`teletype at 110 baud: 0002 and SPACE printed ${JSON.stringify(opened.shown)} (${Date.now() - t1} ms)`);
  if (!opened.pass) problems.push(`teletype at 110 baud: 0002 and SPACE printed ${JSON.stringify(opened.shown)}`);
  console.log(`teletype at 110 baud, the paper: ${JSON.stringify(await paperText())}`);

  // The session from the file, at 1200 baud.
  await page.locator('[data-kim1-baud]').selectOption('1200');
  await page.locator('[data-kim1-clear]').click();
  const { steps } = loadTeletype('kim-1');
  for (const [i, step] of steps.entries()) {
    const t = Date.now();
    if (step.keypad) for (const key of parseKeys(step.keypad)) await page.locator(`[data-key="${key}"]`).click();
    if (step.type) await type(step.type);
    const got = await settled(step.prints);
    console.log(`teletype step ${i + 1} ${got.pass ? 'ok  ' : 'FAIL'} ${step.keypad ? `keypad "${step.keypad}" ` : ''}${step.type ? `typed "${step.type}" ` : ''}expected ${JSON.stringify(step.prints)} printed ${JSON.stringify(got.shown)} (${Date.now() - t} ms)`);
    if (!got.pass) problems.push(`teletype step ${i + 1}: expected ${JSON.stringify(step.prints)}, the paper shows ${JSON.stringify(got.shown)}`);
  }
  console.log(`teletype at 1200 baud, the paper: ${JSON.stringify(await paperText())}`);
  const rows = await page.locator('[data-kim1-paper] > div').count();
  const lines = (await paperText()).split('\n').length;
  if (rows !== lines) problems.push(`teletype: the paper has ${rows} rows for ${lines} lines`);

  // The switch off: the monitor leaves the teletype for the keypad and the
  // digits, which show the cell the session last opened, from the file's last
  // line ("0212 3C" is the digits 02123C).
  const lastLine = steps.at(-1).prints.at(-1);
  const wantDigits = lastLine.replaceAll(' ', '');
  await page.locator('[data-kim1-mode]').uncheck();
  await page.waitForFunction((want) => document.querySelector('[data-kim1]').dataset.display === want, wantDigits, { timeout: 10_000, polling: 100 }).catch(() => {});
  const back = await panel.getAttribute('data-display');
  console.log(`teletype: the switch off, the digits show "${back}"`);
  if (back !== wantDigits) problems.push(`teletype: with the switch off the digits show "${back}", not "${wantDigits}", the cell the teletype last opened`);
}
