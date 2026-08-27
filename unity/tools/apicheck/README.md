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
