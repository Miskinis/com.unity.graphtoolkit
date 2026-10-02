using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.GraphToolkit.Editor
{
    /// <summary>
    /// The UI for an <see cref="WireModel"/>.
    /// </summary>
    [UnityRestricted]
    internal class Wire : AbstractWire, IShowItemLibraryUI
    {
        /// <summary>
        /// The USS class name added to a <see cref="Wire"/>.
        /// </summary>
        public new static readonly string ussClassName = "ge-wire";

        /// <summary>
        /// The USS class name added to ghost wires.
        /// </summary>
        public static readonly string ghostUssClassName = ussClassName.WithUssModifier(ghostUssModifier);

        /// <summary>
        /// The name used for the <see cref="ModelViewPart"/> of the wire control.
        /// </summary>
        public static readonly string wireControlName = "wire-control";

        /// <summary>
        /// The name used for the <see cref="ModelViewPart"/> of the wire bubble.
        /// </summary>
        public static readonly string wireBubblePartName = "wire-bubble";

        WireManipulator m_WireManipulator;

        WireControl m_WireControl;

        ChildView m_LastUsedFromPort;
        ChildView m_LastUsedToPort;
        WireModel m_LastUsedWireModel;

        bool m_VisuallySelected;

        // Selected wires render IN FRONT of nodes: wires rest in layer -1 (below the node layer),
        // where BringToFront only reorders among wires. While selected, the view moves to a
        // dedicated top layer and returns to its resting layer when deselected. The drag
        // candidate uses Int32.MaxValue, so selected wires stay just below it.
        const int k_SelectedWireLayer = int.MaxValue - 1;
        int m_RestingLayer = -1;

        protected WireManipulator WireManipulator
        {
            get => m_WireManipulator;
            set => this.ReplaceManipulator(ref m_WireManipulator, value);
        }

        /// <inheritdoc />
        public override Vector2 GetFrom()
        {
            var p = Vector2.zero;

            var port = WireModel.FromPort;
            if (port == null)
            {
                if (WireModel is IGhostWireModel ghostWire)
                {
                    p = ghostWire.FromWorldPoint;
                }
            }
            else
            {
                var ui = port.GetView<Port>(RootView);
                if (ui == null)
                    return Vector2.zero;

                p = ui.GetGlobalCenter();
            }

            return this.WorldToLocal(p);
        }

        /// <inheritdoc />
        public override Vector2 GetTo()
        {
            var p = Vector2.zero;

            var port = WireModel.ToPort;
            if (port == null)
            {
                if (WireModel is IGhostWireModel ghostWireModel)
                {
                    p = ghostWireModel.ToWorldPoint;
                }
            }
            else
            {
                var ui = port.GetView<Port>(RootView);
                if (ui == null)
                    return Vector2.zero;

                p = ui.GetGlobalCenter();
            }

            return this.WorldToLocal(p);
        }

        /// <summary>
        /// The WireControl that represents the wire.
        /// </summary>
        public WireControl WireControl => m_WireControl;

        public PortModel Output => WireModel.FromPort;

        public PortModel Input => WireModel.ToPort;

        internal override VisualElement SizeElement => WireControl;

        /// <summary>
        /// Initializes a new instance of the <see cref="Wire"/> class.
        /// </summary>
        public Wire()
        {
            Layer = -1;

            WireManipulator = new WireManipulator();
        }

        /// <inheritdoc />
        public override void BuildUITree()
        {
            base.BuildUITree();

            m_WireControl = new WireControl(this) { name = wireControlName };
            m_WireControl.AddToClassList(ussClassName.WithUssElement(wireControlName));

            m_WireControl.RegisterCallback<MouseLeaveEvent>(OnMouseLeaveWire);
            m_WireControl.RegisterCallback<MouseDownEvent>(OnMouseDownWire);

            Insert(0, m_WireControl);
        }

        /// <inheritdoc />
        protected override void BuildPartList()
        {
            PartList.AppendPart(WireBubblePart.Create(wireBubblePartName, Model, this, ussClassName));
        }

        /// <inheritdoc />
        protected override void PostBuildUI()
        {
            base.PostBuildUI();
            WireControl?.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            AddToClassList(ussClassName);
            EnableInClassList(ghostUssClassName, Model is IGhostWireModel);
            this.AddPackageStylesheet("Wire.uss");
        }

        /// <inheritdoc />
        public override bool HasBackwardsDependenciesChanged()
        {
            return m_LastUsedFromPort != WireModel.FromPort?.GetView(RootView) || m_LastUsedToPort != WireModel.ToPort?.GetView(RootView);
        }

        /// <inheritdoc />
        public override bool HasModelDependenciesChanged() => m_LastUsedWireModel != WireModel;

        /// <inheritdoc/>
        public override void AddBackwardDependencies()
        {
            base.AddBackwardDependencies();

            // When the ports move, the wire should be redrawn.
            AddDependencies(WireModel.FromPort);
            AddDependencies(WireModel.ToPort);

            m_LastUsedFromPort = WireModel.FromPort.GetView(RootView);
            m_LastUsedToPort = WireModel.ToPort.GetView(RootView);

            void AddDependencies(PortModel portModel)
            {
                if (portModel == null)
                    return;

                var ui = portModel.GetView(RootView);
                if (ui != null)
                {
                    // Wire color changes with port color.
                    Dependencies.AddBackwardDependency(ui, DependencyTypes.Style);

                    // When port geometry changes, the wire should follow.
                    Dependencies.AddBackwardDependency(ui, DependencyTypes.Geometry);
                }

                ui = portModel.NodeModel.GetView(RootView);
                if (ui != null)
                {
                    // Wire position changes with node position.
                    Dependencies.AddBackwardDependency(ui, DependencyTypes.Geometry);
                }

                ui = (portModel.NodeModel.Container as GraphElementModel)?.GetView(GraphView);
                if (ui != null)
                {
                    // Wire position changes with container's position.
                    Dependencies.AddBackwardDependency(ui, DependencyTypes.Geometry);
                }
            }
        }

        /// <inheritdoc/>
        public override void AddModelDependencies()
        {
            var ui = WireModel.FromPort?.GetView<Port>(RootView);
            ui?.AddDependencyToWireModel(WireModel);

            ui = WireModel.ToPort?.GetView<Port>(RootView);
            ui?.AddDependencyToWireModel(WireModel);

            m_LastUsedWireModel = WireModel;
        }

        /// <inheritdoc />
        public override bool Overlaps(Rect rectangle)
        {
            return WireControl.RectIntersectsLine(rectangle);
        }

        /// <inheritdoc />
        public override bool ContainsPoint(Vector2 localPoint)
        {
            return WireControl.IsPointOnLine(localPoint);
        }

        /// <summary>
        /// Gets the wire data required to create portals.
        /// </summary>
        /// <param name="wires">The wires that will be converted to portals.</param>
        /// <param name="rootView">The <see cref="RootView"/> that contains the portals.</param>
        internal static List<(WireModel, Vector2, Vector2)> GetPortalsWireData(IEnumerable<WireModel> wires, RootView rootView)
        {
            var wireData = wires.Select(
                wireModel =>
                {
                    var outputPort = wireModel.FromPort.GetView<Port>(rootView);
                    var inputPort = wireModel.ToPort.GetView<Port>(rootView);
                    var outputNode = wireModel.FromPort.NodeModel.GetView<NodeView>(rootView);
                    var inputNode = wireModel.ToPort.NodeModel.GetView<NodeView>(rootView);
                    var wire = wireModel.GetView<Wire>(rootView);

                    if (outputNode == null || inputNode == null || outputPort == null || inputPort == null || wire == null)
                        return (null, Vector2.zero, Vector2.zero);

                    return (wireModel,
                        outputPort.ChangeCoordinatesTo(wire.contentContainer, outputPort.layout.center),
                        inputPort.ChangeCoordinatesTo(wire.contentContainer, inputPort.layout.center));
                }
                ).Where(tuple => tuple.Item1 != null).ToList();

            return wireData;
        }

        /// <inheritdoc/>
        public virtual bool ShowItemLibrary(Vector2 mousePosition)
        {
            var graphPosition = GraphView.ContentViewContainer.WorldToLocal(mousePosition);
            ItemLibraryService.ShowNodesForWire(GraphView, WireModel, mousePosition, item =>
            {
                if (item is GraphNodeModelLibraryItem nodeItem)
                    GraphView.Dispatch(CreateNodeCommand.OnWire(nodeItem, WireModel, graphPosition));
            });

            return true;
        }

        /// <inheritdoc/>
        public override void SetElementLevelOfDetail(float zoom, GraphViewZoomMode newZoomMode, GraphViewZoomMode oldZoomMode)
        {
            base.SetElementLevelOfDetail(zoom, newZoomMode, oldZoomMode);

            if (WireControl != null)
                WireControl.Zoom = zoom;
        }

        /// <inheritdoc/>
        public override void UpdateUIFromModel(UpdateFromModelVisitor visitor)
        {
            base.UpdateUIFromModel(visitor);
            if (WireModel?.GraphModel is not null )
            {
                m_WireControl.FromOrientation = WireModel.FromPort?.Orientation ?? (WireModel.ToPort?.Orientation ?? PortOrientation.Horizontal);
                m_WireControl.ToOrientation = WireModel.ToPort?.Orientation ?? (WireModel.FromPort?.Orientation ?? PortOrientation.Horizontal);
                m_WireControl.FromDirection = WireModel.FromPort?.Direction ?? GetReverseDirection(WireModel.ToPort?.Direction ?? PortDirection.Output);
                m_WireControl.ToDirection = WireModel.ToPort?.Direction ?? GetReverseDirection(WireModel.FromPort?.Direction ?? PortDirection.Input);

                if ((WireModel.ToPort is null || WireModel.FromPort is null))
                {
                    var(inputResult, outputResult) = WireModel.AddMissingPorts(out var inputNode, out var outputNode);

                    if (inputResult == PortMigrationResult.MissingPortAdded && inputNode != null)
                    {
                        var inputNodeUi = inputNode.GetView(GraphView);
                        inputNodeUi?.UpdateView(visitor);
                    }

                    if (outputResult == PortMigrationResult.MissingPortAdded && outputNode != null)
                    {
                        var outputNodeUi = outputNode.GetView(GraphView);
                        outputNodeUi?.UpdateView(visitor);
                    }
                }
            }

            if (visitor.ChangeHints.HasChange(ChangeHint.Layout))
            {
                m_WireControl.UpdateLayout();
            }

            UpdateWireControlColors();
            m_WireControl.MarkDirtyRepaint();
        }

        /// <inheritdoc />
        public override void UpdateSelectionVisuals(bool selected)
        {
            m_VisuallySelected = selected;
            base.UpdateSelectionVisuals(selected);
            UpdateWireControlColors();
            UpdateSelectionLayer(selected);
        }

        /// <summary>
        /// Moves a selected wire to the front-most layer so it renders above nodes, and restores
        /// its resting layer when deselected.
        /// </summary>
        void UpdateSelectionLayer(bool selected)
        {
            var graphView = GraphView;
            if (graphView == null)
                return;

            if (selected)
            {
                if (Layer < k_SelectedWireLayer)
                {
                    m_RestingLayer = Layer;
                    Layer = k_SelectedWireLayer;
                    graphView.ChangeLayer(this);
                }
            }
            else if (Layer == k_SelectedWireLayer)
            {
                Layer = m_RestingLayer;
                graphView.ChangeLayer(this);
            }
        }

        /// <summary>
        /// True when this wire is a back edge: following it returns to a node that is still on the
        /// current depth-first path (an ancestor of its source) — i.e. the wire closes a loop — or
        /// the wire is a self-loop. <see cref="WireControl"/> draws back edges dashed so a
        /// loop-back reads as a loop instead of looking like a plain connection.
        /// </summary>
        /// <remarks>
        /// Classic back-edge classification (depth-first, roots = nodes without incoming wires),
        /// so only the returning leg of a loop is styled — not every wire that participates in a
        /// cycle. Computed per draw (graphs are small); not cached, so a wire that closes a new
        /// cycle styles correctly without a view rebuild.
        /// </remarks>
        public bool IsLoopBackWire()
        {
            var wireModel = WireModel;
            var fromNode = wireModel?.FromPort?.NodeModel;
            var toNode = wireModel?.ToPort?.NodeModel;
            if (fromNode == null || toNode == null)
                return false;
            if (ReferenceEquals(fromNode, toNode))
                return true; // self-loop

            var graphModel = GraphView?.GraphModel;
            if (graphModel == null)
                return false;

            // Adjacency + roots (nodes with no incoming wires; a tailed cycle starts there).
            var outgoing = new Dictionary<PortNodeModel, List<WireModel>>();
            var roots = new List<PortNodeModel>();
            var hasIncoming = new HashSet<PortNodeModel>();
            foreach (var wire in graphModel.WireModels)
            {
                var from = wire.FromPort?.NodeModel;
                var to = wire.ToPort?.NodeModel;
                if (from == null || to == null)
                    continue;

                if (!outgoing.TryGetValue(from, out var outgoingWires))
                    outgoing[from] = outgoingWires = new List<WireModel>();
                outgoingWires.Add(wire);
                hasIncoming.Add(to);
                if (!outgoing.ContainsKey(to))
                    outgoing[to] = new List<WireModel>(); // ensure target nodes are walkable
            }

            foreach (var pair in outgoing)
            {
                if (!hasIncoming.Contains(pair.Key))
                    roots.Add(pair.Key);
            }

            // Iterative depth-first walk with gray (on path) / black (finished) colors.
            // The candidate wire is a back edge when it is traversed while its target is gray.
            var color = new Dictionary<PortNodeModel, int>();
            var nodeStack = new Stack<PortNodeModel>();
            var wireIndexStack = new Stack<int>();

            PortNodeModel NextUnvisited()
            {
                foreach (var root in roots)
                {
                    if (!color.ContainsKey(root))
                        return root;
                }

                foreach (var pair in outgoing)
                {
                    if (!color.ContainsKey(pair.Key))
                        return pair.Key;
                }

                return null;
            }

            var first = NextUnvisited();
            while (first != null)
            {
                color[first] = 1;
                nodeStack.Push(first);
                wireIndexStack.Push(0);

                while (nodeStack.Count > 0)
                {
                    var node = nodeStack.Peek();
                    var wireIndex = wireIndexStack.Pop();

                    if (!outgoing.TryGetValue(node, out var wires) || wireIndex >= wires.Count)
                    {
                        color[node] = 2; // black — done with this node's subtree
                        nodeStack.Pop();
                        continue;
                    }

                    wireIndexStack.Push(wireIndex + 1);
                    var wire = wires[wireIndex];
                    var target = wire.ToPort?.NodeModel;
                    if (target == null)
                        continue;

                    if (ReferenceEquals(wire, wireModel))
                        return color.TryGetValue(target, out var targetColor) && targetColor == 1;

                    if (!color.ContainsKey(target))
                    {
                        color[target] = 1;
                        nodeStack.Push(target);
                        wireIndexStack.Push(0);
                    }
                }

                first = NextUnvisited();
            }

            return false;
        }

        /// <inheritdoc />
        public override bool CanBePartitioned()
        {
            return Model is not GhostWireModel && base.CanBePartitioned();
        }

        /// <inheritdoc />
        public override Rect GetBoundingBox()
        {
            return WireControl.layout;
        }

        static PortDirection GetReverseDirection(PortDirection direction)
        {
            switch (direction)
            {
                case PortDirection.Input:
                    return PortDirection.Output;
                default:
                    return PortDirection.Input;
            }
        }

        /// <summary>
        /// Allow updating the color of the wire.
        /// </summary>
        /// <param name="from">The color of the from side.</param>
        /// <param name="to">The color of the to side</param>
        /// <returns>True if the custom color should be used.</returns>
        protected virtual bool GetWireCustomColor(out Color from, out Color to)
        {
            from = Color.clear;
            to = Color.clear;
            return false;
        }

        /// <summary>
        /// Update the wire color base on the selection state, the port color, or a custom wire color.
        /// </summary>
        protected void UpdateWireControlColors()
        {
            if (m_VisuallySelected)
            {
                m_WireControl.ResetColor();
            }
            else if (GetWireCustomColor(out Color input, out Color output))
            {
                m_WireControl.SetColor(input, output);
            }
            else if (WireModel is IPlaceholder)
            {
                m_WireControl.SetColor(Color.red, Color.red);
            }
            else
            {
                var inputColor = Color.white;
                var outputColor = Color.white;

                if (WireModel?.ToPort != null)
                    inputColor = WireModel.ToPort.GetView<Port>(RootView)?.PortColor ?? Color.white;
                else if (WireModel?.FromPort != null)
                    inputColor = WireModel.FromPort.GetView<Port>(RootView)?.PortColor ?? Color.white;

                if (WireModel?.FromPort != null)
                    outputColor = WireModel.FromPort.GetView<Port>(RootView)?.PortColor ?? Color.white;
                else if (WireModel?.ToPort != null)
                    outputColor = WireModel.ToPort.GetView<Port>(RootView)?.PortColor ?? Color.white;

                if (WireModel is IGhostWireModel)
                {
                    inputColor = new Color(inputColor.r, inputColor.g, inputColor.b, 0.5f);
                    outputColor = new Color(outputColor.r, outputColor.g, outputColor.b, 0.5f);
                }

                m_WireControl.SetColor(inputColor, outputColor);
            }
        }

        void OnMouseDownWire(MouseDownEvent e)
        {
            if (e.target == m_WireControl)
            {
                m_WireControl.ResetColor();
            }
        }

        void OnMouseLeaveWire(MouseLeaveEvent e)
        {
            if (e.target == m_WireControl)
            {
                UpdateWireControlColors();
            }
        }
    }
}
