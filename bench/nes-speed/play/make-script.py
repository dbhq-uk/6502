# Writes ../lan-master-play.json: the scripted play session of the bundled homebrew, Lan Master, that the
# speed benches and the line probe feed to the pad frame by frame (task 3b of the scanline renderer work).
#   python3 bench/nes-speed/play/make-script.py > bench/nes-speed/lan-master-play.json
#
# A step [frame, mask] sets controller 1 to mask before the PPU's frame number `frame` runs; the mask stays
# until the next step. Bits: 1 A, 2 B, 4 Select, 8 Start, 16 Up, 32 Down, 64 Left, 128 Right.
#
# The first level has a clock of 60 seconds of the game's own time, so 3600 frames on NTSC and 3000 on PAL
# (found by running it with no buttons held after Start): TIME IS OUT starts about frame 3600 and 2900. The
# script plays the level until about 55 seconds into it and stops there, so it never meets the game over
# screen, which waits for a button. `limit` is the last frame of play in each region.
import json

A, B, SELECT, START, UP, DOWN, LEFT, RIGHT = 1, 2, 4, 8, 16, 32, 64, 128
# Two squares round the board's middle: right, down, left, up, twice, a turn after each move or two.
ROUND = [RIGHT, A, RIGHT, B, DOWN, A, DOWN, B, LEFT, SELECT, LEFT, A, UP, B, UP, SELECT,
         LEFT, A, DOWN, B, RIGHT, A, UP, SELECT]
LIMIT = {"NTSC": 3450, "PAL": 2850}

presses = [
    (52, 3, DOWN),     # in the title's menu: down to CODE
    (64, 3, UP),       # and back to START
    (90, 6, START),    # begin the first level
]
f = 170                # the board is up and the cursor answers by about frame 160
while f < max(LIMIT.values()):
    presses.append((f, 3, ROUND[(f - 170) // 6 % len(ROUND)]))  # one button held 3 frames in 6
    f += 6

steps = []
for first, held, mask in sorted(presses):
    steps += [[first, mask], [first + held, 0]]

phases = [
    ["title", 0],        # the title fades in, its music starts
    ["menu", 36],        # the menu: START, CODE, SFX, BGM, and the square moves
    ["transition", 96],  # Start was pressed: the title fades out, the board fades in
    ["level", 150],      # the board, the cursor, the clock running
]
text = json.dumps({
    "about": "Lan Master (roms/nes/Lan_Master.nes) played by script, for bench/nes-speed and bench/thread-time: steps are [frame, buttons held on controller 1 from that PPU frame on], phases are [name, first frame], limit is each region's last frame of play. Made by bench/nes-speed/play/make-script.py.",
    "limit": LIMIT,
    "phases": phases,
    "steps": steps,
}, separators=(",", ":"))
print(text.replace(',"limit"', ',\n"limit"').replace(',"phases"', ',\n"phases"').replace(',"steps"', ',\n"steps"'))
