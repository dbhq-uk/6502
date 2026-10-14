# Atari 2600 regions: the clocks, the lines, the picture window and the pixel shape

Written 14 October 2026 for the Atari 2600 plan, task 1. Tags and sources are in [`README.md`](README.md). Used by tasks 2 (`Region`), 5 (the frame and the palette), 8 (the speed rule's real time), 10 (the sample rate), 12 (the boot check) and 13 (the page's window, pixel shape and PAL suggestion).

## 1. The clocks

| | NTSC | PAL | Source |
|---|---|---|---|
| Colour clock (the TIA's oscillator, one pixel) | 3.579545 MHz | 3.546894 MHz | [from 2k6specs, "Clocks"] |
| CPU clock (colour clock divided by 3) | 1.193182 MHz | 1.182298 MHz | [from 2k6specs, "Clocks"; SPG 3.2; SPG TIA 1A 2.D] |
| Colour clocks a line | 228 | 228 | [from SPG 3.1; the suite: include/region.h] |
| CPU cycles a line | 76 | 76 | [from SPG 3.2; the suite: include/region.h] |

- The CPU clock is the colour clock divided by three, made in the TIA and given to the 6507 as its phase zero input [from SPG TIA 1A 2.D]. So the model keeps one number per region, the colour clock, and computes the CPU clock from it [inferring].
- The NTSC colour clock is the NTSC colour subcarrier, exactly 315/88 MHz (3.5795454... MHz) [from Wikipedia "Color burst"]. Korth's 3.579545 MHz is that value rounded [inferring].
- The PAL console's colour clock is not the PAL colour subcarrier (exactly 4.43361875 MHz) [from Wikipedia "Color burst"]. Wikipedia lists the PAL 2600's CPU at 1.182298 MHz as four fifteenths of the PAL subcarrier, which makes the colour clock four fifths of it, 3.546895 MHz [from Wikipedia "Color burst"; inferring: 4.43361875 x 4 / 5 = 3.546895]. Korth gives 3.546894 MHz [from 2k6specs]. The two differ in the last digit, a part in three and a half million. **The model uses 4.43361875 x 4 / 5 MHz** for PAL and 315/88 MHz for NTSC, each as an exact expression in `Region`, so nothing is typed twice [guessing - verify: no circuit diagram of the PAL console's oscillator was read; Korth's figure may be a measured crystal].
- The SPG's PAL table agrees to four figures: 312 lines take 20055 microseconds, so a line takes 64.28 microseconds and a colour clock 64.28 / 228 = 0.2819 microseconds, 3.547 MHz [from SPG "PAL/SECAM conversions"; inferring]. Its NTSC row gives 262 lines in 16686 microseconds, 3.580 MHz [same].

### Worked example 1: one second of machine time

NTSC: 315/88 MHz / 3 = 1,193,181.8 CPU cycles a second, 15,699.8 lines a second (CPU cycles / 76), and at 262 lines a frame 59.92 frames a second [inferring from the table]. PAL: 4.43361875 x 4 / 5 / 3 MHz = 1,182,298.3 CPU cycles a second, 15,556.6 lines, and at 312 lines 49.86 frames a second [inferring]. The SPG's rounded "60 times a second" and "50 Hz" are these [from SPG "Television protocol"; SPG "PAL/SECAM conversions" 3].

## 2. The lines a program makes

The TIA has no vertical counter: the program makes the frame by turning VSYNC and VBLANK on and off, and a frame has as many lines as the program draws between two VSYNCs [from SPG 3.3; SPG TIA 1A 2.B]. These are the counts Atari recommended, and the counts the suite's builds use.

| | VSYNC | VBLANK after VSYNC | Picture | Overscan | Total | Source |
|---|---|---|---|---|---|---|
| NTSC | 3 | 37 | 192 | 30 | 262 | [from SPG "Television protocol"; 2k6specs, "Vertical timing"; the suite: include/region.h] |
| PAL | 3 | 45 | 228 | 36 | 312 | [from 2k6specs, "Vertical timing"; the suite: include/region.h; SPG "PAL/SECAM conversions" 1, whose VBLANK row of 48 counts the three VSYNC lines in, as its NTSC row of 40 does] |

- So `Region.NominalLines` is 262 for NTSC and 312 for PAL [from the table], which is what the page's PAL suggestion compares a frame against (plan, "Fixed before running" 13).
- Many PAL games draw only 192 picture lines and widen the borders instead [from 2k6specs, "Vertical timing" and its note on PAL games]. So the picture's place on a PAL frame varies more than on NTSC [inferring].
- Korth found that a VSYNC of two or four lines instead of three made his television drop the colour [from 2k6specs, "Vertical timing"]. The model shows the frame as it is and models no television (spec, "Picture and region"); issue #128 covers the analogue picture.
- The suite's reference pictures hold every line after the three VSYNC lines: 259 lines on NTSC and 309 on PAL [from the suite: README.md, "How a test works"]. Its harness says the references carry lines its own frame drops, which disagrees; the plan fixes the alignment once on `harness/calibration` (plan, "Fixed before running" 4) [from the harness: crates/systems/vcs/tests/accuracy/common/mod.rs].

## 3. The picture window

A television shows only part of a frame: the VSYNC and VBLANK lines are above the screen, and overscan is below it or hidden by the bezel [from SPG "Television protocol"]. No source read gives the exact lines a television shows: it varies from set to set (as the NES's overscan does), and the SPG gives only Atari's safe picture of 192 lines [from SPG "Television protocol"; inferring]. So the window is **a choice**, made here and recorded in `docs/known-differences.md` when task 2 builds it:

| | First line shown (counted from the first line after VSYNC ends, as 0) | Lines shown | Covers |
|---|---|---|---|
| NTSC | 19 | 228 | The SPG's 192 picture lines (lines 37 to 228) with 18 lines above and 18 below |
| PAL | 27 | 264 | The 228 picture lines (lines 45 to 272) with 18 lines above and 18 below |

- Eighteen lines is the margin Korth gives for PAL games that draw 192 lines in a 228-line picture ("45+18 upper, and 36+18 lower border lines") [from 2k6specs, "Vertical timing"]. Using the same margin on both regions keeps a PAL game with a 192-line picture inside the window [inferring]. [guessing - verify: task 12 checks the bundled game's picture falls inside the window in both regions, and task 13 looks at the suite's calibration picture through it.]
- Both windows fit inside the frame's line limit of 400 (plan, "Fixed before running" 2) and inside a nominal frame less its VSYNC lines (259 and 309) [inferring: 19 + 228 = 247; 27 + 264 = 291].

### Worked example 2: where a line lands

On NTSC a program that makes the SPG's frame draws its first picture line on line 37 after VSYNC, which is row 37 - 19 = 18 of the window [inferring from the table]. On PAL a game with 45 VBLANK lines and 18 more of border (Korth's 192-line PAL game) draws its first picture line on line 63, row 36 of the window [inferring].

## 4. The pixel shape

- One pixel is one colour clock, and 160 of the 228 are drawn [from SPG 3.1; 2k6specs, "Clocks"].
- A television that samples a line into square pixels does it at 135/11 MHz (12 3/11 MHz) for 480-line (NTSC) video and at 14.75 MHz (14 3/4 MHz) for 576-line (PAL) video [from Wikipedia "Pixel aspect ratio"].
- So one colour clock is as wide as (135/11) / (315/88) = 24/7 square pixels on NTSC, and 14.75 / 3.546895 = 4.1586 on PAL [inferring].
- The 2600 draws a progressive frame of about 262 or 312 lines, each a full field line, so one of its lines is as tall as two lines of an interlaced 480 or 576-line picture [inferring from SPG "Television protocol": 262 lines a frame, 60 times a second, which is one field].
- **The pixel shape (width to height):** NTSC 24/7 / 2 = **12:7** (1.714), exactly. PAL 4.1586 / 2 = **2.079**, which `Region` holds as the ratio of two integers chosen to within 1 part in 1000 (task 2 chooses them; 104:50 is 2.080) [inferring from the two facts above].

### Worked example 3: the NTSC window on a page

The NTSC window is 160 by 228 pixels. At 12:7 it shows as 160 x 12 / 7 = 274.3 units wide for 228 tall, a ratio of 1.20 [inferring]. The PAL window is 160 by 264 at 2.079: 332.6 wide for 264 tall, 1.26 [inferring]. Both are narrower than 4:3 (1.33) because the 160 drawn clocks do not fill the television's line [inferring from SPG 3.1: 68 of the 228 clocks are blank].

## 5. What differs between the regions, and what does not

The spec decided the region switch takes effect at once, without a restart, because the two TIAs differ in the colour signal and the console in its clock, "not in any state the program can see"; this sheet was to confirm it (spec, "Decisions made while writing this").

- **What the SPG's PAL conversion lists:** the line counts, a lower sound pitch from the slower crystal, and 50 frames a second instead of 60 [from SPG "PAL/SECAM conversions" 1 to 3]. It lists no register, timing or memory difference [from SPG "PAL/SECAM conversions"].
- **The line is the same:** 228 colour clocks and 76 CPU cycles on every standard; only the line counts differ [from the suite: include/region.h; 2k6specs, "Video synchronization and blanking"].
- **The suite's self-test verdicts are region-independent** by its own statement, and every one of its sources builds for every standard with only the line counts and the result screen's colours changed [from the suite: README.md, "Build"; include/region.h]. A program-visible difference in the TIA, the RIOT or the bus would have to show in some self-test [inferring].
- **The colour signal differs:** the PAL TIA's hues are arranged differently (NTSC green is hue `$C`, PAL green hue `$5`; PAL hue 1 is grey), confirmed on a PAL console by the suite's author [from the suite: include/region.h]. `tia.md` gives each region's colour signal.
- **The chips differ in their pins, not their registers:** on the PAL TIA the audio and luminance outputs sit on different pins from the NTSC part [from 2k6specs, the video-output modification's pin table]. Nothing the program reads changes with that [inferring].
- **Confirmed, with one caution:** nothing the program can see differs, so the switch changes the palette and the clock at once with no restart [inferring from the five points above]. The caution: no source read compares the two TIAs' logic directly, only what programmers and the suite see. [guessing - verify: if task 11 finds a self-test that passes on one region's TIA model and fails on the other's with the same program, the region switch restarts the machine instead, and the journal says why.]
- **What the program does see:** nothing tells a program which region it runs on; a game is built for one, and the colour switch decides the palette in software [from SPG "PAL/SECAM conversions"; 2k6specs, the console switches: SWCHB bit 3]. 2048 2600 makes PAL frames when the colour switch is at black and white (spec, "How it plays on the page"), a choice of the game's, not of the console.

## 6. SECAM

Not modelled (spec, issue #106). For the record: SECAM consoles run PAL software, have the colour switch wired to black and white, and show eight fixed colours chosen by the luminance bits alone [from SPG "PAL/SECAM conversions", SECAM 1; 2k6specs, the console switches; the suite: include/region.h].

## 7. Open items

1. The PAL colour clock's last digit (section 1): 3.546894 (Korth) or 3.546895 (four fifths of the subcarrier). The model takes the subcarrier ratio; a test of `Region` cites this section.
2. The picture window is a choice (section 3), checked in tasks 12 and 13.
3. The region switch at once rests on an inference (section 5), checked by task 11.
