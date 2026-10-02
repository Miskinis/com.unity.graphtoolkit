using System;
using Unity.GraphToolkit.InternalBridge;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.GraphToolkit.Editor
{
    /// <summary>
    /// VisualElement that controls how a wire is displayed. Designed to be added as a children to a <see cref="Wire"/>
    /// </summary>
    [UnityRestricted]
    internal class WireControl : VisualElement
    {
        static readonly CustomStyleProperty<float> k_WireWidthProperty = new("--wire-width");
        static readonly CustomStyleProperty<Color> k_WireColorProperty = new("--wire-color");
        static readonly Gradient k_Gradient = new Gradient();

        /// <summary>
        /// The current zoom level.
        /// </summary>
        public float Zoom
        {
            get => m_Zoom;
            set
            {
                m_Zoom = value;
                MarkDirtyRepaint();
            }
        }

        const float k_WireLengthFromPort = 12.0f;
        const float k_WireTurnDiameter = 16.0f;
        const float k_WireTurnRadius = k_WireTurnDiameter * 0.5f;

        /// <summary>
        /// Loop-back wires leave their port straight for this distance (covering the always-visible
        /// port window) before the bowed curve begins.
        /// </summary>
        const float k_LoopStraightLength = 44f;
        const float k_MinWireWidth = 1.75f;
        const float k_MinOpacity = 0.6f;

        protected Wire m_Wire;

        // Short segments of this wire drawn in a top-level overlay near each port, so the
        // connection is visibly attached to its arrows even where the node body covers the wire.
        WireEndCap m_FromEndCap;
        WireEndCap m_ToEndCap;

        float m_Zoom = 1.0f;

        /// <summary>
        /// The control points of the wire expressed in the parent <see cref="Wire"/> coordinates.
        /// </summary>
        protected Vector2[] m_ControlPoints = new Vector2[4];

        protected PortOrientation m_ToOrientation;

        protected PortOrientation m_FromOrientation;

        protected PortDirection m_FromDirection;

        protected PortDirection m_ToDirection;

        protected Color m_ToColor = Color.grey;

        protected Color m_FromColor = Color.grey;

        protected bool m_ColorOverridden;

        protected bool m_WidthOverridden;

        protected float m_LineWidth = WireUtilities.DefaultWireWidth;

        protected float StyleLineWidth { get; set; } = WireUtilities.DefaultWireWidth;

        protected Color WireColor { get; set; } = WireUtilities.DefaultWireColor;

        // The start of the wire in graph coordinates.
        protected Vector2 From => m_Wire?.GetFrom() ?? Vector2.zero;

        // The end of the wire in graph coordinates.
        protected Vector2 To => m_Wire?.GetTo() ?? Vector2.zero;

        internal PortOrientation ToOrientation
        {
            get => m_ToOrientation;
            set => m_ToOrientation = value;
        }

        internal PortOrientation FromOrientation
        {
            get => m_FromOrientation;
            set => m_FromOrientation = value;
        }


        internal PortDirection FromDirection
        {
            get => m_FromDirection;
            set => m_FromDirection = value;
        }
        internal PortDirection ToDirection
        {
            get => m_ToDirection;
            set => m_ToDirection = value;
        }

        /// <summary>
        /// The color of the wire at the input port.
        /// </summary>
        public Color InputColor
        {
            get => m_ToColor;
            private set
            {
                if (m_ToColor != value)
                {
                    m_ToColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        /// <summary>
        /// The color of the wire at the output port.
        /// </summary>
        public Color OutputColor
        {
            get => m_FromColor;
            private set
            {
                if (m_FromColor != value)
                {
                    m_FromColor = value;
                    MarkDirtyRepaint();
                }
            }
        }

        /// <summary>
        /// The width of the wire.
        /// </summary>
        public float LineWidth
        {
            get => m_LineWidth;
            set
            {
                m_WidthOverridden = true;

                if (Math.Abs(m_LineWidth - value) < 0.05)
                    return;

                m_LineWidth = value;
                UpdateLayout(); // The layout depends on the wires width
                MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WireControl"/> class.
        /// </summary>
        public WireControl(Wire wire)
        {
            m_Wire = wire;
            generateVisualContent += OnGenerateVisualContent;
            pickingMode = PickingMode.Position;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        protected void OnCustomStyleResolved(CustomStyleResolvedEvent e)
        {
            if (e.customStyle.TryGetValue(k_WireWidthProperty, out var wireWidthValue))
                StyleLineWidth = wireWidthValue;

            if (e.customStyle.TryGetValue(k_WireColorProperty, out var wireColorValue))
                WireColor = wireColorValue;

            if (!m_WidthOverridden)
            {
                m_LineWidth = StyleLineWidth;
                UpdateLayout(); // The layout depends on the wires width
                MarkDirtyRepaint();
            }

            if (!m_ColorOverridden)
            {
                m_ToColor = WireColor;
                m_FromColor = WireColor;
                MarkDirtyRepaint();
            }

            UpdateEndCapColors();
        }

        /// <summary>
        /// Resets the line width to the default value.
        /// </summary>
        public void ResetLineWidth()
        {
            m_WidthOverridden = false;
            m_LineWidth = StyleLineWidth;
        }

        /// <summary>
        /// Sets the color of the wire.
        /// </summary>
        /// <param name="inputColor">The color of the wire at the input port.</param>
        /// <param name="outputColor">The color of the wire at the output port.</param>
        public void SetColor(Color inputColor, Color outputColor)
        {
            m_ColorOverridden = true;
            InputColor = inputColor;
            OutputColor = outputColor;
            UpdateEndCapColors();
        }

        /// <summary>
        /// Resets the color of the wire to the default value.
        /// </summary>
        public void ResetColor()
        {
            m_ColorOverridden = false;
            InputColor = WireColor;
            OutputColor = WireColor;
            UpdateEndCapColors();
        }

        protected void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            DrawWire(mgc);
        }

        /// <inheritdoc />
        public override bool ContainsPoint(Vector2 localPoint)
        {
            return base.ContainsPoint(localPoint) && IsPointOnLine(this.ChangeCoordinatesTo(parent, localPoint));
        }

        /// <summary>
        /// Tests if a point is on the wire.
        /// </summary>
        /// <param name="parentPoint">The point to test.</param>
        /// <returns>True if the point lies on the wire, false otherwise.</returns>
        public bool IsPointOnLine(Vector2 parentPoint)
        {
            // Hit-test against the flattened wire path — the same geometry that is stroked and
            // that the endpoint windows use — not the straight polyline through the control
            // points. A bowed back edge arcs far away from that polyline, which made the visible
            // dashed line unclickable.
            var points = new System.Collections.Generic.List<Vector2>(64);
            BuildWirePoints(m_ControlPoints[0], m_ControlPoints[1], m_ControlPoints[2], m_ControlPoints[3],
                m_Wire != null && m_Wire.IsLoopBackWire(), points);
            return WireUtilities.IsPointOnLine(parentPoint, points.ToArray(), LineWidth + 1);
        }

        /// <inheritdoc />
        public override bool Overlaps(Rect r)
        {
            return base.Overlaps(r) && RectIntersectsLine(this.ChangeCoordinatesTo(parent, r));
        }

        /// <summary>
        /// Tests if a rectangle intersects the wire.
        /// </summary>
        /// <param name="r">The rectangle to test.</param>
        /// <returns>True if the rectangle intersects the wire, false otherwise.</returns>
        public bool RectIntersectsLine(Rect r)
        {
            return WireUtilities.RectIntersectsLine(r, m_ControlPoints);
        }

        static bool Approximately(Vector2 v1, Vector2 v2)
        {
            return Mathf.Approximately(v1.x, v2.x) && Mathf.Approximately(v1.y, v2.y);
        }

        /// <summary>
        /// Recomputes the layout of the wire.
        /// </summary>
        public void UpdateLayout()
        {
            if (parent != null)
                ComputeLayout();
        }

        static void AssignControlPoint(ref Vector2 destination, Vector2 newValue)
        {
            if (!Approximately(destination, newValue))
            {
                destination = newValue;
            }
        }

        void ComputeControlPoints()
        {
            float offset = k_WireLengthFromPort + k_WireTurnDiameter;

            // This is to ensure we don't have the wire extending
            // left and right by the offset right when the `from`
            // and `to` are on top of each other.
            float fromToDistance = (To - From).magnitude;
            offset = Mathf.Min(offset, fromToDistance * 2);
            offset = Mathf.Max(offset, k_WireTurnDiameter);

            if (m_ControlPoints == null || m_ControlPoints.Length != 4)
                m_ControlPoints = new Vector2[4];

            AssignControlPoint(ref m_ControlPoints[0], From);

            if (FromDirection == PortDirection.Output)
            {
                if (FromOrientation == PortOrientation.Horizontal)
                    AssignControlPoint(ref m_ControlPoints[1], new Vector2(From.x + offset, From.y));
                else
                    AssignControlPoint(ref m_ControlPoints[1], new Vector2(From.x, From.y + offset));
            }
            else
            {
                if (FromOrientation == PortOrientation.Horizontal)
                    AssignControlPoint(ref m_ControlPoints[1], new Vector2(From.x - offset, From.y));
                else
                    AssignControlPoint(ref m_ControlPoints[1], new Vector2(From.x, From.y - offset));
            }

            if (ToDirection == PortDirection.Input)
            {
                if (ToOrientation == PortOrientation.Horizontal)
                    AssignControlPoint(ref m_ControlPoints[2], new Vector2(To.x - offset, To.y));
                else
                    AssignControlPoint(ref m_ControlPoints[2], new Vector2(To.x, To.y - offset));
            }
            else
            {
                if (ToOrientation == PortOrientation.Horizontal)
                    AssignControlPoint(ref m_ControlPoints[2], new Vector2(To.x + offset, To.y));
                else
                    AssignControlPoint(ref m_ControlPoints[2], new Vector2(To.x, To.y + offset));
            }

            AssignControlPoint(ref m_ControlPoints[3], To);
        }

        void ComputeLayout()
        {
            ComputeControlPoints();

            // Compute VisualElement position and dimension.
            var wireModel = m_Wire?.WireModel;

            if (wireModel == null)
            {
                style.top = 0;
                style.left = 0;
                style.width = 0;
                style.height = 0;
                return;
            }

            // Bounds over the flattened path: covers the bowed back edge, whose curve extends
            // beyond the plain control-point box.
            var pathPoints = new System.Collections.Generic.List<Vector2>(64);
            BuildWirePoints(m_ControlPoints[0], m_ControlPoints[1], m_ControlPoints[2], m_ControlPoints[3],
                m_Wire != null && m_Wire.IsLoopBackWire(), pathPoints);

            Vector2 min = pathPoints[0];
            Vector2 max = pathPoints[0];

            for (int i = 1; i < pathPoints.Count; ++i)
            {
                min.x = Math.Min(min.x, pathPoints[i].x);
                min.y = Math.Min(min.y, pathPoints[i].y);
                max.x = Math.Max(max.x, pathPoints[i].x);
                max.y = Math.Max(max.y, pathPoints[i].y);
            }

            var grow = LineWidth / 2.0f;
            min.x -= grow;
            max.x += grow;
            min.y -= grow;
            max.y += grow;

            var dim = max - min;
            style.left = min.x;
            style.top = min.y;
            style.width = dim.x;
            style.height = dim.y;

            SyncEndCaps();
        }

        static readonly string k_EndCapsContainerName = "wire-end-caps";

        /// <summary>Length of wire, in graph units, made visible at each port by the overlay.</summary>
        const float k_EndCapLength = 44f;

        /// <summary>
        /// Keeps the two always-visible endpoint stubs in sync with the wire path. The stubs live
        /// in a dedicated container at the top of the graph view's content, above the node layer,
        /// so a fraction of every connection is visible at its arrows even when the wire itself
        /// runs under a node.
        /// </summary>
        void SyncEndCaps()
        {
            var content = m_Wire?.GraphView?.ContentViewContainer;
            if (content == null || m_ControlPoints == null || m_ControlPoints.Length != 4)
                return;

            var container = content.Q(k_EndCapsContainerName);
            if (container == null)
            {
                container = new VisualElement { name = k_EndCapsContainerName, pickingMode = PickingMode.Ignore };
                container.style.position = Position.Absolute;
                container.style.left = 0;
                container.style.top = 0;
                content.Add(container);
            }

            if (m_FromEndCap == null)
                m_FromEndCap = new WireEndCap();
            if (m_FromEndCap.parent == null)
                container.Add(m_FromEndCap);

            if (m_ToEndCap == null)
                m_ToEndCap = new WireEndCap();
            if (m_ToEndCap.parent == null)
                container.Add(m_ToEndCap);

            var parentElement = parent;
            if (parentElement == null)
                return;

            // Control points live in the Wire element's local space; the caps live in the graph
            // content's space. Convert before flattening — otherwise the windows render offset
            // copies of the wire (parallel duplicate lines).
            var c0 = parentElement.ChangeCoordinatesTo(content, m_ControlPoints[0]);
            var c1 = parentElement.ChangeCoordinatesTo(content, m_ControlPoints[1]);
            var c2 = parentElement.ChangeCoordinatesTo(content, m_ControlPoints[2]);
            var c3 = parentElement.ChangeCoordinatesTo(content, m_ControlPoints[3]);

            var width = EffectiveWidth();
            var isLoopBack = m_Wire != null && m_Wire.IsLoopBackWire();
            var points = new System.Collections.Generic.List<Vector2>(64);
            BuildWirePoints(c0, c1, c2, c3, isLoopBack, points);

            m_FromEndCap.Set(points, true, k_EndCapLength, OutputColor, width, isLoopBack);
            m_ToEndCap.Set(points, false, k_EndCapLength, InputColor, width, isLoopBack);
        }

        /// <summary>
        /// The stroke width actually used for this wire at the current zoom (matches DrawWire's
        /// minimum-width clamp), so the endpoint stubs line up with the wire.
        /// </summary>
        float EffectiveWidth()
        {
            float width = m_WidthOverridden ? LineWidth : StyleLineWidth;
            if (width * Zoom < k_MinWireWidth)
                width = k_MinWireWidth / Zoom;
            return width;
        }

        void UpdateEndCapColors()
        {
            if (m_FromEndCap != null)
                m_FromEndCap.SetColor(OutputColor);
            if (m_ToEndCap != null)
                m_ToEndCap.SetColor(InputColor);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            // The wire left the tree (culled or the graph rebuilt): its endpoint stubs must not
            // linger as floating segments in the overlay.
            m_FromEndCap?.RemoveFromHierarchy();
            m_ToEndCap?.RemoveFromHierarchy();
            m_FromEndCap = null;
            m_ToEndCap = null;
        }

        static GradientColorKey[] s_ColorKeys = new GradientColorKey[2];
        static GradientAlphaKey[] s_AlphaKeys = new GradientAlphaKey[1];

        protected void DrawWire(MeshGenerationContext mgc)
        {
            UnityEngine.Profiling.Profiler.BeginSample("DrawWire");

            if (LineWidth <= 0)
                return;

            Color inColor = InputColor;
            Color outColor = OutputColor;

            inColor *= this.GetPlayModeTintColor();
            outColor *= this.GetPlayModeTintColor();

            var painter2D = mgc.painter2D;

            float width = m_WidthOverridden ? LineWidth : StyleLineWidth;
            float alpha = 1.0f;

            if (width * Zoom < k_MinWireWidth)
            {
                float t = width * Zoom / k_MinWireWidth;

                alpha = Mathf.Lerp(k_MinOpacity, 1.0f, t);
                width = k_MinWireWidth / Zoom;
            }

            s_ColorKeys[0] = new GradientColorKey(outColor, 0);
            s_ColorKeys[1] = new GradientColorKey(inColor, 1);

            s_AlphaKeys[0] = new GradientAlphaKey(alpha, 0);

            k_Gradient.SetKeys(s_ColorKeys, s_AlphaKeys);
            painter2D.BeginPath();
            painter2D.strokeGradient = k_Gradient;

            var localToWorld = parent.worldTransform;
            var worldToLocal = this.GetWorldTransformInverse();

            Vector2 ChangeCoordinates(Vector2 point)
            {
                Vector2 res;
                res.x = localToWorld.m00 * point.x + localToWorld.m01 * point.y + localToWorld.m03;
                res.y = localToWorld.m10 * point.x + localToWorld.m11 * point.y + localToWorld.m13;

                Vector2 res2;
                res2.x = worldToLocal.m00 * res.x + worldToLocal.m01 * res.y + worldToLocal.m03;
                res2.y = worldToLocal.m10 * res.x + worldToLocal.m11 * res.y + worldToLocal.m13;

                return res2;
            }

            Vector2 p1 = ChangeCoordinates(m_ControlPoints[0]);
            Vector2 p2 = ChangeCoordinates(m_ControlPoints[1]);
            Vector2 p3 = ChangeCoordinates(m_ControlPoints[2]);
            Vector2 p4 = ChangeCoordinates(m_ControlPoints[3]);

            var isLoopBack = m_Wire != null && m_Wire.IsLoopBackWire();

            painter2D.lineWidth = width;

            // Loop-back wires are dashed — the flowchart convention for a return edge — so cycles
            // read as loops instead of looking like a plain connection.
            if (isLoopBack)
                painter2D.SetDashPattern(width * 4f, width * 2.5f);

            BuildWirePath(painter2D, p1, p2, p3, p4, isLoopBack);

            painter2D.Stroke();

            UnityEngine.Profiling.Profiler.EndSample();
        }

        /// <summary>
        /// Builds (but does not stroke) the wire path for the given control points. Shared with
        /// <see cref="WireEndCap"/>, which re-strokes the same path clipped to a small window
        /// around each port, so the always-visible end segments match the wire exactly — same
        /// curvature, same dash phase — instead of being straight stubs that create corners.
        /// </summary>
        internal static void BuildWirePath(Painter2D painter2D, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, bool loopBack)
        {
            painter2D.MoveTo(p1);

            if (loopBack)
            {
                // Straight out of the port for the always-visible lead-in, then a smooth bowed
                // curve to the target's straight lead-in. The first handle is collinear with the
                // lead-in, so the curve starts with zero lateral tangent — no corner where the
                // straight piece ends.
                var loop = ComputeLoopPath(p1, p2, p3, p4);
                painter2D.LineTo(loop.S0);
                painter2D.BezierCurveTo(loop.C1A, loop.C2A, loop.M);
                painter2D.BezierCurveTo(loop.C1B, loop.C2B, loop.S1);
                painter2D.LineTo(p4);
                return;
            }

            var threshold = Vector2.Distance(p1, p2) + Vector2.Distance(p3, p4);
            if (Vector2.Distance(p1, p4) < threshold || Vector2.Distance(p2, p3) < k_WireTurnDiameter)
            {
                painter2D.LineTo(p4);
                return;
            }

            var slopeDirection = (p2 - p3).normalized;
            painter2D.BezierCurveTo(p2, p2, p2 - slopeDirection * k_WireTurnRadius);
            painter2D.LineTo(p3 + slopeDirection * k_WireTurnRadius);
            painter2D.BezierCurveTo(p3, p3, p4);
        }

        /// <summary>
        /// Flattens the wire path (same geometry as <see cref="BuildWirePath"/>) into a polyline
        /// in the given coordinate space, so the endpoint windows can trim it to the portion near
        /// a port without a clip stack.
        /// </summary>
        internal static void BuildWirePoints(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, bool loopBack, System.Collections.Generic.List<Vector2> points)
        {
            points.Clear();
            points.Add(p1);

            if (loopBack)
            {
                var loop = ComputeLoopPath(p1, p2, p3, p4);
                points.Add(loop.S0);
                AppendCubic(points, loop.S0, loop.C1A, loop.C2A, loop.M);
                AppendCubic(points, loop.M, loop.C1B, loop.C2B, loop.S1);
                points.Add(p4);
                return;
            }

            var threshold = Vector2.Distance(p1, p2) + Vector2.Distance(p3, p4);
            if (Vector2.Distance(p1, p4) < threshold || Vector2.Distance(p2, p3) < k_WireTurnDiameter)
            {
                points.Add(p4);
                return;
            }

            var slopeDirection = (p2 - p3).normalized;
            var turnStart = p2 - slopeDirection * k_WireTurnRadius;
            var turnEnd = p3 + slopeDirection * k_WireTurnRadius;
            AppendCubic(points, p1, p2, p2, turnStart);
            points.Add(turnEnd);
            AppendCubic(points, turnEnd, p3, p3, p4);
        }

        static void AppendCubic(System.Collections.Generic.List<Vector2> points, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var roughLength = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
            var samples = Mathf.Clamp(Mathf.CeilToInt(roughLength / 4f), 6, 48);
            for (var i = 1; i <= samples; i++)
            {
                var t = i / (float)samples;
                var u = 1f - t;
                points.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        /// <summary>
        /// Control geometry of a loop-back wire: straight lead-ins at both ports and two smoothly
        /// joined cubics bowing through <see cref="M"/>.
        /// </summary>
        struct LoopPath
        {
            public Vector2 S0;   // end of the straight lead-in at the source port
            public Vector2 C1A;  // first cubic: collinear with the lead-in (smooth departure)
            public Vector2 C2A;
            public Vector2 M;    // bow apex
            public Vector2 C1B;  // second cubic
            public Vector2 C2B;
            public Vector2 S1;   // start of the straight lead-in at the target port
        }

        /// <summary>
        /// Builds the bowed loop geometry from the base control points: <paramref name="p0"/> and
        /// <paramref name="p4"/> are the port positions, <paramref name="fromHandle"/> and
        /// <paramref name="toHandle"/> the port-axis handles that define the stub directions.
        /// The wire runs straight for <see cref="k_LoopStraightLength"/> units at each end (the
        /// always-visible port windows) and curves only in between.
        /// </summary>
        static LoopPath ComputeLoopPath(Vector2 p0, Vector2 fromHandle, Vector2 toHandle, Vector2 p4)
        {
            var fromDir = (fromHandle - p0).normalized;
            var toDir = (toHandle - p4).normalized;

            var s0 = p0 + fromDir * Mathf.Max(Vector2.Distance(fromHandle, p0), k_LoopStraightLength);
            var s1 = p4 + toDir * Mathf.Max(Vector2.Distance(toHandle, p4), k_LoopStraightLength);

            var travel = s1 - s0;
            var distance = travel.magnitude;
            var direction = distance > 1f ? travel / distance : fromDir;
            var perpendicular = new Vector2(direction.y, -direction.x);
            var bow = Mathf.Clamp(distance * 0.22f, 48f, 180f);
            var m = (s0 + s1) * 0.5f + perpendicular * bow;

            var handle = Mathf.Clamp(distance * 0.3f, 40f, 160f);

            var result = default(LoopPath);
            result.S0 = s0;
            result.S1 = s1;
            result.M = m;
            result.C1A = s0 + fromDir * handle;   // smooth: tangent continues the lead-in
            result.C2A = m + fromDir * handle;    // arrival at the apex along -fromDir
            result.C1B = m - fromDir * handle;    // C1 join at the apex
            result.C2B = s1 + toDir * handle;     // arrival aligned with the target lead-in
            return result;
        }
    }

    /// <summary>
    /// A small always-visible window of a wire around one of its ports, drawn in a top-level
    /// overlay (above the node layer). The cap re-strokes the wire's own path clipped to the
    /// window, so it overlays seamlessly: same curvature, width, color, and dash phase. Where the
    /// wire is covered by a node, the cap makes a fraction of it visible at the arrows; where the
    /// wire is already visible, the cap is invisible.
    /// </summary>
    internal class WireEndCap : VisualElement
    {
        readonly System.Collections.Generic.List<Vector2> m_LocalPoints = new();
        Color m_Color = Color.white;
        float m_Width;
        bool m_Dashed;

        public WireEndCap()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>
        /// Rebuilds the window from a flattened wire path (content coordinates): keeps the portion
        /// within <paramref name="length"/> of the requested end of the wire, so it overlays the
        /// stroked wire exactly — same curve, width, color, and dash pattern.
        /// </summary>
        public void Set(System.Collections.Generic.IReadOnlyList<Vector2> path, bool fromSide, float length, Color color, float width, bool dashed)
        {
            var trimmed = new System.Collections.Generic.List<Vector2>();
            if (fromSide)
            {
                trimmed.Add(path[0]);
                var remaining = length;
                for (var i = 1; i < path.Count && remaining > 0f; i++)
                {
                    var segment = path[i] - path[i - 1];
                    var segmentLength = segment.magnitude;
                    if (segmentLength <= remaining)
                    {
                        trimmed.Add(path[i]);
                        remaining -= segmentLength;
                    }
                    else
                    {
                        trimmed.Add(path[i - 1] + segment / segmentLength * remaining);
                        remaining = 0f;
                    }
                }
            }
            else
            {
                trimmed.Add(path[path.Count - 1]);
                var remaining = length;
                for (var i = path.Count - 2; i >= 0 && remaining > 0f; i--)
                {
                    var segment = path[i] - path[i + 1];
                    var segmentLength = segment.magnitude;
                    if (segmentLength <= remaining)
                    {
                        trimmed.Add(path[i]);
                        remaining -= segmentLength;
                    }
                    else
                    {
                        trimmed.Add(path[i + 1] + segment / segmentLength * remaining);
                        remaining = 0f;
                    }
                }
            }

            var min = trimmed[0];
            var max = trimmed[0];
            for (var i = 1; i < trimmed.Count; i++)
            {
                min = Vector2.Min(min, trimmed[i]);
                max = Vector2.Max(max, trimmed[i]);
            }

            // Pad by the stroke width so a straight segment (zero-height bounding box) still has
            // a renderable element and the stroke is never clipped by the element bounds.
            var pad = Mathf.Max(width, 4f);
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);

            style.left = min.x;
            style.top = min.y;
            style.width = max.x - min.x;
            style.height = max.y - min.y;

            m_LocalPoints.Clear();
            foreach (var point in trimmed)
                m_LocalPoints.Add(point - min);

            m_Color = color;
            m_Width = width;
            m_Dashed = dashed;
            MarkDirtyRepaint();
        }

        public void SetColor(Color color)
        {
            m_Color = color;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (m_Width <= 0f || m_LocalPoints.Count < 2)
                return;

            var painter = mgc.painter2D;
            painter.BeginPath();
            painter.lineWidth = m_Width;
            painter.strokeColor = m_Color;
            painter.lineCap = LineCap.Round;
            if (m_Dashed)
                painter.SetDashPattern(m_Width * 4f, m_Width * 2.5f);

            painter.MoveTo(m_LocalPoints[0]);
            for (var i = 1; i < m_LocalPoints.Count; i++)
                painter.LineTo(m_LocalPoints[i]);
            painter.Stroke();
        }
    }
}
