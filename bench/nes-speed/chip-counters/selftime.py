"""Self-time share per function from DevTools profiles: python3 selftime.py <name>=<profile.json>..."""
import json, sys, collections
cols = []
for arg in sys.argv[1:]:
    name, path = arg.split('=', 1)
    p = json.load(open(path))
    byid = {n['id']: n['callFrame']['functionName'] or '(anon)' for n in p['nodes']}
    c = collections.Counter(byid[s] for s in p['samples'])
    tot = sum(c.values())
    cols.append((name, {k: 100 * v / tot for k, v in c.items()}, tot))
keys = sorted(set().union(*[c for _, c, _ in cols]), key=lambda k: -max(c.get(k, 0) for _, c, _ in cols))
short = lambda k: k.replace('Dbhq_Machines_Nes_Dbhq_Machines_Nes_', '').replace('Dbhq_Cpu6502_Dbhq_Cpu6502_', '')[:40]
print(f"{'function':40} " + ' '.join(f'{n:>9}' for n, _, _ in cols))
print(f"{'samples':40} " + ' '.join(f'{t:>9}' for _, _, t in cols))
for k in keys[:22]:
    print(f'{short(k):40} ' + ' '.join(f'{c.get(k, 0):9.2f}' for _, c, _ in cols))
for k in keys:
    if 'Note' in k or 'ChipAt' in k or 'ChipAccesses' in k:
        print(f'{short(k):40} ' + ' '.join(f'{c.get(k, 0):9.2f}' for _, c, _ in cols))
