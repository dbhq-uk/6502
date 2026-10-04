---
title: "The BBC Micro counts"
date: 2026-10-04
summary: "The BBC Micro's page goes live in the build: a credited photograph of an original machine, the registry switched to running, and the home page's count of machines going up by one, from the registry and never typed. The preview that built the page before it counted is deleted, and the page's tests and the browser check now read the site that deploys."
order: 25
---

# 4 October 2026: the BBC Micro counts

Task 14b of the BBC Micro plan, the second half of task 14. Task 14a built the
page and kept it out of the deployed site, because the registry still said
planned until a photograph was chosen. This task adds the photograph, switches
the registry record to running, and lets everything that reads the registry
follow: the machine's page, the machines table, the home page's count and its
sentence about which machines run, and the sitemap. The branch is not merged
here; merging deploys, and that is Dan's call.

## The photograph

Chosen by research earlier the same day, against about 25 candidates on
Wikimedia Commons, of which twelve were looked at in full:
[`File:Acorn BBC Micro.jpg`](https://commons.wikimedia.org/wiki/File:Acorn_BBC_Micro.jpg),
by simon.inns (Simon Inns, by his Flickr account), CC BY 2.0. It is a whole
original machine from the front left and above, on a plain blue cloth, sharp,
5195 pixels wide. The closest rival was Daniel Beardsmore's public-domain
photograph, straight down from above on a carpet at half the size, whose page
does not say Model B in words; the white-background public-domain one is under
2000 pixels, its author is "assumed" by a bot, and the machine has an add-on
fitted. The research's full table is not in the repository; the photographs'
README records the chosen file's provenance in the KIM-1's shape.

**What the source says, and what the picture can show.** The file page
describes it as "An Acorn BBC Micro Model B from 1982", and the page's caption
says "an original Acorn BBC Micro Model B" on that word and no further. The
picture alone cannot tell a Model B from a Model A: the case, the keyboard and
the badge are the same, and the difference is inside and at the back. That is
said in the README rather than claimed on the page.

**Checked again before use.** The copy downloaded for the research hashed to
the SHA-256 it recorded (`2329615e...ca91`), and the file fetched again from
`upload.wikimedia.org` on 4 October 2026, with a User-Agent naming the site,
was the same 2,257,642 bytes with the same SHA-256. The Commons API still gave
CC BY 2.0, attribution required, the author `simon.inns`, the description above,
and the SHA-1 Commons records, `71f6dfdf...0b4b39`.

**The crop.** The KIM-1's photograph was used as Commons has it, already
cropped. This one has the machine on blue cloth with uneven margins (262
pixels at the left, 163 at the right) and a white mains lead entering at the top
left. The machine's edges were found by marking every pixel that is not the
cloth's blue (blue above red by 25 and above green by 10) and taking the rows
and columns where more than 2 per cent are not blue: columns 262 to 5032, rows
170 to 3291. The crop is that box with 110 pixels of cloth on every side, so
the whole case shows with an even margin, then resized to 1600 wide:
`cwebp -q 82 -crop 152 60 4991 3342 -resize 1600 0 -metadata none`, 1600 by
1072 pixels, 88,150 bytes. Chosen over the full frame (uneven margins, more
cloth than machine at the left) and over a tighter crop (the case's corners
would touch the edge). The lead cannot be cropped out without cutting the case,
so its stub stays at the top left. Nothing else is changed: no colour, no
levels, no mask. The blue is not the site's black canvas, and that is the
photograph as it was taken. The 2.2 MB original is not committed, as the
KIM-1's were not: the URL, the fetch date and both hashes are enough to fetch
it again and check it.

**The licence and the credit.** CC BY 2.0 asks for attribution and nothing
more; the KIM-1's photographs are CC BY-SA, which also asks for share-alike.
`MachinePhoto.astro` already writes the credit from the registry: "By
simon.inns. Source: commons.wikimedia.org. Licence: CC BY 2.0.", with the source
and the licence's deed linked. It names the licence the source states and
assumes nothing about share-alike, so it needed no change; a new test checks
the BBC Micro's credit as built. The photographs' README said "these files are
not MIT", which is true of both licences; it now also says the BBC Micro has no
3D model, so nothing is taken from its photograph. The repository's README
listed the photographs' licences by source and now adds CC BY 2.0.

## The registry, and the preview deleted

The record's `status`, `acceptance`, `rights` and `notes` were copied from task
14a's fixture exactly, by a script that asserted each was equal afterwards; the
fixture's stand-in photograph (the KIM-1's) was not. The live schema is a
`photos` list, as the KIM-1's is (`photo` is refused by the validator), so the
BBC Micro gets a list of one, in the KIM-1's field order, with `used` saying
that nothing is traced or measured from it.

