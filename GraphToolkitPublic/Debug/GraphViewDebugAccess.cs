using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.GraphToolkit.Editor
{
    /// <summary>
    /// Visual status tracked by an external debugger for a node in a graph window.
    /// </summary>
    /// <remarks>
    /// Used by editor debug tooling (e.g. the com.oddlock.behavior editor debugger) to tint node
    /// views with a single status at a time via <see cref="GraphViewDebugAccess.HighlightNode"/>.
    /// </remarks>
    public enum DebugNodeStatus
    {
        /// <summary>
        /// The node is currently executing.
        /// </summary>
        Running,

        /// <summary>
        /// The node has executed at least once during this session.
        /// </summary>
        Visited,

        /// <summary>
        /// The node is marked as a debug breakpoint.
        /// </summary>
        Breakpoint
    }

    /// <summary>
    /// Opaque handle to an open graph window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GraphViewEditorWindow</c> is internal to <c>Unity.GraphToolkit.Internal.Editor</c> and cannot
    /// appear in this public API. The handle hides the window behind an internal property, so external
    /// assemblies (such as <c>Oddlock.Behavior.Editor</c>) can hold and pass window references without
    /// needing InternalsVisibleTo — the same bridge pattern <c>PublicGraphFactory</c> uses to reach
    /// <c>GraphViewEditorWindowImp</c>.
    /// </para>
    /// <para>
    /// Handles are only produced and consumed by <see cref="GraphViewDebugAccess"/>; external callers
    /// treat them as opaque tokens. A handle may point to a window that was closed or destroyed after
    /// the handle was obtained — every API taking a handle re-validates it and degrades gracefully.
    /// </para>
    /// </remarks>
    public sealed class GraphViewWindowHandle
    {
        internal GraphViewEditorWindow Window { get; }

        internal GraphViewWindowHandle(GraphViewEditorWindow window)
        {
            Window = window;
        }
    }

    /// <summary>
    /// Public debug access to the Graph Toolkit graph windows: enumerate the windows displaying a given
    /// graph, resolve the node view of a given <see cref="INode"/>, and tint node views with debug status
    /// USS classes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This API exists so external editor debuggers can target graph windows deterministically — by graph
    /// identity, never by "the active window", which would highlight the wrong graph when an unrelated
    /// window happens to be focused. It is observation-only: it never modifies graph models, only USS
    /// classes on the visuals.
    /// </para>
    /// <para>
    /// All methods are null-safe. Windows may be closed, destroyed, or reloaded mid-session; lookups
    /// re-validate their inputs on every call and return empty results instead of throwing, so debug
    /// polling loops can call this API every tick without try/catch.
    /// </para>
    /// </remarks>
    // JUSTIFIED: static — Editor-only #if UNITY_EDITOR helper, zero mutable state
    public static class GraphViewDebugAccess
    {
        /// <summary>
        /// USS class added to a node view that is currently executing.
        /// </summary>
        public const string runningUssClassName = "behavior-debug-node-running";

        /// <summary>
        /// USS class added to a node view that has been visited during this session.
        /// </summary>
        public const string visitedUssClassName = "behavior-debug-node-visited";

        /// <summary>
        /// USS class added to a node view marked as a debug breakpoint.
        /// </summary>
        public const string breakpointUssClassName = "behavior-debug-node-breakpoint";

        /// <summary>
        /// USS class added to the read-only debug-info badge label attached to a node view by
        /// <see cref="SetNodeDebugInfo"/>.
        /// </summary>
        public const string debugInfoUssClassName = "behavior-debug-node-info";

        /// <summary>
        /// Color used by <see cref="HighlightWire"/> for the active debug transition.
        /// Amber, matching the node Running highlight so the two read as one language.
        /// </summary>
        static readonly Color k_ActiveWireColor = new Color(1f, 0.69f, 0f);

        /// <summary>
        /// Returns handles for all open graph windows whose current graph object is the one backing
        /// <paramref name="graph"/>.
        /// </summary>
        /// <param name="graph">
        /// The graph to look for, as returned by <see cref="GraphDatabase.LoadGraph{T}"/>. The public
        /// <see cref="Graph"/> type is the API-visible representation of the internal graph object; its
        /// backing <c>GraphObject</c> is resolved from the graph's model.
        /// </param>
        /// <returns>
        /// An array of 0..n handles. Empty when the graph is invalid, has no backing asset, or no window
        /// currently displays it. Enumeration order is not guaranteed — callers treat the result as a set.
        /// </returns>
        /// <remarks>
        /// The matching mirrors <see cref="GraphViewEditorWindow.ShowGraphInExistingOrNewWindow"/>'s
        /// window-reuse logic (<c>GraphModel?.GraphObject == graphObject</c>), so this returns exactly the
        /// windows that the fork itself would reuse for the same graph object — never an unrelated window.
        /// </remarks>
        public static GraphViewWindowHandle[] GetWindowsForGraphObject(Graph graph)
        {
            if (graph == null || graph.m_Implementation == null || graph.m_Implementation.GraphObject == null)
                return Array.Empty<GraphViewWindowHandle>();

            var graphObject = graph.m_Implementation.GraphObject;

            var matches = new List<GraphViewWindowHandle>();
            foreach (var window in Resources.FindObjectsOfTypeAll<GraphViewEditorWindow>())
            {
                // Destroyed windows compare null via Unity's overloaded ==; skip before touching members.
                if (window == null)
                    continue;

                if (window.GraphTool?.ToolState?.GraphModel?.GraphObject == graphObject)
                    matches.Add(new GraphViewWindowHandle(window));
            }

            return matches.ToArray();
        }

        /// <summary>
        /// Resolves the node view displaying <paramref name="node"/> inside the window referenced by
        /// <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">
        /// A window handle from <see cref="GetWindowsForGraphObject"/>. May point to a destroyed window.
        /// </param>
        /// <param name="node">
        /// The node to find, as enumerated by the graph (e.g. <see cref="Graph.GetNodes"/>, or a
        /// <see cref="BlockNode"/> from its <see cref="ContextNode"/>). Identity is the node's model
        /// instance, not its name or position.
        /// </param>
        /// <param name="view">The matching node view as a <see cref="VisualElement"/>, or null.</param>
        /// <returns>
        /// True when the node's view is present in one of the window's graph views. False when the handle,
        /// window, or node is invalid, or when the view is not currently in the visual tree (e.g. culled —
        /// callers polling every tick simply retry).
        /// </returns>
        /// <remarks>
        /// Resolution chain: the <see cref="INode"/> is mapped to the model that backs its view — a
        /// user-authored <see cref="Node"/> wraps its model directly, while internal node models
        /// (subgraph/variable/constant models) implement <see cref="INode"/> themselves — then the
        /// window's graph views are queried for the <see cref="NodeView"/> whose
        /// <see cref="ModelView.Model"/> is that same model instance (reference equality). No reflection.
        /// </remarks>
        public static bool TryGetNodeView(GraphViewWindowHandle handle, INode node, out VisualElement view)
        {
            view = null;
            if (handle?.Window == null || node == null)
                return false;

            var model = GetNodeModel(node);
            if (model == null)
                return false;

            foreach (var graphView in handle.Window.GraphViews)
            {
                if (graphView == null)
                    continue;

                foreach (var nodeView in graphView.Query<NodeView>().Build())
                {
                    if (!ReferenceEquals(nodeView.Model, model))
                        continue;

                    view = nodeView;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tints a node view with the USS class for <paramref name="status"/>.
        /// </summary>
        /// <param name="view">The node view returned by <see cref="TryGetNodeView"/>. Null is ignored.</param>
        /// <param name="status">The status to display. Replaces any previous debug status class.</param>
        /// <remarks>
        /// A view carries at most ONE debug status class at a time — previous status classes are removed
        /// first, so a node that stops being active immediately stops looking active. The USS classes are
        /// <see cref="runningUssClassName"/>, <see cref="visitedUssClassName"/>, and
        /// <see cref="breakpointUssClassName"/>; styling them is the debugger's responsibility (editor USS).
        /// </remarks>
        public static void HighlightNode(VisualElement view, DebugNodeStatus status)
        {
            if (view == null)
                return;

            view.RemoveFromClassList(runningUssClassName);
            view.RemoveFromClassList(visitedUssClassName);
            view.RemoveFromClassList(breakpointUssClassName);

            switch (status)
            {
                case DebugNodeStatus.Running:
                    view.AddToClassList(runningUssClassName);
                    break;
                case DebugNodeStatus.Visited:
                    view.AddToClassList(visitedUssClassName);
                    break;
                case DebugNodeStatus.Breakpoint:
                    view.AddToClassList(breakpointUssClassName);
                    break;
            }
        }

        /// <summary>
        /// Removes every debug status class from all node views in the window referenced by
        /// <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">
        /// A window handle from <see cref="GetWindowsForGraphObject"/>. Null or destroyed handles are
        /// ignored.
        /// </param>
        /// <remarks>
        /// Used when debug state resets — entity deselected, world stopped, or a new node becomes active —
        /// so stale highlights never linger on nodes the debugger no longer tracks.
        /// </remarks>
        public static void ClearHighlights(GraphViewWindowHandle handle)
        {
            if (handle?.Window == null)
                return;

            foreach (var graphView in handle.Window.GraphViews)
            {
                if (graphView == null)
                    continue;

                foreach (var nodeView in graphView.Query<NodeView>().Build())
                {
                    nodeView.RemoveFromClassList(runningUssClassName);
                    nodeView.RemoveFromClassList(visitedUssClassName);
                    nodeView.RemoveFromClassList(breakpointUssClassName);
                }
            }
        }

        /// <summary>
        /// Resolves the wire view connecting <paramref name="fromNode"/>'s output to
        /// <paramref name="toNode"/>'s input inside the window referenced by <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">A window handle from <see cref="GetWindowsForGraphObject"/>. May point to a destroyed window.</param>
        /// <param name="fromNode">The transition's source node.</param>
        /// <param name="toNode">The transition's target node.</param>
        /// <param name="view">The matching wire view, or null.</param>
        /// <returns>
        /// True when a wire connecting the two nodes is present in one of the window's graph views.
        /// False for invalid inputs, no matching wire, or a culled view (pollers simply retry).
        /// </returns>
        /// <remarks>
        /// Resolves both plain wire views and transition-support wire views (graphs that opt execution wires
        /// into transition supports). Matching is by node-model reference identity — the same rule as
        /// <see cref="TryGetNodeView"/> — and direction-aware, so the two opposite links of a loop
        /// (A -&gt; B and B -&gt; A) resolve distinctly. When several wires connect the same ordered node pair,
        /// the first match is returned; callers that need a specific edge should disambiguate first.
        /// </remarks>
        public static bool TryGetWireView(GraphViewWindowHandle handle, INode fromNode, INode toNode, out VisualElement view)
        {
            view = null;
            if (handle?.Window == null || fromNode == null || toNode == null)
                return false;

            var fromModel = GetNodeModel(fromNode);
            var toModel = GetNodeModel(toNode);
            if (fromModel == null || toModel == null)
                return false;

            foreach (var graphView in handle.Window.GraphViews)
            {
                if (graphView == null)
                    continue;

                foreach (var wireView in graphView.Query<AbstractWire>().Build())
                {
                    var wireModel = wireView.WireModel;
                    if (wireModel?.FromPort == null || wireModel.ToPort == null)
                        continue;
                    if (!ReferenceEquals(wireModel.FromPort.NodeModel, fromModel))
                        continue;
                    if (!ReferenceEquals(wireModel.ToPort.NodeModel, toModel))
                        continue;

                    view = wireView;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tints a wire view as the active debug transition (amber, matching the node Running highlight).
        /// </summary>
        /// <param name="view">A wire view returned by <see cref="TryGetWireView"/>. Null is ignored.</param>
        /// <remarks>
        /// Supports both plain <see cref="Wire"/> views and transition-support <c>Transition</c> views:
        /// transition controls are recolored, and transition arrows (single-state transitions) get their
        /// outer and inner contour recolored.
        /// </remarks>
        public static void HighlightWire(VisualElement view)
        {
            SetWireColor(view, k_ActiveWireColor);
        }

        /// <summary>
        /// Restores a wire view's default color.
        /// </summary>
        /// <param name="view">A wire view previously passed to <see cref="HighlightWire"/>. Null is ignored.</param>
        public static void ResetWireHighlight(VisualElement view)
        {
            if (view == null)
                return;

            var wireControl = view.Q<WireControl>();
            if (wireControl != null)
            {
                wireControl.ResetColor();
                return;
            }

            view.Q<TransitionControl>()?.ResetColor();

            var transitionArrow = view.Q<TransitionArrow>();
            if (transitionArrow != null)
            {
                transitionArrow.ResetOuterLineColor();
                transitionArrow.ResetInnerLineColor();
            }
        }

        static void SetWireColor(VisualElement view, Color color)
        {
            if (view == null)
                return;

            var wireControl = view.Q<WireControl>();
            if (wireControl != null)
            {
                wireControl.SetColor(color, color);
                return;
            }

            var transitionControl = view.Q<TransitionControl>();
            if (transitionControl != null)
                transitionControl.Color = color;

            var transitionArrow = view.Q<TransitionArrow>();
            if (transitionArrow != null)
            {
                transitionArrow.OuterLineColor = color;
                transitionArrow.InnerLineColor = color;
            }
        }

        /// <summary>
        /// Restores every wire in the window referenced by <paramref name="handle"/> to its default color.
        /// </summary>
        /// <param name="handle">A window handle from <see cref="GetWindowsForGraphObject"/>. Null or destroyed handles are ignored.</param>
        /// <remarks>Used when debug state resets so stale wire highlights never linger.</remarks>
        public static void ClearWireHighlights(GraphViewWindowHandle handle)
        {
            if (handle?.Window == null)
                return;

            foreach (var graphView in handle.Window.GraphViews)
            {
                if (graphView == null)
                    continue;

                foreach (var wireView in graphView.Query<AbstractWire>().Build())
                    ResetWireHighlight(wireView);
            }
        }

        /// <summary>
        /// Attaches or updates a small read-only badge label on <paramref name="view"/> showing live
        /// runtime info (e.g. "wp 3/5").
        /// </summary>
        /// <param name="view">
        /// The node view returned by <see cref="TryGetNodeView"/>. Null or destroyed views are ignored.
        /// </param>
        /// <param name="text">The badge text. Null or empty clears the badge instead of displaying an empty one.</param>
        /// <remarks>
        /// <para>
        /// The badge is a plain <see cref="Label"/> carrying <see cref="debugInfoUssClassName"/>, appended
        /// to the node's title part root. <see cref="NodeView"/> exposes no public title container in this
        /// fork, and the title part root is the least intrusive stable host: it survives the fork's
        /// culling/rebuild cycle (part roots are detached and re-added as whole subtrees), whereas direct
        /// children of the node view are wiped by <c>Clear()</c> and would silently vanish.
        /// </para>
        /// <para>
        /// Idempotent: exactly one badge per node view. The label instance is cached in
        /// <c>view.userData</c> — a slot the fork leaves unused on node views — so debuggers polling every
        /// tick update the same label instead of stacking new ones, and a label detached by a UI rebuild is
        /// re-attached rather than duplicated. The badge never intercepts pointer events
        /// (<c>pickingMode = PickingMode.Ignore</c>), so node selection, dragging, and ports are unaffected.
        /// </para>
        /// </remarks>
        public static void SetNodeDebugInfo(VisualElement view, string text)
        {
            if (view == null || view.panel == null)
                return;

            if (string.IsNullOrEmpty(text))
            {
                ClearNodeDebugInfo(view);
                return;
            }

            var label = GetOrCreateDebugInfoBadge(view);
            if (label != null)
                label.text = text;
        }

        /// <summary>
        /// Removes the debug-info badge label previously attached to <paramref name="view"/> by
        /// <see cref="SetNodeDebugInfo"/>, if present.
        /// </summary>
        /// <param name="view">
        /// The node view returned by <see cref="TryGetNodeView"/>. Null or destroyed views are ignored.
        /// </param>
        /// <remarks>
        /// Also frees the <c>userData</c> cache slot when it holds the badge, so a later
        /// <see cref="SetNodeDebugInfo"/> starts clean.
        /// </remarks>
        public static void ClearNodeDebugInfo(VisualElement view)
        {
            if (view == null || view.panel == null)
                return;

            if (view.userData is Label cached && cached.ClassListContains(debugInfoUssClassName))
            {
                cached.RemoveFromHierarchy();
                view.userData = null;
                return;
            }

            // The cache slot may be occupied by foreign userData, in which case the badge is found by class.
            view.Q<Label>(className: debugInfoUssClassName)?.RemoveFromHierarchy();
        }

        /// <summary>
        /// Returns the cached debug-info badge for <paramref name="view"/>, creating and attaching one on
        /// first use.
        /// </summary>
        /// <param name="view">A live node view.</param>
        /// <returns>
        /// The badge label, or null when no badge could be resolved (e.g. a view torn down mid-call);
        /// callers degrade to a no-op in that case.
        /// </returns>
        /// <remarks>
        /// Primary cache is <c>view.userData</c>, which the fork leaves unused on node views. When the slot
        /// is occupied by foreign data the badge is looked up by USS class instead; if neither yields a
        /// label, a new uncached one is created — the class-based lookup still prevents stacking on
        /// subsequent calls. A cached label whose parent is null was detached by a UI rebuild or culling
        /// transition and is re-attached to the current host rather than duplicated.
        /// </remarks>
        private static Label GetOrCreateDebugInfoBadge(VisualElement view)
        {
            if (view.userData is Label cached)
            {
                if (cached.hierarchy.parent == null)
                    GetDebugInfoBadgeHost(view).Add(cached);

                return cached;
            }

            if (view.userData == null)
            {
                var label = CreateDebugInfoBadge();
                GetDebugInfoBadgeHost(view).Add(label);
                view.userData = label;
                return label;
            }

            var existing = view.Q<Label>(className: debugInfoUssClassName);
            if (existing != null)
                return existing;

            var uncached = CreateDebugInfoBadge();
            GetDebugInfoBadgeHost(view).Add(uncached);
            return uncached;
        }

        private static Label CreateDebugInfoBadge()
        {
            // Ignore so the badge never steals clicks from the node it sits on.
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(debugInfoUssClassName);
            return label;
        }

        /// <summary>
        /// Returns the container the debug-info badge should be appended to for <paramref name="view"/>.
        /// </summary>
        /// <param name="view">A live node view.</param>
        /// <returns>
        /// The title part root when the view is a <see cref="NodeView"/> with a built title part; the view
        /// itself otherwise (e.g. custom node views without a title part).
        /// </returns>
        /// <remarks>
        /// <see cref="NodeView"/> exposes no public <c>titleContainer</c>; the title lives in a model-view
        /// part whose name varies by node type — plain nodes use
        /// <see cref="NodeView.titleContainerPartName"/> (<c>title-container</c>) while collapsible and
        /// capsule nodes use <c>title-icon-container</c>. Part roots are the only stable hosts here —
        /// <see cref="GraphElement"/> culling wipes direct children of the node view via <c>Clear()</c>.
        /// </remarks>
        private static VisualElement GetDebugInfoBadgeHost(VisualElement view)
        {
            if (view is ChildView childView && childView.PartList != null)
            {
                if (childView.PartList.GetPart(NodeView.titleContainerPartName)?.Root is { } titleRoot)
                    return titleRoot;

                if (childView.PartList.GetPart(CollapsibleInOutNodeView.titleIconContainerPartName)?.Root is { } collapsibleTitleRoot)
                    return collapsibleTitleRoot;
            }

            return view;
        }

        /// <summary>
        /// Maps an <see cref="INode"/> to the model that backs its view, or null.
        /// </summary>
        /// <param name="node">The node to map.</param>
        /// <returns>
        /// The node's model — <see cref="Node.m_Implementation"/> for user-authored nodes, the node itself
        /// for internal model types that implement <see cref="INode"/> directly — or null when the node has
        /// no model (e.g. a <see cref="Node"/> that was never added to a graph).
        /// </returns>
        private static AbstractNodeModel GetNodeModel(INode node)
        {
            // User-authored nodes (Node, BlockNode, ContextNode, ...) wrap their model in m_Implementation;
            // internal node models (SubgraphNodeModelImp, VariableNodeModelImp, ConstantNodeModelImp, ...)
            // implement INode directly, so the node IS the model.
            if (node is Node userNode)
                return userNode.m_Implementation;

            return node as AbstractNodeModel;
        }
    }
}
