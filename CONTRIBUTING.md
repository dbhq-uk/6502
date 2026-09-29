# Contributing

## The one rule

**A difference is a number, not an argument.** Every claim about how the
hardware behaves is backed by a test against recorded data from the real chip
or a trusted reference. Where the reference and this code disagree, the test
says which cycle and which byte.

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

Requires the .NET 10 SDK.

## Style

`.editorconfig` covers the mechanical parts. Beyond that: comments explain
*why*, particularly where something is non-obvious or was got wrong once.
