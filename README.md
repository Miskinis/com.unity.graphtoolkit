# Graph Toolkit — Oddlock fork

> **This is a fork of Unity's experimental Graph Toolkit package**, maintained for the
> **Shard-Struck** project (*Ancient Privateers*). It is consumed as a git dependency by that
> project and is not a general-purpose distribution or a registry release. Fork changes are
> additive and tracked as `0.4.0-fork.N` releases in [CHANGELOG.md](CHANGELOG.md); upstream is
> `com.unity.graphtoolkit 0.4.0-exp.2`.

Graph Toolkit provides a framework to build graph editing tools: a graph data model, a UI
foundation, and a graph-to-asset pipeline. It has no functionality immediately available to the
user; if you write a tool that deals with graphs, Graph Toolkit can help you reach your goals more
quickly while adhering to Unity UI/UX standards.

The upstream package is experimental, and its features and documentation might change before it is
verified for release. This fork tracks it with focused, project-driven changes on top — mostly
editor authoring UX, debug tooling, and opt-in support for condition authoring on ordinary
execution wires.

## Fork features

### Node identity and authoring UX

- **`Node.DefaultTitle`** — public virtual designer-facing default titles. The default derives a
  readable label from the class name (`PatrolActionNode` → `Patrol Action`); node classes override
  it for intent titles.
- **Per-instance renames** — user node, context, and block models expose `IRenamable` and honor the
  serialized per-instance title over `DefaultTitle`, so inline title editing and the right-click
  **Rename** action persist across reloads.
- **`Node.Tooltip`** — public virtual hover tooltip reported through `AbstractNodeModel.Tooltip`
  (direct model tooltip → node class tooltip → displayed title fallback), keeping a node's type
  discoverable even when its displayed title does not name it.
- **Blackboard-variable dropdown fixes** — `IBlackboardVariableReference` metadata is re-bridged
  after deserialize on blocks and context nodes, and the picker no longer truncates long variable
  names (120px input floor).
- **`IPort.GetConnectedPortOrder(IPort)`** — contractual, serialization-stable connection order for
  multi-connected output ports.

### Wire rendering and interaction

- **Loop-back wires** — back edges render dashed with straight lead-ins and a smooth bowed curve;
  every wire gets an always-visible window at each port (drawn in a top-level overlay) so links stay
  readable where nodes cover them.
- **Selected wires render in front of nodes**, restoring their resting layer on deselect.
- **Wire hit-testing follows the drawn (flattened) path**, so bowed dashed links are clickable.
- **Space-partitioning crash fixes** (`IndexOutOfRangeException` from duplicate container-change
  keys in add/remove/rebuild paths).

### Debug tooling (`GraphViewDebugAccess`)

- Enumerate graph windows displaying a given graph, resolve node views, and apply debug highlight
  USS classes; per-node read-only debug badge API (`SetNodeDebugInfo` / `ClearNodeDebugInfo`).
- Wire-view resolution and amber highlight (`TryGetWireView`, `HighlightWire`, `ResetWireHighlight`,
  `ClearWireHighlights`) — direction-aware for loop pairs, covering both plain wires and
  transition-support wires.

### Transitions and conditions on execution wires (opt-in)

- **`GraphOptions.SupportsTransitionWires`** (default off) — graph types that opt in route
  execution-flow wires between regular nodes through transition supports, exposing the transition
  inspector (transitions, conditions, nested AND/OR condition groups) on ordinary links. Each
  created wire starts with exactly one default transition.
- **Condition-type registry** (`RegisterConditionType` / `UnregisterConditionType` /
  `ClearConditionTypes`) consumed by the Add-condition menu; scoped to the graph-model instance,
  re-registered by the consumer after domain reload.
- **Non-creating `TransitionModel.ConditionModelOrNull` probe** and a live condition indicator on
  conditioned transition views (`ge-transition--conditioned` + badge).
- **Transition positioning between regular nodes** resolves from port views, so links render
  between the nodes and selection lands on the drawn link.
- **Extension-method factory dispatch order fixed** — exact-model factories always outrank
  base-model factories; previously a cached plain-wire factory could silently degrade transition
  wires to plain wire views.

### Consumer integration (friend access)

- `InternalsVisibleTo("Oddlock.Behavior.Editor")` grants on `Unity.GraphToolkit.Internal.Editor`,
  `Unity.GraphToolkit.Editor`, and `Unity.CommandStateObserver`.
- Internal `Graph.GetImplementationModel()` accessor for enumerating wires, transitions, and
  conditions without reflection.
- `NodeOptionsInspector`'s constructor is `protected`, so consumers can subclass the node options
  section (the consumer prepends a read-only node-type row).

## Fork releases

| Release | Date | Highlights |
| --- | --- | --- |
| `0.4.0-fork.11` | 2026-10-07 | `Node.Tooltip` public API; `NodeOptionsInspector` protected constructor |
| `0.4.0-fork.10` | 2026-10-06 | `CommandStateObserver` friend access (consumer undo path for condition edits) |
| `0.4.0-fork.9` | 2026-10-05 | Transition-wire opt-in, condition registry, condition indicator, dispatch-order + positioning fixes |
| `0.4.0-fork.8` | 2026-10-01 | Editor UI rename (`IRenamable`) |
| `0.4.0-fork.7` | 2026-10-01 | Dashed/bowed loop-back wires, port end windows, wire hit-testing |
| `0.4.0-fork.6` | 2026-10-01 | Per-instance node titles honored |
| `0.4.0-fork.5` | 2026-10-01 | Space-partitioning fix completion |
| `0.4.0-fork.4` | 2026-10-01 | Selected wires in front; partitioning crash fix |
| `0.4.0-fork.3` | 2026-10-01 | Wire debug highlight API; `DeleteWiresBetween` fix |
| `0.4.0-fork.2` | 2026-10-01 | `Node.DefaultTitle`, `IPort.GetConnectedPortOrder`, variable dropdown fixes |
| `0.4.0-fork.1` | 2026-08-13 | `GraphViewDebugAccess` debug API, node badges, highlight USS |

Full text for each release: [CHANGELOG.md](CHANGELOG.md).

## Installation

This fork is consumed as a git dependency. In the consuming project's `Packages/manifest.json`:

```json
"com.unity.graphtoolkit": "https://github.com/Miskinis/com.unity.graphtoolkit.git"
```

Unity resolves the default-branch HEAD and pins the exact commit in `packages-lock.json`. To pick
up new fork changes: commit + push here, then re-resolve packages in the Unity Editor so the lock
file bumps to the new commit.

**Fork workflow used by this repository:** fork changes are live-tested by copying them into the
consuming project's `Library/PackageCache/com.unity.graphtoolkit@<hash>` before committing; after a
push, the package is re-resolved so the lock points at the new commit. Release markers are
`0.4.0-fork.N` in the changelog; `package.json` intentionally keeps the upstream version string
(`0.4.0-exp.2`).

## Compatibility notes

- Editor-only package (Graph Toolkit is an editor framework). `package.json` declares Unity
  `6000.2`; this fork is developed and verified against Unity `6000.3.24f1`.
- Graph assets serialize with `[SerializeReference]` managed references. Renaming assemblies or
  node/condition types, or removing condition types, can orphan authored data — treat public model
  type names as a compatibility surface.
- Upstream documentation: [Graph Toolkit 0.4 manual](https://docs.unity3d.com/Packages/com.unity.graphtoolkit@0.4/manual/index.html).
- License: see [LICENSE.md](LICENSE.md).
