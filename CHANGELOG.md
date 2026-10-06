# Changelog

## [0.4.0-fork.9] - 2026-10-05

### Added

* `GraphOptions.SupportsTransitionWires` (new flag, default off). Graph types that opt in route
  execution-flow wires between regular nodes through transition supports instead of plain wires, so
  the transition inspector — transitions, conditions, and nested AND/OR condition groups — becomes
  available on ordinary execution links. A wire created this way starts with exactly one default
  transition; rewiring or reusing an existing wire never adds a second one. The flag is exposed
  through `GraphModelImp.SupportsTransitionWires` (mirroring `AllowSubgraphCreation`) and consumed
  by `GraphModelImp.GetWireType`; both endpoints must be execution-flow ports.
* Internal condition-type registry on `GraphModel` (`RegisterConditionType`,
  `UnregisterConditionType`, `ClearConditionTypes`) consumed by `GetAddConditionOptions()`. The
  default "Add Group Condition" entry is preserved and listed first. Registration is scoped to the
  graph-model instance and is not serialized: consumers re-register after a domain reload (for
  example from `Graph.OnEnable`).
* Internal `Graph.GetImplementationModel()` accessor and `InternalsVisibleTo` grants for the
  `Oddlock.Behavior.Editor` consumer, so friend tooling can enumerate wires, transitions, and
  conditions without reflection.
* Non-creating `TransitionModel.ConditionModelOrNull` accessor for read-only inspection. It returns
  null instead of lazily creating and registering an empty root condition group, so importers,
  validators, and indicators can enumerate transitions without dirtying the graph.
* Condition indicator on transition views. A transition support whose transition has authored
  conditions gets the `ge-transition--conditioned` USS class and shows a compact badge
  (`ge-transition__condition-badge`) at the middle of the link. The state is computed with the
  non-creating probe (a root group without sub-conditions is not "conditioned"), and the view
  registers model dependencies on the condition roots so adding or removing a condition updates the
  indicator live.

### Fixed

* Extension-method factory dispatch order. A factory registered for a base model type could shadow
  an exact-model factory once the base factory had been resolved for the same view domain: the
  plain `WireModel` lookup cached `CreateWire` for the derived graph-view type, which then matched
  `TransitionSupportModel` through its base type and silently degraded transition wires to `Wire`
  views (losing the transition inspector). Resolved lookups are now cached separately from declared
  factories and are reused only for the exact (view domain, model type) pair, so exact-model
  factories always win.
* Transition views between regular execution ports now position from the connected port views
  instead of requiring `State` views. Endpoints resolve to the port centers, so the link renders
  between the two nodes and selection/hit-testing lands on the drawn link instead of the content
  origin; state-anchored transitions are unchanged. Transition views also follow port geometry like
  wires do, and the `TransitionControl` path math is extracted into static helpers
  (`GetAnchorDirection`, `GetRenderPoints`) so the geometry is unit-testable.
* `GraphViewDebugAccess.TryGetWireView`, `HighlightWire`, `ResetWireHighlight`, and
  `ClearWireHighlights` now resolve and highlight transition-support wires (`Transition` views) in
  addition to plain wires. Highlighting covers `TransitionControl` and `TransitionArrow`; matching
  is direction-aware, so the two opposite links of a loop resolve distinctly. Plain-wire behavior
  is unchanged.

## [0.4.0-fork.8] - 2026-10-01

### Added

* Node rename via the editor UI. User node, context node, and block models now expose the
  `Renamable` capability and implement `IRenamable` (`Rename` stores the per-instance title), so
  the right-click **Rename** action and inline title editing are available for every user node.
  Previously node models had no `Renamable` capability (plain `AbstractNodeModel` defaults do not
  include it), so the rename affordances never appeared even though per-instance titles were
  honored (0.4.0-fork.6).

## [0.4.0-fork.7] - 2026-10-01

### Changed

