#!/usr/bin/env python3
"""
Check the hand-written UnityEngine/UnityEditor stubs against Unity's real C# source.

api-surface.txt covers packages. This covers the engine itself — the other half of the
same problem, and the half that produced the Arial bug: Resources.GetBuiltinResource
exists, compiles, and returns null on Unity 6, and no amount of checking my own stub
would ever have said so.

For every member the stubs declare, this asks Unity's source three questions:
  does it exist at all, is it static where the stub says it is, and is it settable where
  the stub makes it settable. Each mismatch is a compile error waiting on the far side of
  a push, or worse, a silent behavioural difference.

  python3 stubcheck.py             non-zero exit if any stub member is mis-declared or
                                   does not exist in Unity at all
"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
STUBS = os.path.join(HERE, '..', 'stubs')
UNITY = '/tmp/ucs'

# uGUI ships as a package, so its source is not in UnityCsReference — but the port's entire
# UI is uGUI, so leaving it out would mean the least-verified code was also the most used.
UGUI = os.path.join(HERE, '.cache', 'com.unity.ugui')

# ...and neither is anything else the port imports. verify.py already clones these to
# check the 67 pinned signatures; scanning them here too is free, and it is the
# difference between "21 members unaccounted for" and a clean answer.
CACHE = os.path.join(HERE, '.cache')

BLOCK = re.compile(r'/\*.*?\*/', re.S)
TYPE = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*'
    r'(?:public|internal|protected|private|static|sealed|abstract|partial|unsafe|readonly|ref|new|\s)*'
    r'\b(class|struct|interface|enum)\s+(\w+)')
MEMBER = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*'
    # `ref\s` is here because Transport returns `ref NetworkSettings` from its extension
    # methods, and without it those declarations parse as nothing at all.
    r'((?:public|protected|internal|static|extern|virtual|override|readonly|sealed|new|abstract|unsafe|const|ref\s|\s)+)'
    r'([\w<>\[\],\.\?]+\s+)?(\w+)(?:<[\w\s,]+>)?\s*(\(|\{|=>|;|=[^=]|$)')


def scrub(text):
    """Strings and comments in one pass — see verify.py for why this cannot be two."""
    out, i, n = [], 0, len(text)
    while i < n:
        c, nxt = text[i], text[i + 1] if i + 1 < n else ''
        if c == '/' and nxt == '/':
            while i < n and text[i] != '\n': i += 1
        elif c == '/' and nxt == '*':
            i += 2
            while i < n - 1 and not (text[i] == '*' and text[i + 1] == '/'):
                if text[i] == '\n': out.append('\n')
                i += 1
            i += 2
        elif c == '@' and nxt == '"':
            i += 2
            while i < n:
                if text[i] == '"':
                    if i + 1 < n and text[i + 1] == '"': i += 2; continue
                    i += 1; break
                if text[i] == '\n': out.append('\n')
                i += 1
            out.append('""')
        elif c in '"\'':
            q = c; i += 1
            while i < n and text[i] != q: i += 2 if text[i] == '\\' else 1
            i += 1
            out.append('""')
        else:
            out.append(c); i += 1
    return ''.join(out)


def scan(paths):
    """type -> {member: {'static': bool, 'settable': bool}}"""
    api = {}
    for path in paths:
        try:
            text = scrub(open(path, encoding='utf-8', errors='ignore').read())
        except OSError:
            continue
        scopes, depth, pending = [], 0, None
        lines = text.splitlines()
        for idx, line in enumerate(lines):
            m = TYPE.match(line)
            if m:
                pending = m.group(2)
            elif scopes:
                mm = MEMBER.match(line)
                if mm and mm.group(4) == '':
                    # Unity writes plenty of properties with the brace on the next line
                    # (`public extern static int touchCount` / newline / `{`). Without this
                    # the scanner simply cannot see them, and "not found" reads as drift.
                    nxt = next((l.strip() for l in lines[idx + 1:idx + 3] if l.strip()), '')
                    if not (nxt.startswith('{') or nxt.startswith('=>')):
                        mm = None
                if mm:
                    mods, name = mm.group(1), mm.group(3)
                    if 'public' in mods or 'protected' in mods:
                        # A property is settable if a `set` accessor appears in its body.
                        # Unity's real properties run a dozen lines, so a fixed peek-ahead
                        # window silently reports half the setters in the engine as missing.
                        # Walk the actual block instead.
                        tail = property_body(lines, idx)
                        entry = api.setdefault(scopes[-1][0], {}).setdefault(
                            name, {'static': False, 'instance': False, 'settable': False})
                        # A name can be declared more than once — overloads, and partial
                        # types that a whole other file also contributes to. Record every
                        # form seen rather than letting the last one win: Vector3.Normalize
                        # is genuinely both, and calling that drift is just noise.
                        if 'static' in mods: entry['static'] = True
                        else: entry['instance'] = True
                        if re.search(r'\bset\s*[;{=]|\bset\b', tail) or mm.group(4).startswith('='):
                            entry['settable'] = True
                        if mm.group(4) == '(':          # methods count as "settable" never
                            entry['method'] = True
            opens, closes = line.count('{'), line.count('}')
            if pending and opens:
                # Remember the brace depth the type opened at. Comparing against
                # len(scopes) instead looks right and is wrong the moment a namespace
                # (or any nested type) is in play: the enum at the top of Toggle.cs
                # never popped, so every member of Toggle was filed under
                # Toggle.ToggleTransition and the whole class read as "not found".
                scopes.append((pending, depth + 1)); pending = None
            depth += opens - closes
            while scopes and depth < scopes[-1][1]:
                scopes.pop()
    return api


def property_body(lines, idx):
    """The text of a property's accessor block, or just its own line for anything else."""
    depth, started, out = 0, False, []
    for line in lines[idx:idx + 60]:
        out.append(line)
        depth += line.count('{') - line.count('}')
        if '{' in line: started = True
        if started and depth <= 0:
            break
    return ' '.join(out)


