#!/usr/bin/env bash
# Runs the NES outside model's measurements, in order, from the inputs.
#
#   NES_MODEL_INPUTS=<folder of inputs> tools/nes-model/run-case.sh
#
# The folder holds the inputs under the names data/sources.json gives (the
# README says how to fetch each by hand), and every script checks an input's
# SHA-256 before it reads it. PYTHON picks the interpreter, for a venv:
#   PYTHON=/tmp/nesvenv/bin/python tools/nes-model/run-case.sh
# It reads data/parts.json and data/registration.json, so run-board.sh comes
# first. pdfimages (poppler-utils) reads the patent, O1.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
py="${PYTHON:-python3}"
: "${NES_MODEL_INPUTS:?set NES_MODEL_INPUTS to the folder of inputs (see tools/nes-model/README.md)}"
cd "$here"

"$py" verify.py O1 O2-FL O2-BR O4 O5 O9 I7-FL
"$py" case_measure.py       # data/case.json and site/src/models/nes-famicom-case-parts.mjs: the case, both consoles (--look draws pictures in out/)
