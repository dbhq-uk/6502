---
title: "Building on ARM"
date: 2026-10-09
summary: "Everything in the repository was built and tested on an ARM Linux machine. All of it worked except Dormann's tests, whose assembler, as65, is an x86 program that ARM cannot run. The tests now run it under qemu's user-mode emulator on any machine that is not x86, with Ubuntu's i386 libraries, and say what to install when either is missing. The browser checks need a Chromium there, because Google Chrome has no Linux ARM build."
order: 38
---

# 9 October 2026: building on ARM

Dan asked whether building everything in this repository would work on ARM.
Rather than reason about it, every step CI runs was run on `dbhq-arm`, an
Ubuntu 24.04 machine on 64-bit ARM (`uname -m` says `aarch64`), with the .NET
10.0.400 SDK installed by Microsoft's `dotnet-install.sh` and Node 24.

## What was checked, and how

On 9 October, on `dbhq-arm`, from `main` at `5640a57` with this change:

- `dotnet test --configuration Release`: the KIM-1's 39 tests, the NES's 1627,
  the core's 1608 and the BBC Micro's 980 passed, none failed and none were
  skipped.
- `dotnet workload install wasm-tools --version 10.0.401` installed the
  workload's `linux-arm64` packs, and `node scripts/build-machines.mjs kim-1
  bbc-micro nes` compiled all three machines ahead of time.
- `npm ci` in `site/` chose the `linux-arm64` builds of esbuild and sharp from
  the lock file.
- `npm test` in `site/`, and `node scripts/browser-check.mjs` with
  `CHROME_PATH` set to the Chromium that `npx playwright-core install
  chromium` downloads, both passed.

Earlier the same day, from `feat/bbc-micro-models`, `tools/perfect6502/generate.sh`
wrote a `visual6502.txt` with the same SHA-256 as the committed one, so the C
harness gives the same answer on ARM. The Python tools' libraries (numpy,
opencv-python-headless, scipy and Pillow) installed from wheels in a venv, as
their READMEs describe.

## What did not work

**Dormann's assembler.** The first `dotnet test` on ARM failed the core's 12
Dormann tests, every one with `Exec format error`: `as65` is a 2007 build for
32-bit x86 Linux (`file` says `ELF 32-bit LSB executable, Intel 80386,
dynamically linked`), and an ARM processor cannot run it. Everything else
passed.

**Google Chrome.** The browser checks and the speed benches default to
`/usr/bin/google-chrome`, and Google publishes no Linux ARM build. They already
take `CHROME_PATH`, so this needed a sentence in the docs, not code.

## Decision: run as65 under qemu off x86

On a machine that is not x86 or x64, `Dormann.cs` now starts `qemu-i386 -L
<libraries> as65 ...` instead of `as65`. The libraries are the i386 C and C++
runtime from Ubuntu's `libc6-i386-cross` and `libstdc++6-i386-cross`, in
`/usr/i686-linux-gnu`, or wherever `QEMU_LD_PREFIX` points. On x86 and x64
nothing changes. When qemu is missing, or cannot find the libraries, the test
fails with the `apt-get` line that installs both, as it already did for the
missing 32-bit libraries on x86.

Chosen against:

- **Registering qemu with the kernel's `binfmt_misc`**, so `as65` runs as it
  is with no code change. It needs `dpkg --add-architecture i386` and an extra
  apt source for i386 packages on an ARM machine, which changes how the whole
  machine updates, to run one test tool. The cross packages install like any
  other package.
- **Assembling Dormann's programs once and keeping the binaries in a
  `dbhq-uk` fork.** It would remove the assembler from ARM entirely, but the
  design assembles them at test time with Dormann's own assembler so that the
  success address comes from the listing, and the 2A03, Synertek and decimal
  builds are made from settings in the test code. A fixed set of binaries
  would move those settings out of the code that states them.
- **A script under `tools/` that fetches the i386 libraries.** This was how
  the idea was first checked: the three `.deb` files from Ubuntu's archive,
  unpacked into a scratch directory. It fetches from a host outside
  `dbhq-uk`, which `site/tests/mirrors.test.mjs` forbids (AGENTS.md rule 3),
  and the cross packages give the same libraries through apt.

## Assumed, and wrong

- **That the unpacked i386 `libc6` puts its loader where qemu looks.** It puts
  `ld-linux.so.2` in `usr/lib/i386-linux-gnu/` and `usr/lib/`, and qemu asked
  for `/lib/ld-linux.so.2` under its prefix, so the first try failed with
  `Could not open '/lib/ld-linux.so.2'`. The cross packages lay out
  `/usr/i686-linux-gnu/lib/` the way qemu expects, which is one more reason
  to use them.
- **That the first check of the missing-qemu message was a check.** It
  printed nothing, for two reasons: its test filter matched no test, and its
  `PATH` held `/bin`, which on Ubuntu is `/usr/bin`, where qemu is. Run again
  over the Dormann tests with a `PATH` holding only `dotnet`, each failed with
  `qemu-i386 could not be started` and the install line, as intended. With
  `QEMU_LD_PREFIX=/nonexistent` each failed with qemu's own `Could not open`
  and the same line.

## Not changed

CI still runs on x64 runners, where `as65` runs directly. A GitHub ARM runner
would need the `apt-get` line in `CONTRIBUTING.md` and a `CHROME_PATH`, and
nothing here moves CI to one.