* Loop-back wires render **dashed with straight lead-ins and a smoothly bowed curve**. A wire is a
  back edge when following it returns to a node still on the current depth-first path (an ancestor
  of its source) — classic back-edge classification, so only the returning leg of a loop is styled,
  not every wire in a cycle. Back edges run straight for 44 units out of each port (exactly the
  always-visible port window), then bow through two C1-joined cubics (bow clamped 48–180, handle
  40–160 local units) — the first handle is collinear with the lead-in, so there is no corner where
  the straight piece ends. Detected generically per draw (roots = nodes without incoming wires), so
  any graph benefits.
* Every wire renders a short always-visible window at each of its ports in a top-level overlay
  (`wire-end-caps`, above the node layer). The window is the wire's own flattened path trimmed to
  44 graph units from the port and redrawn in content coordinates, so it overlays the wire exactly
  — same curve, width, color, and dash pattern; where the wire is already visible, the window is
  invisible. Where a node covers the wire, a fraction of it stays visible at the arrows. The caps
  are absolutely positioned (relative flow would stack them) and follow wire color changes
  (selection, amber debug highlight).

### Fixed

* Wire hit-testing follows the flattened drawn path (the same geometry that is stroked) instead of
  the straight polyline through the control points. A bowed back edge arcs far away from that
  polyline, which made the visible dashed line unclickable.

## [0.4.0-fork.6] - 2026-10-01

### Fixed