Then the preview went: `REGISTRY_PREVIEW`, `applyPreview`, the fixture,
`SITE_DIST`, the temporary build in `bbc-page.test.mjs`, the run of the whole
suite on it, and the second build and server in the browser check, whose BBC
section now runs on the same server as the KIM-1's. Chosen over keeping the
mechanism for the next machine: it was built for one handover, the next
machine's page may not need one, and code nobody runs goes stale. Also deleted,
because the preview was what gave them meaning: the test that the deployed site
has no BBC Micro page, the test that no workflow sets the preview, the two
tests of the preview itself, and the test that the KIM-1's page is unchanged by
the preview. In their place: the registry says running, names the acceptance
test and has the photograph; the page is linked from the machines table; the
home page's count and sentence come from the registry and name both machines;
and the photograph's credit is as the source gives it.

## The count

Nothing on the home page was edited. Built with the new registry, it says
"2 of 51" machines implemented and "Running in the browser now: KIM-1 and BBC
Micro.", the BBC Micro's card on the home page links its page, and the sitemap
lists `/machines/bbc-micro/`. `runningSentence` already joined two names with
"and"; it now has a test for exactly two. No site test had typed "1 of 51";
the two tests that assert one machine implemented use made-up registries.

## What else changed with it

- **CI floors.** The site suite went from 261 tests to 258: seven went with the
  preview and four came in. Both workflows' floors are 258, with the history in
  the comment. The deploy's check that every machine file serves had a floor of
  6, set for the KIM-1 alone. Measured in `dist/` after `npm run build`: 26
  files, 13 for each machine. The new floor is 21, the least the two must
  publish (the five ROMs, and for each machine the four runtime files, CoreLib,
  the core, the machine and its host), which the KIM-1's 13 alone cannot meet.
  Not 26, because a .NET update that drops an assembly (the KIM-1 publishes
  `System.Linq` and the BBC Micro does not) would fail a deploy that is fine.
  There is no floor on the .NET test count in either workflow.
- **The About page said no ROM is committed**, and that a running machine's ROM
  is fetched from a pinned copy at build. That has been false since 2 October,
  when the system ROMs were committed under `roms/`. It now says the ROMs are
  committed with their provenance and checked against their hashes, that the
  photographs keep their sources' licences, and that no game or third-party test
  program is committed. Its Illustrations paragraph began "The images here were
  generated", which the photographs made untrue; it now says the illustrations.
- **The CSP comment** in `public/_headers` gave the KIM-1's page as the reason
  for `'wasm-unsafe-eval'`; it now says each machine's page, and its test follows.
- **The repository's README** says the BBC Micro runs, passes its acceptance
  test and counts, with a link to its page, and that the NES is next.
  `docs/the-6502-family.md` says of both machines that the project built them
  and that they run in the browser. Not "on the core": the family page's test
  forbids that phrase, which on that page would read as a claim about the CPU.
  `AGENTS.md`'s layout line for the BBC Micro no longer says its chips arrive
  task by task.

## What was measured

All on 4 October 2026, on this machine.

- `npm run build && npm test` in `site/`: 258 tests, 258 pass, 0 fail.
- `dotnet test -c Release`: Kim1 39, BbcMicro 941, Cpu6502 1597, all passing,
  no warnings; `make-results.mjs` read 2,577 passing in 46 suites.
- `node scripts/browser-check.mjs`, now with both sections on `dist/`: passes in
  3 m 36 s, no console errors, no failed requests, no CSP violations. The BBC
  Micro was running 889 ms after Start and at the prompt 887 ms later, `PRINT
  6*7` showed 42, `PRINT "A"` tapped on the on-screen keys showed A, a disc
  listed with `*CAT` and saved the same 102,400 bytes, and Break restarted it;
  the photograph loaded twice from this site (the head and the photographs
  section, the same AVIF).
- `node scripts/page-weight.mjs /machines/bbc-micro/`, before Start: 116,149
  bytes, 51,442 gzipped, of which the photograph is 22,002 (the WebP the
  build made from it at the page's width). Task 14a's figure, 169,293, carried
  the KIM-1's photograph as a stand-in.
- Screenshots of `/machines/bbc-micro/` at 1280 and 390 pixels wide, from
  `dist/`: the photograph in the head with its caption and credit, the panel,
  the on-screen keys (wrapping on the phone), the disc drive, the not-modelled
  list and the photographs section. The home page's status card read "2 of 51".

## Mistakes

- The first draft of the family doc's sentence said each machine "runs on the
  core, in the browser", which the family page's own test forbids. Caught by
  reading the test before running it.
- Rewrapping the floor comment in the workflows split "Raise the number when
  the suite grows" across two lines, and `build.test.mjs`, which looks for that
  phrase, failed. The phrase is back on one line.
