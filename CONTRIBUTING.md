# Contributing

## The one rule

**A difference is a number, not an argument.** Every claim about how the
hardware behaves is backed by a test against a trusted reference, such as Tom
Harte's SingleStepTests, a published record of every opcode's bus activity.
Where the reference and this code disagree, the test says which cycle and which
byte.

In practice:

- A fix comes with the test that failed before it.
- A figure in the documentation or on the site comes from test output. It is
  never typed by hand.
- Where real hardware varies from chip to chip, as it does for the unstable
  undocumented opcodes, say so rather than claim one answer.

## Building

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK. The first `dotnet test` downloads about 5 GB of
test data into `.testdata/`. The Dormann tests run on Linux only and are
skipped elsewhere. On x86 Linux they need `libc6-i386` and `lib32stdc++6`, because
Dormann's assembler is a 32-bit program.

On an ARM machine, or any Linux that is not x86, the assembler cannot run
directly, so the tests run it under qemu with Ubuntu's i386 libraries:

```bash
sudo apt-get install qemu-user libc6-i386-cross libstdc++6-i386-cross
```

They look for the libraries in `/usr/i686-linux-gnu`, where those packages put
them; set `QEMU_LD_PREFIX` to use another directory. Everything else builds and
runs on ARM as it is. The browser checks need a Chromium, because Google Chrome
has no Linux ARM build: set `CHROME_PATH`, for example to the one
`npx playwright-core install chromium` downloads.

## Style

`.editorconfig` covers the mechanical parts. Beyond that: comments explain
*why*, particularly where something is non-obvious or was got wrong once.
