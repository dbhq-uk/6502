---
title: "The home, about, status and inside pages made shorter"
date: 2026-10-15
summary: "Part of the site tidy pass. The home page now says what the project is, which machines run and where to start, in about half the words. The about, status and inside pages keep every fact in fewer, plainer sentences. Every figure is still computed."
order: 64
---

# 15 October 2026: the home, about, status and inside pages made shorter

Dan asked for the whole site to be concise, plain and easier to read. This entry covers the pages without a machine on them. The machine pages and the margins are separate parts of the same pass.

## What changed

The home page leads with what the project is and two buttons. The lime one now reads "Run a machine" and goes to the machines; it used to read "Read the journal", which is now the plain second button. The "See where it stands" button, which jumped to the status section, went: that section is on the same page and the header links to the status page. Its machines section now lists every machine that runs, from the registry, each with its year and CPU, in priority order, then registry order, as a list, with no "Running" label on each card because the heading and the sentence above them already say it. It used to show the three machines first in line (the KIM-1, the BBC Micro and the NES, all running) with their long notes; those notes are on each machine's own page. The journal list on the home page shows titles and dates only, and the summaries stay on the journal index. The proof section's heading, "A difference is a number, not an argument", became "Checked cycle by cycle". The status cards and the speed card say the same with fewer words.

The about page's licence paragraph became a short list, one item per kind of thing that is somebody else's. Every fact in it stayed: the ROMs and their hashes, the photographs' licences, the bundled software and its authors' permission, the visitor's own games, and the pinned test programs.

The status page's speed paragraph is split in two, what was measured and where, and drops repeats. Every phrase the figure tests hold it to is still there. The inside page's caption is a little shorter. The analytics notice, the header and the 404 page were already short and are unchanged. The footer gained the missing space before "Cookie settings".

## How it was checked

The words in the built `<main>` of each page were counted before and after, on 15 October 2026, with this command from the repository's root after `npm run build` in `site/`, its path changed for each page:

```
node -e "const h=require('fs').readFileSync('site/dist/index.html','utf8');const m=h.match(/<main[\s\S]*?<\/main>/)[0].replace(/<(script|style)[\s\S]*?<\/\1>/g,' ').replace(/<[^>]+>/g,' ').replace(/&[a-z#0-9]+;/gi,' ');console.log(m.split(/\s+/).filter(w=>/[A-Za-z0-9]/.test(w)).length)"
```

The home page went from 768 words to 394, and the about page from 342 to 317. The home page's count moves with the titles of the three newest journal entries, so it is a record of the day. The site's tests passed without a change, so no check was weakened: the running sentence, the headline figures, the design target beside its verdict and the claims about Tom Harte's tests are all still asserted.
