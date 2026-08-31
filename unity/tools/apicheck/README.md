# Package API checker

The port is type-checked offline against hand-written Unity stubs in `../stubs`. That
works for `UnityEngine` itself, which is stable and well known. It does **not** work for
Unity's packages, and one evening made the reason concrete.

Four consecutive builds failed on the same shape of mistake:

| What the code assumed | What the package actually does |
| --- | --- |
| `NetworkEndPoint` | renamed to `NetworkEndpoint` in Transport 2 |
| `DataStreamReader` in `Unity.Networking.Transport` | moved to `Unity.Collections` |
| `WithFragmentationStageParameters` on `NetworkSettings` | extension method in `Unity.Networking.Transport.Utilities` |
| `allocation.ToRelayServerData("udp")` | does not exist; `new RelayServerData(allocation, "udp")` does |

Every one compiled offline. The stub had been written from the same memory as the code,
so it agreed with the code's mistakes. **A stub cannot catch its author being confidently
wrong** — only the real source can.

## What this does

`api-surface.txt` lists every package member the port depends on. `verify.py` checks each
claim against the package's actual source, at the versions `Packages/manifest.json` pins,
fetched from Unity's public mirror.

```sh
python3 verify.py --fetch     # clone the pinned package sources into .cache/
python3 verify.py             # check every claim; non-zero exit if any is wrong
```

Anything added to the port that touches a package goes in `api-surface.txt`. If the
manifest's versions change, re-fetch and re-run: that is the moment a moved API is
cheapest to find.

## What it is not

`verify.py` contains a small C# scanner, not a compiler. It tracks brace depth so it can
say which type a member belongs to, and it scrubs comments and strings in a single pass —
because doing those as separate regex passes destroys any string containing `//`, and
Unity's sources are full of `[HelpURL("https://...")]`. That bug made the checker's first
run report `XROrigin` as not existing.

It answers "does this member exist here, spelled this way". It does not check parameter
types or overload resolution. Those still land on the stubs and on Unity itself.

## The other half: `stubcheck.py`

`verify.py` checks what the port claims about *packages*. `stubcheck.py` checks what the
*stubs* claim about everything — the engine included — by scanning Unity's own C# source
plus the same package clones.

```sh
git clone --depth 1 --branch 6000.3 --filter=blob:none --sparse \
    https://github.com/Unity-Technologies/UnityCsReference /tmp/ucs
git -C /tmp/ucs sparse-checkout set Runtime Editor Modules
python3 verify.py --fetch     # also fetches com.unity.ugui, which stubcheck needs
python3 stubcheck.py          # non-zero exit on any drift
```

For every member the stubs declare it asks three questions: does Unity declare it at all,
is it static where the stub says it is, and is it settable where the stub makes it
settable. Anything the stubs invented is as much a failure as anything they mis-declared —
inventing `Allocation.ToRelayServerData` is precisely how four builds compiled offline and
died in the editor.

It found four invented members, now deleted:

| Stub claimed | Reality |
| --- | --- |
| `Allocation.ToRelayServerData(string)` | never existed; `new RelayServerData(a, type)` |
| `JoinAllocation.ToRelayServerData(string)` | same |
| `Component.AddComponent<T>()` | `GameObject` has it; `Component` does not |
| `MonoBehaviour.FindObjectOfType<T>()` | lives on `Object`, and is superseded on Unity 6 by `FindFirstObjectByType` |

It also renamed the stubs' merged `PipelineParameterExtensions` to the two real classes,
`FragmentationStageParameterExtensions` and `ReliableStageParameterExtensions`, so the
names now match what `api-surface.txt` already pinned.

Removing all four left the offline build green, which is the useful part of the result:
nothing in the port was leaning on them. They were loaded guns pointing at the next commit.

### What its scanner cannot do

The same caveat as `verify.py` — existence and spelling, not parameter types or overload
resolution — plus a lesson worth recording. This check first reported 72 members as
"not found", and every one was the scanner's fault, not the stubs':

- it tracked type scope by comparing brace depth against `len(scopes)`, which is off by
  one inside a `namespace`, so a nested enum never popped and every member after it was
  filed under the wrong type;
- it required a property's `{` on the declaration line, which Unity mostly does not do;
- it looked three lines ahead for a `set` accessor, in bodies that are routinely twelve;
- it could not parse generic methods, or `ref` returns.

All four are fixed, and the count is now zero. A checker with poor recall is worse than
none: it trains you to read "not found" as noise, which is what "not found" must never mean.
