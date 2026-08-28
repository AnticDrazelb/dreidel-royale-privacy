#!/usr/bin/env python3
"""
Check every package API this port depends on against the package's real source.

The port is type-checked offline against hand-written Unity stubs (../stubs), which
is fine for UnityEngine itself but useless for packages: a stub written from the same
memory as the code will agree with the code's mistakes. This reads the actual source
Unity compiles against, so it disagrees.

  python3 verify.py --fetch     clone the pinned package versions into .cache/
  python3 verify.py             check api-surface.txt against them

Versions come from ../../DreidelRoyale/Packages/manifest.json, so the check is always
against what the project actually pins.
"""
import argparse, json, os, re, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, '.cache')
MANIFEST = os.path.join(HERE, '..', '..', 'DreidelRoyale', 'Packages', 'manifest.json')
SURFACE = os.path.join(HERE, 'api-surface.txt')
MIRROR = 'https://github.com/needle-mirror/{pkg}'

# Packages the manifest does not name directly, but that are pulled in as dependencies
# and whose types the port uses by name.
TRANSITIVE = {
    'com.unity.collections': '2.2.1',      # Transport 2.4's DataStream types
    'com.unity.xr.core-utils': '2.3.0',    # AR Foundation 6's XROrigin
}


def pinned_versions():
    with open(MANIFEST) as f:
        deps = json.load(f)['dependencies']
    versions = {k: v for k, v in deps.items() if not k.startswith('com.unity.modules')}
    versions.update({k: v for k, v in TRANSITIVE.items() if k not in versions})
    return versions


def fetch(versions):
    os.makedirs(CACHE, exist_ok=True)
    ok = True
    for pkg, ver in sorted(versions.items()):
        dest = os.path.join(CACHE, pkg)
        if os.path.isdir(dest):
            print(f'  cached   {pkg}@{ver}')
            continue
        r = subprocess.run(['git', 'clone', '-q', '--depth', '1', '--branch', ver,
                            MIRROR.format(pkg=pkg), dest],
                           capture_output=True, text=True)
        if r.returncode == 0:
            print(f'  fetched  {pkg}@{ver}')
        else:
            print(f'  MISSING  {pkg}@{ver}  (no such version on the mirror)')
            ok = False
    return ok


def parse_surface():
    rows = []
    with open(SURFACE) as f:
        for n, line in enumerate(f, 1):
            line = line.split('#')[0].strip()
            if not line:
                continue
            parts = [p.strip() for p in line.split('|')]
            if len(parts) == 4:
                parts.append('')                     # no signature demanded
            if len(parts) != 5:
                print(f'  api-surface.txt:{n}: malformed line')
                sys.exit(2)
            rows.append((n, *parts))
    return rows


def sources(pkg):
    root = os.path.join(CACHE, pkg)
    for dirpath, _, names in os.walk(root):
        for name in names:
            if name.endswith('.cs'):
                yield os.path.join(dirpath, name)


_cache = {}

# ---------------------------------------------------------------------------
#  A small C# scanner.
#
#  Line-matching alone cannot answer "which type is this member on?", which is
#  the only question that matters here. So this tracks brace depth and keeps a
#  scope stack, and attributes each declaration to the type it is actually
#  inside. It is not a compiler and does not need to be: it needs to be right
#  about where a public member lives.
# ---------------------------------------------------------------------------
NS = re.compile(r'^\s*namespace\s+([\w.]+)\s*[;{]?\s*$')
TYPE = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*'
    r'(?:public|internal|protected|private|static|sealed|abstract|partial|unsafe|readonly|ref|new|\s)*'
    r'\b(class|struct|interface|enum)\s+(\w+)')
ACCESS = re.compile(r'^\s*(?:public|protected)\b')
ATTRIBUTE = re.compile(r'^\s*\[[^\]]*\]\s*')
GENERIC = re.compile(r'<[^<>]*>')


def _scrub(text):
    """
    Blank out comments and string contents in one pass.

    It has to be one pass. Stripping comments first with a regex destroys any
    string containing "//" — and Unity's sources are full of [HelpURL("https://...")]
    attributes, which then leave an unterminated quote that swallows the class
    declaration on the next line. That is not a hypothetical: it is why this
    checker first reported XROrigin as not existing.
    """
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ''
        if c == '/' and nxt == '/':
            while i < n and text[i] != '\n':
                i += 1
        elif c == '/' and nxt == '*':
            i += 2
            while i < n - 1 and not (text[i] == '*' and text[i + 1] == '/'):
                if text[i] == '\n':
                    out.append('\n')
                i += 1
            i += 2
        elif c == '@' and nxt == '"':                 # verbatim string
            i += 2
            while i < n:
                if text[i] == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if text[i] == '\n':
                    out.append('\n')
                i += 1
            out.append('""')
        elif c in '"\'':                              # regular string or char
            quote = c
            i += 1
            while i < n and text[i] != quote:
                i += 2 if text[i] == '\\' else 1
            i += 1
            out.append('""')
        else:
            out.append(c)
            i += 1
    return ''.join(out)


