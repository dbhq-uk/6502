# Journal

The record of how this was built, kept as it happens. It is the source for
the journal at 6502.dbhq.uk: the site's entries are written from these, so
nothing on the site should need anyone to remember what happened.

## How entries are kept

- **One file per working day that changed something,** named
  `YYYY-MM-DD-short-title.md`.
- **Written in the same pull request as the work it describes.** An entry
  written a week later is a reconstruction.
- **Decisions carry their reason and what was chosen against,** so a reader
  can disagree with the reasoning rather than only the outcome.
- **Mistakes and corrections stay in.** A wrong assumption that a test caught
  is the most useful thing a build-in-public journal can show.
- **A figure is a dated measurement with the command that produced it.**
  "On 30 September, `python3 tools/probes/check_harte_findings.py` passed all
  39 checks" can be rerun and checked. A figure describing the current state
  of the project belongs on the site, generated from test output, and never
  here.
- **Each entry has an `order` in its front matter, and on any one date no two entries share one.** The site lists entries newest first by date, then by `order`, then by file name. Within a day, `order` is the sequence the work happened in, so give a new entry a number above every other entry of that day. The numbers need not run on from one day to the next. If you find a tie, or a merge makes one, renumber the entry that was filed later and move any later entry of that day up with it. A test in `site/tests/journal.test.mjs` fails on a tie.