* Per-instance node titles are now honored. User node/block/context models report the serialized
  per-instance title (`AbstractNodeModel.m_Title`, set by the title editor or by tools) when one is
  present, falling back to `Node.DefaultTitle` (the type's designer-facing default). Previously the
  generated `Title` override returned `DefaultTitle` unconditionally, so renaming a node in the
  editor (or programmatically) appeared to do nothing and was lost on reload.

## [0.4.0-fork.5] - 2026-10-01

### Fixed

* Completed the space-partitioning fix from `0.4.0-fork.4`, which guarded only one write path and
  could still throw `IndexOutOfRangeException` from `BoundingBoxKdTreePartitioning.RemoveAndRebuild`
  and `UpdateAndRebuild`:
  - `AddOrUpdateElements` now de-duplicates input keys before use. A container change recorded
    twice for the same target container produced a `[W, W]` list, which built a kd-tree with
    duplicate nodes; later rebuilds sized arrays from the unique-key count and overflowed.
  - `RemoveAndRebuild` counts and filters unique keys, so duplicated removal entries no longer
    size the rebuilt array too small.
  - `GraphView.UpdateSpacePartitioning` no longer adds a moved non-placemat element twice when the
    recorded target container equals the element's current parent (the normal case).

## [0.4.0-fork.4] - 2026-10-01

### Changed

* Selected wires now render **in front of nodes**. Wires rest in layer -1 (below the node layer),
  so the existing last-selected `BringToFront` call only reordered wires among themselves and a
  selected wire could stay hidden under a node. While selected, the wire view moves to a top-most
  layer (`int.MaxValue - 1`, just below the wire-drag candidate) and returns to its resting layer
  when deselected.

### Fixed

* `IndexOutOfRangeException` in `BoundingBoxKdTreePartitioning.UpdateAndRebuild` when an element's
  container changes (selecting a wire moves it to the front layer). `GraphView.UpdateSpacePartitioning`
  can enqueue the same element twice for the same target container, and the rebuild wrote every
  duplicate into an array sized from unique keys. Duplicate keys are now skipped during the write
  pass.

## [0.4.0-fork.3] - 2026-10-01

### Added

* `GraphViewDebugAccess` wire-transition debug API for the com.oddlock.behavior editor debugger:
  `TryGetWireView(handle, fromNode, toNode, out view)` resolves the wire connecting two nodes,
  `HighlightWire` tints it amber (matching the node Running highlight), `ResetWireHighlight`
  restores one wire, and `ClearWireHighlights` restores every wire in a window. Together with the
  existing node highlight this makes the active transition path visible while debugging — the
  answer to "which of the two wires between these nodes just fired?" in loop-heavy graphs.

### Fixed

* `GraphModelImp.DeleteWiresBetween(output, input)` never matched direct wires: the `sameOutput`
  test compared `wire.ToPort == output` instead of `wire.FromPort == output`, so a wire between two
  ports fell into the portal-matching branch and was not deleted. The portal path (which relies on
  `sameOutput`) is corrected by the same fix.

## [0.4.0-fork.2] - 2026-10-01

### Added

* `Node.DefaultTitle`: a public virtual property for designer-facing node titles. The default derives a human-readable label from the class name — the conventional `Node`/`Block` suffix is dropped and PascalCase boundaries become spaces (`PatrolActionNode` → `Patrol Action`). Override it to set an explicit title (`"Abort"`, `"Patrol"`, …). User node, block, and context models now report `Node.DefaultTitle` as their `Title`.
* `IPort.GetConnectedPortOrder(IPort)`: returns the 0-based connection order of a connected port (the order shown by the wire-order bubbles on multi-connected output ports), or `-1` when the port is not connected. Documents and makes contractual the ordering already used by `IPort.GetConnectedPorts`, which is now documented as connection-order and serialization-stable.

### Fixed

* Block and context node models now re-bridge `IBlackboardVariableReference` metadata in `OnAfterDeserialize`. Previously only plain user nodes did, so after any save/load (or asset reimport) variable-reference options on **blocks and context nodes** silently fell back to plain text fields — e.g. the Abort node's `ConditionVariable` option never showed its blackboard dropdown.
* Blackboard-variable dropdowns (`VariablePickerDropdown`) no longer truncate long variable names in narrow node bodies (`HasTarget` previously rendered as `HasTa…`). The popup input now has a 120px minimum width and the node grows to fit.

## [0.4.0-fork.1] - 2026-08-13

### Added

* Added a public debug API `GraphViewDebugAccess` for external debug tooling (the com.oddlock.behavior editor debugger, requirements R4.6/R4.8): enumerate the graph windows displaying a given graph, resolve the node view of a given `INode`, and apply debug status highlights via the `behavior-debug-node-running`, `behavior-debug-node-visited`, and `behavior-debug-node-breakpoint` USS classes. Windows are exposed through the public `GraphViewWindowHandle` type — no internal types leak to external assemblies.
* Extended `GraphViewDebugAccess` with a per-node read-only debug-info badge API: `SetNodeDebugInfo` attaches/updates a small `Label` on a node view and `ClearNodeDebugInfo` removes it, styled by the `behavior-debug-node-info` USS class. The badge is cached per node view (via `VisualElement.userData`), never intercepts pointer events, and lives in the node's title part root so it survives the fork's culling/rebuild cycle.

### Fixed

* Styled the `behavior-debug-node-running`, `behavior-debug-node-visited`, and `behavior-debug-node-breakpoint` USS classes in `GraphViewWindow.uss` (border color, border width, and compensating negative margin). Previously the debug highlight classes were applied to node views but had no matching USS rules, so the highlight was invisible.

## [0.4.0-exp.2] - 2025-09-18

### Changed

* The "Create Local Subgraph from Selection" menu item now appears in the contextual (right-click) menu for context nodes.

### Fixed

* Contextual menus previously only displayed menu items related to the graph canvas. Now, contextual menus correctly display menu items relevant to the current context.
* If you use [UseWithContext] on a BlockNode with a ContextNode type, the BlockNode is now compatible with all ContextNodes derived from that ContextNode type as well.
* Fixed null pointer error when loading a graph asset.
* The contextual menu items "Create Opposite Portal", "Revert to Wire", and "Revert All to Wires" now correctly appear in the contextual menus for portals.
* Fixed an exception when an input port and an option have the same name.

## [0.4.0-exp.1] - 2025-09-10

### Added

* Added a new shortcut `Create Local Subgraph from Selection`. Default value: `Ctrl/Cmd + Shift + L`.
* Users can now pan using right click except on macOS.

### Changed

* Style: Removed the space occupied by the hidden port connectors of input ports with a capacity of `None` if all input ports on the node have no capacity.
* Contextual menus now show only items common to the selection, instead of combining all menu items.
* Right-clicking on an empty part of the graph canvas now opens the canvas menu, even when elements are selected.

### Fixed

* Fixed a bug where port types on Block Nodes would not appear in the Create Variable menu in the Blackboard.

## [0.3.0-exp.1] - 2025-08-20

### Added

* Added a shortcut to collapse or expand the selected nodes. Default value: `Ctrl/Cmd + Shift + O`.
* Added a shortcut Extract Contents To Placemat that applies on the selected subgraph. Default value: `Ctrl/Cmd + Shift + U`.
* Added a shortcut to convert the selected wires to portals. Default value: `Ctrl + Shift + P`.
* Added a default shortcut for toggling the Blackboard overlay on and off: B.
* Added a default shortcut for toggling the Graph Inspector overlay on and off: `I`.
* Added a default shortcut for toggling the Minimap overlay on and off: `M`.
* Added a shortcut to delete all wires on a node. Default value: `Ctrl/Cmd + Shift + W`.

### Changed

* Minor tweaks to the layout of the graph inspector
* Node Options are now defined via a builder, similar to ports.
* Node outputs no longer have a dark background around them

### Fixed

* Fixed issue where port fields would not be updated if the port data type changed
* MacOS: Shortcut `Convert Variable and Constant` is set to `Cmd + Shift + T`.
* After creating a read or write graph variable in a subgraph the parent graph's subgraph node's ports will immediately be updated.
* Removed border between inputs and outputs on context nodes
* Fixed some layout issues in context nodes
* In a Graph with two context types, the item library now show the correct block list for each context type.

### Removed

* Node Options can no longer include any attribute. Supported attributes are defined as builder methods (ex: `Delayed()`).
* Node Options no longer have a parameter for `order`, their order is defined by the order they're defined in.
* Additional CSS margin specific to 'execution flow' port labels. We no longer impose semantics (like 'execution flow') on ports.

## [0.2.0-exp.1] - 2025-07-29

### Added
* Set default shortcut to Create Sticky Note: `` Alt + ` ``.
* Set default shortcut for converting Variable node to Constant node (and vice-versa): `Ctrl + Shift + T`.

### Changed
* [Breaking] Inverted logic for `GraphOptions.AutoIncludeNodesFromGraphAssembly` (used as [Flags]). It's now `GraphOptions.DisableAutoInclusionOfNodesFromGraphAssembly` with the default value set to false instead of true. This prevents unintentionally clearing the flag when setting other flags, as it no longer defaults to 1. Update existing usages to explicitly include this flag if needed.
* Importing samples will now check for any missing package dependencies and prompt the user to install them if necessary.
* Reduced padding around visual elements in the Blackboard.

### Fixed
* Fixed the documentation page `Implement node options` where two parameters within `OnDefinePorts()` in one of the code snippets were inverted: `"Port Count"` and `k_PortCountName`.
* Fixed an issue where tab navigation did not work correctly within ContextNodes. Focus now moves sequentially through all visible fields.
* Fixed a visual artifact that appeared when hovering over a Sticky Note with an empty title.
* Fixed missing shortcut indicators for some actions in the Canvas context menu.
* Fixed variable names and colors not displaying correctly on variable instances when reloading the window (applies to Unity 6000.2.0b12 and later).
* Fixed missing visual cue (disc inside the circle) on ports when hovered by the cursor (applies to Unity 6000.2.0b12 and later).
* Fixed a UI issue with option-less and port-less nodes would be shrunk and not display its title (applies to Unity 6000.2.0b12 and later).

### Removed
* Removed the Tooltip field from the Variable quick access settings in the Blackboard panel.

## [0.1.0-exp.1] - 2025-07-15

### Added
* First experimental release of Graph Toolkit (UGTK)
