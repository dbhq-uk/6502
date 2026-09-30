# 30 September 2026: the programme changes shape

The core was merged and the browser speed check was in. The next step was the
site, and designing it changed what the project is for.

## The decisions, in the order they were made

**The look.** Four mocks of the home page were built from the real brand
tokens: paper (like dbhq.uk), ink (like skills.dbhq.uk), an amber phosphor
look, and two built from reference styles sent in: a black page with serif
headlines, and a phosphor terminal. The phosphor terminal was chosen. It is a
deliberate fork of the DBHQ brand, green where the brand is blue, and the
site's design notes will record it as such.

**Imagery.** Four draft images were generated for about two cents, then the
three that worked were made again at high quality for about fifteen cents.
They are illustrations. The chip is a generic die, not a photograph of the
6502, and the board had twelve keys where a KIM-1 has twenty-four, so it was
dropped. Every image on the site will say it is an illustration.

**The mission grew.** The aim is now to implement as many 6502-family machines
as possible, not three. The KIM-1, BBC Micro and NES are the first of a long
list, and the site's headline figure is the number of machines implemented.

**What counts.** A machine counts only when it runs in the browser and passes
an automated test in CI. A machine that boots but has no passing test shows as
in progress. This keeps the headline as strict as the core's own claims.

**A table of every 6502.** The site gets a table of the whole family, from a
registry file, filterable and sortable, with each machine's status. A test
keeps the registry and the family document in step.

**Analytics.** The site follows the estate's existing pattern: the one shared
GA4 measurement ID, consent denied until accepted, GA loaded only on the live
host, and a Search Console property under the domain roll-up.

The design is
[`docs/superpowers/specs/2026-09-30-machines-and-site-design.md`](../superpowers/specs/2026-09-30-machines-and-site-design.md).