def files(root, *skip):
    for dirpath, _, names in os.walk(root):
        if any(s in dirpath for s in skip): continue
        for n in names:
            if n.endswith('.cs'):
                yield os.path.join(dirpath, n)


def main():
    if not os.path.isdir(UNITY):
        print('Unity source not found at ' + UNITY)
        print('  git clone --depth 1 --branch 6000.3 --filter=blob:none --sparse \\')
        print('      https://github.com/Unity-Technologies/UnityCsReference ' + UNITY)
        sys.exit(2)

    print('scanning Unity 6.3 source...')
    sources = list(files(UNITY, '/Tests/', '/Tools/'))
    pkgs = sorted(d for d in os.listdir(CACHE) if d.startswith('com.unity.')) \
        if os.path.isdir(CACHE) else []
    for d in pkgs:
        sources += list(files(os.path.join(CACHE, d), '/Tests/', '/Samples'))
    if pkgs:
        print('  (including ' + ', '.join(pkgs) + ')')
    real = scan(sources)
    print(f'  {len(real)} types')
    print('scanning stubs...')
    mine = scan(files(STUBS))
    print(f'  {len(mine)} types\n')

    drift, unknown = [], []
    for ty, members in sorted(mine.items()):
        if ty not in real:
            unknown.append(f'{ty} (whole type not found)')
            continue
        for name, info in sorted(members.items()):
            if name == ty:                       # constructors
                continue
            r = real[ty].get(name)
            if r is None:
                unknown.append(f'{ty}.{name}')
                continue
            want = 'static' if info['static'] else 'instance'
            if not r[want]:
                have = '/'.join(k for k in ('static', 'instance') if r[k]) or 'neither'
                drift.append(f'{ty}.{name}: stub says {want}, Unity says {have}')
            elif info['settable'] and not r['settable'] and not r.get('method'):
                drift.append(f'{ty}.{name}: stub is settable, Unity is read-only')

    if drift:
        print(f'DRIFT — {len(drift)} member(s) declared differently from Unity:')
        for d in drift: print('  ' + d)
    else:
        print('No static/settability drift in any stub member Unity also declares.')

    if unknown:
        print(f'\nNOT FOUND — {len(unknown)} stub member(s) Unity does not declare:')
        for u in unknown: print('  ' + u)
    else:
        print('Every stub member resolves to a real declaration.')

    # A member the stubs invent is not a lesser problem than one they mis-declare: it is
    # how ToRelayServerData compiled offline four times running. Both fail the check.
    sys.exit(1 if (drift or unknown) else 0)


if __name__ == '__main__':
    main()
