// Reads the machine and chip names out of docs/the-6502-family.md, so a test can
// check the registry against the human-written document.
const clean = (t) => t.replace(/\*\*/g, '').replace(/\*/g, '').trim();
const cells = (l) => l.trim().replace(/^\||\|$/g, '').split('|').map((c) => c.trim());

export function parseFamilyDoc(markdown) {
  const machines = [];
  const chips = [];
  let h2 = '';
  const lines = markdown.split('\n');
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].startsWith('## ')) h2 = lines[i].slice(3).trim();
    else if (lines[i].startsWith('|') && lines[i + 1]?.startsWith('|---')) {
      let j = i + 2;
      while (j < lines.length && lines[j].startsWith('|')) {
        const name = clean(cells(lines[j])[0]);
        if (h2 === 'The chips') chips.push(name);
        else if (['Computers', 'Consoles and handhelds', 'Arcade', 'Built today'].includes(h2)) machines.push(name.replace(/\s*\(\d{4}\)/, ''));
        j++;
      }
      i = j - 1;
    }
  }
  return { machines, chips };
}
