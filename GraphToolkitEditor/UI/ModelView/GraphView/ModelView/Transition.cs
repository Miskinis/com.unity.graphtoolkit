using System;
using System.Collections.Generic;
using Unity.GraphToolkit.InternalBridge;
using Unity.GraphToolsAuthoringFramework.InternalEditorBridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.GraphToolkit.Editor
{
    /// <summary>
    /// Class for transition UI.
    /// </summary>
    [UnityRestricted]
    internal class Transition : AbstractTransition
    {
        /// <summary>
        /// The USS class name added to a <see cref="Transition"/>.
        /// </summary>
        public new static readonly string ussClassName = "ge-transition";

        /// <summary>
        /// The USS class name added to ghost transitions.
        /// </summary>
        public static readonly string ghostUssClassName = ussClassName.WithUssModifier(ghostUssModifier);

        /// <summary>
        /// The USS class name added to local transitions.
        /// </summary>
        public static readonly string localTransitionUssClassName = ussClassName.WithUssModifier("local");

        /// <summary>
        /// The USS class name added to self transitions.
        /// </summary>
        public static readonly string selfTransitionUssClassName = ussClassName.WithUssModifier("self");

        /// <summary>
        /// The USS class name added to transitions triggered when entering a state machine.
        /// </summary>
        public static readonly string onEnterSelectorUssClassName = ussClassName.WithUssModifier("on-enter");

        /// <summary>
        /// The USS class name added to transitions between two states.
        /// </summary>
        public static readonly string stateToStateSelectorUssClassName = ussClassName.WithUssModifier("state-to-state");

        /// <summary>
        /// The USS class name added to transitions that carry at least one authored condition.
        /// </summary>
        public static readonly string conditionedUssClassName = ussClassName.WithUssModifier("conditioned");

        /// <summary>
        /// The name of the condition-indicator badge element.
        /// </summary>
        public static readonly string conditionBadgeName = "transition-condition-badge";

        /// <summary>
        /// The USS class name of the condition-indicator badge.
        /// </summary>
        public static readonly string conditionBadgeUssClassName = ussClassName.WithUssElement("condition-badge");

        /// <summary>
        /// The size of the condition-indicator badge, in graph units. Mirrors the badge size in Wire.uss.
        /// </summary>
        const float k_ConditionBadgeSize = 15f;

        /// <summary>
        /// The name used for the <see cref="ModelViewPart"/> of the transition arrow.
        /// </summary>
        public static readonly string transitionArrowPartName = "transition-arrow";

        /// <summary>
        /// The name used for the <see cref="ModelViewPart"/> of the transition control.
        /// </summary>
        public static readonly string transitionControlPartName = "transition-control";

        /// <summary>
        /// The name for the "paste transitions as new" command.
        /// </summary>
        public static readonly string pasteTransitionsAsNewCommandName = "Paste Transitions as New";

        // Dependency tracking
        ChildView m_LastUsedFromPort;
        ChildView m_LastUsedToPort;
        Port m_LastUsedFromPortView;
        Port m_LastUsedToPortView;
        Hash128 m_LastUsedFromNodeModelGuid;
        Hash128 m_LastUsedToNodeModelGuid;

        // Condition roots the indicator reads. Tracked so model-dependency registrations are rebuilt
        // when transitions or their root groups are added or replaced.
        readonly List<GraphElementModel> m_ConditionRoots = new();

        TransitionHoverDetector m_TransitionHoverDetector;
        TransitionSupportAnchorManipulator m_TransitionAnchorManipulator;

        TransitionControl m_TransitionControl;
        TransitionArrow m_TransitionArrow;
        Label m_ConditionBadge;

        bool m_ShowConnectors;

        /// <inheritdoc />
        public override bool Hovered
        {
            get => base.Hovered;
            set
            {
                base.Hovered = value;
                ShowHideConnectors();
            }
        }

        /// <summary>
        /// The transition hover detector.
        /// </summary>
        protected TransitionHoverDetector TransitionHoverDetector
        {
            get => m_TransitionHoverDetector;
            set => this.ReplaceManipulator(ref m_TransitionHoverDetector, value);
        }

        /// <summary>
        /// The transition anchor manipulator.
        /// </summary>
        protected TransitionSupportAnchorManipulator TransitionSupportAnchorManipulator
        {
            get => m_TransitionAnchorManipulator;
            set => this.ReplaceManipulator(ref m_TransitionAnchorManipulator, value);
        }

        /// <summary>
        /// The transition control.
        /// </summary>
        public TransitionControl TransitionControl
        {
            get
            {
                if (m_TransitionControl == null)
                {
                    var wireControlPart = PartList.GetPart(transitionControlPartName);
                    m_TransitionControl = wireControlPart?.Root as TransitionControl;
                }

                return m_TransitionControl;
            }
        }

        /// <summary>
        /// The transition arrow.
        /// </summary>
        protected TransitionArrow TransitionArrow
        {
            get
            {
                if (m_TransitionArrow == null)
                {
                    var wireControlPart = PartList.GetPart(transitionArrowPartName);
                    m_TransitionArrow = wireControlPart?.Root as TransitionArrow;
                }

                return m_TransitionArrow;
            }
        }

        /// <inheritdoc />
        public override Vector2 GetFrom()
        {
            var p = Vector2.zero;

            var port = WireModel.FromPort;
            if (port == null)
            {
                if (WireModel is IGhostWireModel ghostWireModel)
                {
                    p = ghostWireModel.FromWorldPoint;
                }
            }
            else
            {
                p = GetEndpointPosition(port, true);
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
                p = GetEndpointPosition(port, false);
            }

            return this.WorldToLocal(p);
        }

        /// <summary>
        /// Resolves one endpoint of the transition in graph content coordinates.
        /// </summary>
        /// <param name="port">The connected port.</param>
        /// <param name="isFromSide">Whether the endpoint is the start (true) or the end (false) of the transition.</param>
        /// <returns>The endpoint position in graph content coordinates.</returns>
        /// <remarks>
        /// State-anchored transitions (state machines) resolve against the state view's border anchor.
        /// Transitions between regular execution ports have no <see cref="State"/> view and resolve
        /// against the port view's center, exactly like plain wires, so the arrow renders between the
        /// connected nodes instead of at the content origin.
        /// </remarks>
        Vector2 GetEndpointPosition(PortModel port, bool isFromSide)
        {
            var stateView = port.NodeModel.GetView<State>(RootView);
            if (stateView != null)
            {
                return isFromSide
                    ? stateView.GetFromPositionForTransition(TransitionModel)
                    : stateView.GetToPositionForTransition(TransitionModel);
            }

            var portView = port.GetView<Port>(RootView);
            return portView?.GetGlobalCenter() ?? Vector2.zero;
        }

        /// <inheritdoc />
        internal override VisualElement SizeElement => TransitionModel.IsSingleStateTransition ? TransitionArrow : TransitionControl;

        /// <summary>
        /// Initializes a new instance of the <see cref="Transition"/> class.
        /// </summary>
        public Transition()
        {
            Layer = -1;
            TransitionHoverDetector = new TransitionHoverDetector();
        }

        /// <inheritdoc />
        protected override void BuildPartList()
        {
            base.BuildPartList();

            if (TransitionModel.IsSingleStateTransition)
            {
                PartList.AppendPart(TransitionArrowPart.Create(transitionArrowPartName, WireModel, this, ussClassName));
            }
            else
            {
                PartList.AppendPart(TransitionControlPart.Create(transitionControlPartName, Model, this, ussClassName));
            }
        }

        /// <inheritdoc />
        protected override void PostBuildUI()
        {
            base.PostBuildUI();
            AddToClassList(ussClassName);
            EnableInClassList(ghostUssClassName, Model is IGhostWireModel);
            EnableInClassList(selfTransitionUssClassName, TransitionModel.TransitionSupportKind == TransitionSupportKind.Self);
            EnableInClassList(localTransitionUssClassName, TransitionModel.TransitionSupportKind == TransitionSupportKind.Local);
            EnableInClassList(onEnterSelectorUssClassName, TransitionModel.TransitionSupportKind == TransitionSupportKind.OnEnter);
            EnableInClassList(stateToStateSelectorUssClassName, TransitionModel.TransitionSupportKind == TransitionSupportKind.StateToState);
            this.AddPackageStylesheet("Wire.uss");

            if (TransitionModel.IsSingleStateTransition)
            {
                m_TransitionAnchorManipulator = new TransitionSupportAnchorManipulator();
                this.AddManipulator(m_TransitionHoverDetector);
            }

            m_ConditionBadge = new Label("C")
            {
                name = conditionBadgeName,
                pickingMode = PickingMode.Ignore
            };
            m_ConditionBadge.AddToClassList(conditionBadgeUssClassName);
            Add(m_ConditionBadge);
        }

        /// <inheritdoc />
        public override void UpdateUIFromModel(UpdateFromModelVisitor visitor)
        {
            base.UpdateUIFromModel(visitor);
            RefreshConditionIndicator();
        }

        /// <summary>
        /// Whether the transition support carries at least one transition with authored conditions.
        /// </summary>
        /// <param name="transitionSupport">The transition support to inspect. Null is not conditioned.</param>
        /// <returns>True when a transition has at least one condition directly under its root group.</returns>
        /// <remarks>
        /// Non-creating probe: it reads <see cref="TransitionModel.ConditionModelOrNull"/> so inspecting a
        /// wire never materializes an empty root group. A root group without sub-conditions does not count
        /// as conditioned.
        /// </remarks>
        internal static bool HasAuthoredConditions(TransitionSupportModel transitionSupport)
        {
            if (transitionSupport == null)
                return false;

            var transitions = transitionSupport.Transitions;
            for (var i = 0; i < transitions.Count; i++)
            {
                var root = transitions[i]?.ConditionModelOrNull;
                if (root != null && root.SubConditions.Count > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Refreshes the condition affordance: the conditioned USS class and the badge position.
        /// </summary>
        void RefreshConditionIndicator()
        {
            var conditioned = HasAuthoredConditions(TransitionModel);
            EnableInClassList(conditionedUssClassName, conditioned);

            if (!conditioned || m_ConditionBadge == null)
                return;

            var from = GetFrom();
            var to = GetTo();
            var middle = (from + to) * 0.5f;
            m_ConditionBadge.style.left = middle.x - k_ConditionBadgeSize * 0.5f;
            m_ConditionBadge.style.top = middle.y - k_ConditionBadgeSize * 0.5f;
        }

        /// <inheritdoc />
        public override void UpdateUISelection(UpdateSelectionVisitor visitor)
        {
            base.UpdateUISelection(visitor);
            ShowHideConnectors(true);
        }

        void ShowHideConnectors(bool forceUpdate = false)
        {
            if (!TransitionModel.IsSingleStateTransition)
            {
                var showConnectors = IsSelected() || Hovered;
                if (showConnectors && (forceUpdate || !m_ShowConnectors))
                {
                    var fromStateGuid = TransitionModel.FromNodeGuid;
                    var fromStateUI = fromStateGuid.GetView<GraphElement>(GraphView);
                    (fromStateUI as INodeWithConnector)?.ShowConnector(this);

                    var toStateGuid = TransitionModel.ToNodeGuid;
                    var toStateUI = toStateGuid.GetView<GraphElement>(GraphView);
                    (toStateUI as INodeWithConnector)?.ShowConnector(this);

                    m_ShowConnectors = true;
                }
                else if (!showConnectors && (forceUpdate || m_ShowConnectors))
                {
                    var fromStateGuid = TransitionModel.FromNodeGuid;
                    var fromStateUI = fromStateGuid.GetView<GraphElement>(GraphView);
                    (fromStateUI as INodeWithConnector)?.HideConnector(this);

                    var toStateGuid = TransitionModel.ToNodeGuid;
                    var toStateUI = toStateGuid.GetView<GraphElement>(GraphView);
                    (toStateUI as INodeWithConnector)?.HideConnector(this);

                    m_ShowConnectors = false;
                }
            }
        }

        /// <inheritdoc />
        public override void RemoveFromRootView()
        {
            var fromStateGuid = TransitionModel.FromNodeGuid;
            var fromStateUI = fromStateGuid.GetView<GraphElement>(GraphView);
            (fromStateUI as INodeWithConnector)?.HideConnector(this);

            var toStateGuid = TransitionModel.ToNodeGuid;
            var toStateUI = toStateGuid.GetView<GraphElement>(GraphView);
            (toStateUI as INodeWithConnector)?.HideConnector(this);

            base.RemoveFromRootView();
        }

        /// <inheritdoc />
        public override bool HandlePasteOperation(PasteOperation operation, string operationName, Vector2 delta, CopyPasteData copyPasteData)
        {
            var selection = GraphView.GetSelection();
            if (selection.Count == 0)
                return false;

            var destinationTransitionSupportModels = new List<TransitionSupportModel>();
            foreach (var element in selection)
            {
                if( element is TransitionSupportModel tsm)
                    destinationTransitionSupportModels.Add(tsm);
                else
                    return false;
            }

            return PasteOn(destinationTransitionSupportModels, operationName == ShortCutPasteWithoutWires.id);
        }

        internal static bool CanPasteTransitionsAsNew(CopyPasteData copyPaste)
        {
            if (copyPaste == null || copyPaste.Wires.Count == 0 || copyPaste.Nodes.Count != 0 || copyPaste.Placemats.Count != 0 || copyPaste.StickyNotes.Count != 0 || copyPaste.StickyNotes.Count != 0 || copyPaste.VariableDeclarations.Count != 0)
                return false;

            foreach (var wire in copyPaste.Wires)
            {
                if (wire is not TransitionSupportModel)
                {
                    return false;
                }
            }

            return true;
        }

        internal bool PasteAsNew()
        {
            return PasteOn(new []{TransitionModel}, true);
        }

        bool PasteOn(IReadOnlyList<TransitionSupportModel> transitionSupportModels, bool additivePaste)
        {
            using var copyPasteData = GraphView.GraphTool.ClipboardProvider.DeserializeDataFromClipboard();
            if (!CanPasteTransitionsAsNew(copyPasteData))
                return false;

            var sourceTransitionSupportModels = new List<TransitionSupportModel>();
            foreach (var wire in copyPasteData.Wires)
            {
                if( wire is TransitionSupportModel tsm)
                    sourceTransitionSupportModels.Add(tsm);
            }

            GraphView.Dispatch(new PasteTransitionSupportsCommand(pasteTransitionsAsNewCommandName, transitionSupportModels, sourceTransitionSupportModels, additivePaste));

            return true;
        }

        /// <inheritdoc />
        public override bool HasForwardsDependenciesChanged()
        {
            return m_LastUsedFromNodeModelGuid != WireModel.FromNodeGuid || m_LastUsedToNodeModelGuid != WireModel.ToNodeGuid;
        }

        /// <inheritdoc />
        public override void AddForwardDependencies()
        {
            base.AddForwardDependencies();

            m_LastUsedFromNodeModelGuid = WireModel.FromNodeGuid;
            m_LastUsedToNodeModelGuid = WireModel.ToNodeGuid;

            var uiList = new List<ChildView>();
            m_LastUsedFromNodeModelGuid.AppendAllViews(GraphView, null, uiList);
            m_LastUsedToNodeModelGuid.AppendAllViews(GraphView, null, uiList);
            foreach (var childView in uiList)
            {
                Dependencies.AddForwardDependency(childView, DependencyTypes.Geometry);
                Dependencies.AddForwardDependency(childView, DependencyTypes.Style);
            }
        }

        /// <inheritdoc />
        public override bool HasBackwardsDependenciesChanged()
        {
            return m_LastUsedFromPort != WireModel.FromNodeGuid.GetView(RootView)
                || m_LastUsedToPort != WireModel.ToNodeGuid.GetView(RootView)
                || m_LastUsedFromPortView != WireModel.FromPort?.GetView<Port>(RootView)
                || m_LastUsedToPortView != WireModel.ToPort?.GetView<Port>(RootView);
        }

        /// <inheritdoc />
        public override bool HasModelDependenciesChanged()
        {
            var transitions = TransitionModel?.Transitions;
            var index = 0;

            if (transitions != null)
            {
                for (var i = 0; i < transitions.Count; i++)
                {
                    var root = transitions[i]?.ConditionModelOrNull;
                    if (root == null)
                        continue;

                    if (index >= m_ConditionRoots.Count || m_ConditionRoots[index] != root)
                        return true;
                    index++;
                }
            }

            return index != m_ConditionRoots.Count;
        }

        /// <inheritdoc />
        public override void AddModelDependencies()
        {
            m_ConditionRoots.Clear();

            var transitions = TransitionModel?.Transitions;
            if (transitions == null)
                return;

            // The indicator reads the root group of every transition. Registering those roots keeps it
            // live while conditions are authored: adding or removing a condition marks the root changed.
            for (var i = 0; i < transitions.Count; i++)
            {
                var root = transitions[i]?.ConditionModelOrNull;
                if (root == null)
                    continue;

                Dependencies.AddModelDependency(root);
                m_ConditionRoots.Add(root);
            }
        }

        /// <inheritdoc />
        public override void AddBackwardDependencies()
        {
            base.AddBackwardDependencies();

            // When the ports move, the wire should be redrawn.
            AddDependencies(WireModel.FromNodeGuid);
            AddDependencies(WireModel.ToNodeGuid);

            m_LastUsedFromPort = WireModel.FromNodeGuid.GetView(RootView);
            m_LastUsedToPort = WireModel.ToNodeGuid.GetView(RootView);
            m_LastUsedFromPortView = AddPortDependencies(WireModel.FromPort);
            m_LastUsedToPortView = AddPortDependencies(WireModel.ToPort);

            return;

            Port AddPortDependencies(PortModel portModel)
            {
                if (portModel == null)
                    return null;

                // Execution-port transitions follow the port view's geometry, like plain wires do.
                // State ports are hidden and have no port view; state transitions use state anchors.
                var portView = portModel.GetView<Port>(RootView);
                if (portView != null)
                {
                    Dependencies.AddBackwardDependency(portView, DependencyTypes.Geometry);
                }

                return portView;
            }

            void AddDependencies(Hash128 nodeModelGuid)
            {
                if (nodeModelGuid == default)
                    return;

                var ui = nodeModelGuid.GetView(RootView);
                if (ui != null)
                {
                    // Show connectors on node.
                    Dependencies.AddBackwardDependency(ui, DependencyTypes.Style);
                    // Wire position changes with node position.
                    Dependencies.AddBackwardDependency(ui, DependencyTypes.Geometry);
                }

                if (WireModel.GraphModel.TryGetModelFromGuid(nodeModelGuid, out var model))
                {
                    ui = (model.Container as GraphElementModel)?.GetView(GraphView);
                    if (ui != null)
                    {
                        // Show connectors on node.
                        Dependencies.AddBackwardDependency(ui, DependencyTypes.Style);
                        // Wire position changes with container's position.
                        Dependencies.AddBackwardDependency(ui, DependencyTypes.Geometry);
                    }
                }
            }
        }

        /// <inheritdoc />
        public override bool Overlaps(Rect rectangle)
        {
            if (SizeElement != null)
                return SizeElement.Overlaps(this.ChangeCoordinatesTo(SizeElement, rectangle));

            return base.Overlaps(rectangle);
        }

        /// <inheritdoc />
        public override bool ContainsPoint(Vector2 localPoint)
        {
            // The transition root has no layout of its own: the rendered link lives in the size
            // element (transition control or arrow). Never fall back to the root's zero-sized rect,
            // or the content origin would stay clickable while the link is drawn elsewhere.
            if (SizeElement != null)
                return SizeElement.ContainsPoint(this.ChangeCoordinatesTo(SizeElement, localPoint));

            return base.ContainsPoint(localPoint);
        }

        /// <inheritdoc/>
        public override void SetElementLevelOfDetail(float zoom, GraphViewZoomMode newZoomMode, GraphViewZoomMode oldZoomMode)
        {
            base.SetElementLevelOfDetail(zoom, newZoomMode, oldZoomMode);

            if (TransitionControl != null)
                TransitionControl.Zoom = zoom;
        }

        /// <inheritdoc />
        public override bool CanBePartitioned()
        {
            return Model is not GhostTransitionSupportModel && base.CanBePartitioned();
        }

        /// <inheritdoc />
        public override Rect GetBoundingBox()
        {
            return SizeElement.layout;
        }
    }
}
