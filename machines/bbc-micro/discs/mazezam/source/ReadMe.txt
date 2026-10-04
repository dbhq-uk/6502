MazezaM for the BBC Micro Model B - v1.33
----------------------------------------

Changes from v1.32 to 1.33
--------------------------

The ghosting during scrolling was much less noticeable on the Beeb than the Electron but, once
I had spotted it, I just couldn't leave it alone.
This has now been rectified.

Changes from v1.31 to 1.32
-------------------------

While converting MazezaM to the Electron I needed a faster way of drawing the background bricks.
The speed increase was significant enough to include it in the Beeb version too.

Changes from v1.3 to 1.31
-------------------------

Bug fix: Have resolved the compatibility problem with the Master 128.

Changes from v1.2 to 1.3
------------------------

Shifted the code down by 1 page so that I could squeeze in my own 'Wumpus Room' level.
This also needed a small change to the level display routine as it exceeds the 2 byte width for each row.
Provided I haven't broken anything then this should be the first 'release' version.

Changes from v1.1 to 1.2
------------------------

Just shifted a few bits of code around and moved some more data into zero page.
Nothing very drastic.

Changes from v1.0 to 1.1
------------------------

Thanks to Pitfall and Dave, the 'Up' and 'Down' keys have now been remapped to make them more Beeb-like,
as has the 'Restart' function which is now via the 'Esc' key.

Added spanky new 'Retro Software' loading screen.

Saved a few bytes. Added a few more. Now have 15 bytes free.

-------------------------

In November of 2010 I came across this game,by Malcolm Tyrrell, on
World Of Spectrum and very quickly became hooked.

The notes from the author positively encouraged conversion to other platforms
and after finishing the many other projects which I had on the go 
at the time - and a few which cropped up in-between - the Easter holidays
of 2011 finally gave me the opportunity to have a go.
During this time I wrote a version for the PC, closely followed by one for the 
TI-83/84 Plus calculators. 
The following half term I succumbed to the urge to
convert it to the BBC. While 6502 asm was never my language of choice, and not
having used it since I was at school - (how many times should one person have
to write a machine code bubble sort ???) - I managed to squeeze the program 
under a Mode 2 graphics screen with 3 bytes to spare.

The game uses the extended set of 'mazezams' provided with the ZX81 version of the game,
which Malcolm kindly made available to me, for which he still retains the copyright.

There are also 8 levels of my own design, taken from my PC remake.
This version contains a total of 39 different levels. 

Instructions
------------
The disc image will autoboot or can be run using ' CH."LOADER" '.

You must escape from the MazezaMs by navigating through each maze.
From the title screen, press any key to start.

Use the 'Z','X','*' and '?' keys to move.

Blocks may only be moved by pushing, not pulling.

If you should become trapped, then pressing the 'Esc' key will restart the current level,
but lose you 1 of your lives.

To quit to the title screen press the 'Q' key.

Should you be completely defeated by a particular level, then pressing 'f1' will
skip to the next one.

For full details on the game, visit: http://www.worldofspectrum.org/infoseekid.cgi?id=0014525&loadpics=1

I hope you enjoy this game as much as I have.

Kian

MazezaM was written entirely in assembly language, using the rather splendid 'Swift' with 'Beeb-ASM'.

Any thoughts or comments are always welcome.

