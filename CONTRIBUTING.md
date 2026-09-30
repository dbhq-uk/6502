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
skipped elsewhere. On Linux they need `libc6-i386` and `lib32stdc++6`, because
Dormann's assembler is a 32-bit program.

## Style

`.editorconfig` covers the mechanical parts. Beyond that: comments explain
*why*, particularly where something is non-obvious or was got wrong once.
