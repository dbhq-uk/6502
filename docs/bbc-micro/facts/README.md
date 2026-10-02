# BBC Micro fact sheets

Four sheets, written on 2 October 2026 before any code, so the BBC Micro's
chips are built from sources and not from memory. Each fact carries a tag:
`[from <source>]` was read directly, `[from ROM]` was read out of a ROM,
`[inferring ...]` joins dots, and `[guessing - verify]` is a flag for a
correction. Where sources disagree, the sheet says which it trusts.

| Sheet | Covers |
|---|---|
| [`bus.md`](bus.md) | The memory map, the paged ROM latch, what an absent device returns, the 1 MHz stretch, the CRTC clock per mode, the OS version, the reset text and reset |
| [`via.md`](via.md) | The 6522 cycle by cycle, how the BBC wires the two VIAs, the keyboard matrix and start-up links, the SN76489 |
| [`video.md`](video.md) | The 6845, the video ULA, the eight screen modes, mode 7 teletext and where its glyphs can come from |
| [`disc.md`](disc.md) | The 8271 as DFS 1.20 uses it, `.ssd` and `.dsd`, the catalogue layout, what `*CAT` prints |

**Paths under `/tmp` are the working files of the session that wrote the
sheets and were not kept.** The sources they name are public; the ROMs are in
`roms/bbc-micro/`; the throwaway disc machine is in
`tools/probes/bbc-dfs-trace/`.

**Nothing here is code from jsbeeb or B-em**, which are GPL. Where a sheet read
either one to cross-check a number, it says so.
