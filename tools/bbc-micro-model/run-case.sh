#!/usr/bin/env bash
# Runs the BBC Micro outside model's measurements, in order, from the inputs.
#
#   BBC_MODEL_INPUTS=<folder of inputs> tools/bbc-micro-model/run-case.sh
#
# The folder holds the inputs under the names data/sources.json gives (the
# README says how to fetch each by hand), and every script checks an input's
# SHA-256 before it reads it. PYTHON picks the interpreter, for a venv:
#   PYTHON=~/venv-models/bin/python tools/bbc-micro-model/run-case.sh
# A script that crosses one of the plan's STOP thresholds exits 3, and the
# run stops there.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
py="${PYTHON:-python3}"
: "${BBC_MODEL_INPUTS:?set BBC_MODEL_INPUTS to the folder of inputs (see tools/bbc-micro-model/README.md)}"
cd "$here"

"$py" verify.py O1 O2 O3 O4 O5 O8-I K1 P-TE_1757819
"$py" keys.py          # data/keys.json: every K1 keycap mapped to the machine's keys; O1 and O3 registered to K1
"$py" case_parts.py    # data/case.json and site/src/models/bbc-micro-case-parts.mjs: the case top, the triangulation and its check, the rear and its check
