// The paper in the KIM-1's teletype: what the characters the machine printed
// look like once a carriage return has sent the head back and a line feed has
// moved the paper on. The same rules as the tests' TeletypePaper
// (tests/Dbhq.Machines.Kim1.Tests/TeletypePaper.cs): CR goes to the start of
// the line, LF to a new line, a printable character is printed at the head and
// moves it on (over whatever was there), and every other character, NUL, RUB
// OUT and XOFF among them, prints nothing. No DOM here, so a test can run it;
// kim-1.js draws the lines.

export class Paper {
  /** @param {number} max The most lines kept: older ones are torn off the top. */
  constructor(max = 1000) {
    this.max = max;
    this.clear();
  }

  clear() {
    this.lines = [''];
    this.column = 0;
    // Lines counted from the first printed since the paper was cleared, torn-off ones included.
    this.torn = 0;
    this.taken = 0;
  }

  /**
   * Prints text and says what changed: `from`, the first line (an index into
   * `lines` as it is now) that is new or different, and `torn`, how many lines
   * came off the top to keep within `max`.
   */
  print(text) {
    let from = this.lines.length - 1;
    for (const c of text) {
      if (c === '\r') this.column = 0;
      else if (c === '\n') this.lines.push('');
      else if (c >= ' ' && c <= '~') {
        const i = this.lines.length - 1;
        const line = this.lines[i].padEnd(this.column, ' ');
        this.lines[i] = line.slice(0, this.column) + c + line.slice(this.column + 1);
        this.column++;
        from = Math.min(from, i);
      }
    }
    const torn = Math.max(0, this.lines.length - this.max);
    if (torn > 0) this.lines.splice(0, torn);
    this.torn += torn;
    return { from: Math.max(0, from - torn), torn };
  }

  /**
   * The lines completed since the last call, trimmed, blank ones left out: a
   * line is complete once a line feed has moved the paper on from it, and the
   * open line is not given out until then. For a screen reader, which should
   * hear a line whole and once, not a character at a time.
   */
  takeCompleted() {
    const done = this.torn + this.lines.length - 1;
    const fresh = this.lines.slice(Math.max(0, this.taken - this.torn), this.lines.length - 1).map((l) => l.trimEnd()).filter(Boolean);
    this.taken = done;
    return fresh;
  }

  /** The paper as text, a line to a line, with the spaces at their ends taken off. */
  text() {
    return this.lines.map((l) => l.trimEnd()).join('\n');
  }
}