def _member_name(line):
    """The declared identifier on a public member line, or None."""
    line = ATTRIBUTE.sub('', line).strip()
    if not line or line.startswith('//'):
        return None
    # a constructor/method/property/field ends in one of these
    body = line.split('=>')[0]
    while GENERIC.search(body):                       # Foo<T, U> -> Foo
        body = GENERIC.sub('', body)
    m = re.search(r'\b(\w+)\s*\(', body)             # method or constructor
    if m:
        return m.group(1)
    m = re.search(r'\b(\w+)\s*(?:\{|;|=[^=])', body)  # property or field
    if m:
        return m.group(1)
    m = re.search(r'\b(\w+)\s*$', body)              # property on the next line
    return m.group(1) if m else None


def declarations(pkg):
    """type name -> namespaces it is declared in; type name -> its public members."""
    if pkg in _cache:
        return _cache[pkg]
    types, members, sigs = {}, {}, {}

    for path in sources(pkg):
        try:
            text = _scrub(open(path, encoding='utf-8', errors='ignore').read())
        except OSError:
            continue

        scopes = []          # (kind, name, depth_at_open, type_kind)
        depth = 0
        pending = None       # a type declared but whose { has not been seen yet
        for raw in text.splitlines():
            line = raw.rstrip()

            m = NS.match(line)
            if m:
                pending = ('ns', m.group(1), '')
            else:
                m = TYPE.match(line)
                if m:
                    name = m.group(2)
                    ns = '.'.join(n for k, n, _, _kind in scopes if k == 'ns')
                    types.setdefault(name, set()).add(ns)
                    pending = ('type', name, m.group(1))
                elif scopes and scopes[-1][0] == 'type':
                    # Interface members are public without saying so, so inside one
                    # every declaration counts; elsewhere the modifier is required.
                    if ACCESS.match(line) or scopes[-1][3] == 'interface':
                        name = _member_name(line)
                        if name:
                            members.setdefault(scopes[-1][1], set()).add(name)
                            sigs.setdefault((scopes[-1][1], name), []).append(
                                ' '.join(line.split()))

            opens = line.count('{')
            closes = line.count('}')
            if pending and opens:
                scopes.append((pending[0], pending[1], depth, pending[2]))
                pending = None
            depth += opens - closes
            while scopes and depth <= scopes[-1][2]:
                scopes.pop()

    _cache[pkg] = (types, members, sigs)
    return _cache[pkg]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--fetch', action='store_true')
    args = ap.parse_args()

    versions = pinned_versions()
    if args.fetch:
        print('Fetching pinned package sources:')
        sys.exit(0 if fetch(versions) else 1)

    if not os.path.isdir(CACHE):
        print('No package sources cached. Run:  python3 verify.py --fetch')
        sys.exit(2)

    failures = []
    for lineno, pkg, ns, ty, member, want_sig in parse_surface():
        if not os.path.isdir(os.path.join(CACHE, pkg)):
            failures.append((lineno, f'{pkg} not fetched'))
            continue
        types, members, sigs = declarations(pkg)

        if ty not in types:
            failures.append((lineno, f'{pkg}: type {ty} does not exist'))
            continue
        if ns not in types[ty]:
            found = ', '.join(sorted(n for n in types[ty] if n)) or '(global)'
            failures.append((lineno, f'{pkg}: {ty} is in {found}, not {ns}'))
            continue
        name = ty if member == '.ctor' else member
        if member:
            if name not in members.get(ty, set()):
                what = 'public constructor' if member == '.ctor' else f'member {member}'
                failures.append((lineno, f'{pkg}: {ns}.{ty} has no {what}'))
                continue

        # A member existing is not the same as it having the shape the port calls.
        # Both bugs this checker was extended to catch - a Button bound where the
        # driver reads an int, and a Connect() overload that no longer exists -
        # passed a name check and failed at runtime.
        if want_sig:
            found = sigs.get((ty, name), [])
            norm = ' '.join(want_sig.split())
            if not any(norm in ' '.join(f.split()) for f in found):
                shown = found[0][:96] if found else '(no declaration captured)'
                failures.append((lineno,
                    f'{pkg}: {ns}.{ty}.{name} does not match\n'
                    f'{"":>26}wanted: {norm}\n'
                    f'{"":>26}actual: {shown}'))

    checked = len(parse_surface())
    if failures:
        print(f'{len(failures)} of {checked} API claims are WRONG:\n')
        for lineno, msg in failures:
            print(f'  api-surface.txt:{lineno}  {msg}')
        sys.exit(1)
    print(f'All {checked} package API claims verified against the real sources.')


if __name__ == '__main__':
    main()
