#!/usr/bin/env bash
# Runs the BBC Micro inside model's measurements, in order, from the inputs.
#
#   BBC_MODEL_INPUTS=<folder of inputs> tools/bbc-micro-model/run-board.sh
#
# The folder holds the inputs under the names data/sources.json gives (the
# README says how to fetch each by hand), and every script checks an input's
# SHA-256 before it reads it. PYTHON picks the interpreter, for a venv:
#   PYTHON=/tmp/bbcvenv/bin/python tools/bbc-micro-model/run-board.sh
# A script that crosses one of the plan's STOP thresholds exits 3, and the
# run stops there.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
py="${PYTHON:-python3}"
: "${BBC_MODEL_INPUTS:?set BBC_MODEL_INPUTS to the folder of inputs (see tools/bbc-micro-model/README.md)}"
cd "$here"

"$py" verify.py I1 I2
"$py" board_frame.py        # data/frame.json: the scan's scale, turn, outline and holes
"$py" board_register.py     # data/registration.json: the solder side registered; pads, drills and footprints
