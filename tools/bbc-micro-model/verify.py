"""Checks the inputs in BBC_MODEL_INPUTS against data/sources.json.

    BBC_MODEL_INPUTS=<folder> python verify.py           # every input
    BBC_MODEL_INPUTS=<folder> python verify.py I1 I2     # just these

Prints one line per input: present (its hash is the recorded one), missing,
or hash differs. Exits 1 when any hash differs, or when an input named on the
command line is missing or unknown; an input that is merely not fetched yet is
reported and is not a failure unless it was asked for. Nothing is fetched.
"""
import sys

import common


def main(argv):
    folder = common.inputs_dir()
    wanted = argv[1:]
    known = {s['id']: s for s in common.sources()}
    failed = False
    for sid in wanted:
        if sid not in known:
            print(f'{sid}: not in data/sources.json')
            failed = True
    for sid, s in known.items():
        if wanted and sid not in wanted or not s.get('file'):
            continue
        path = folder / s['file']
        if not path.is_file():
            print(f'{sid}: missing ({s["file"]})')
            failed = failed or bool(wanted)
            continue
        got = common.sha256(path)
        if got == s['sha256']:
            print(f'{sid}: present, SHA-256 matches ({s["file"]})')
        else:
            print(f'{sid}: hash differs: {got}, recorded {s["sha256"]} ({s["file"]})')
            failed = True
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
