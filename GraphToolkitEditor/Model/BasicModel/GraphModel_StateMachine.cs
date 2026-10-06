using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Editor
{
    abstract partial class GraphModel
    {
        static readonly (string, ConditionModelFactory)[] k_DefaultConditionTypes = { ("Add Group Condition", _ => new GroupConditionModel()) };

        readonly List<(string label, ConditionModelFactory factory)> m_RegisteredConditionTypes = new();

        /// <summary>
        /// A delegate to create a new condition model.
        /// </summary>
        /// <param name="parent">The <see cref="GroupConditionModel"/> in which this condition will be created.</param>
        public delegate ConditionModel ConditionModelFactory(GroupConditionModel parent);

        /// <summary>
        /// Returns a list of condition types that can be added to the graph as well as the add menu label for each.
        /// </summary>
        /// <returns>A list of condition types that can be added to the graph as well as the add menu label for each.</returns>
        public virtual IReadOnlyList<(string,ConditionModelFactory)> GetAddConditionOptions()
        {
            if (m_RegisteredConditionTypes.Count == 0)
                return k_DefaultConditionTypes;

            var options = new List<(string, ConditionModelFactory)>(k_DefaultConditionTypes.Length + m_RegisteredConditionTypes.Count);
            options.AddRange(k_DefaultConditionTypes);
            options.AddRange(m_RegisteredConditionTypes);
            return options;
        }

        /// <summary>
        /// Registers a condition type that can be added to transitions of this graph.
        /// </summary>
        /// <param name="label">The label shown for the condition type in the "add condition" menu. Must be unique per registration.</param>
        /// <param name="factory">The factory that creates the condition model.</param>
        /// <remarks>
        /// Registration is scoped to this <see cref="GraphModel"/> instance and is not serialized: it does not
        /// survive a domain reload or a graph reload, and it is never shared with other graphs. Consumers own
        /// re-registration after reload; the natural place is the owning <c>Graph.OnEnable</c>, which runs for
        /// the graph instance that owns this model. Registering a label that is already registered replaces its
        /// factory, so re-registration is idempotent.
        /// </remarks>
        internal void RegisterConditionType(string label, ConditionModelFactory factory)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("A condition type label is required.", nameof(label));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            for (var i = 0; i < m_RegisteredConditionTypes.Count; i++)
            {
                if (m_RegisteredConditionTypes[i].label == label)
                {
                    m_RegisteredConditionTypes[i] = (label, factory);
                    return;
                }
            }

            m_RegisteredConditionTypes.Add((label, factory));
        }

        /// <summary>
        /// Unregisters the condition type registered with the given label.
        /// </summary>
        /// <param name="label">The label passed to <see cref="RegisterConditionType"/>.</param>
        internal void UnregisterConditionType(string label)
        {
            for (var i = m_RegisteredConditionTypes.Count - 1; i >= 0; i--)
            {
                if (m_RegisteredConditionTypes[i].label == label)
                    m_RegisteredConditionTypes.RemoveAt(i);
            }
        }

        /// <summary>
        /// Removes every condition type registered on this graph model, restoring the default add-condition options.
        /// </summary>
        /// <remarks>
        /// Intended for tests and for consumers that re-register their condition types wholesale.
        /// </remarks>
        internal void ClearConditionTypes()
        {
            m_RegisteredConditionTypes.Clear();
        }

        /// <summary>
        /// Creates a transition support wire between two ports and adds it to the graph.
        /// </summary>
        /// <param name="toPort">The port to which the transition goes.</param>
        /// <param name="toStateAnchorSide">The side of the state to which the transition goes.</param>
        /// <param name="toStateAnchorOffset">The offset of the state to which the transition goes.</param>
        /// <param name="fromPort">The port from which the transition originates.</param>
        /// <param name="fromStateAnchorSide">The side of the state from which the transition originates.</param>
        /// <param name="fromStateAnchorOffset">The offset of the state from which the transition originates.</param>
        /// <param name="transitionSupportKind">The kind of transition to create.</param>
        /// <param name="guid">The guid to assign to the newly created item.</param>
        /// <returns>The newly created wire</returns>
        public virtual TransitionSupportModel CreateTransitionSupport(
            PortModel toPort, AnchorSide toStateAnchorSide, float toStateAnchorOffset,
            PortModel fromPort, AnchorSide fromStateAnchorSide, float fromStateAnchorOffset,
            TransitionSupportKind transitionSupportKind, Hash128 guid = default)
        {
            var transitionType = GetTransitionType(toPort, fromPort, transitionSupportKind);
            if (transitionType == null)
                return null;

            var transitionSupport = CreateWire(transitionType, toPort, fromPort, false, guid) as TransitionSupportModel;
            if (transitionSupport != null)
            {
                transitionSupport.SetFromAnchor(fromStateAnchorSide, fromStateAnchorOffset);
                transitionSupport.SetToAnchor(toStateAnchorSide, toStateAnchorOffset);
                transitionSupport.TransitionSupportKind = transitionSupportKind;

                // CreateWire may already have added the default transition (see GraphModelImp.CreateWire).
                if (transitionSupport.Transitions.Count == 0)
                {
                    var transition = transitionSupport.CreateTransition();
                    transitionSupport.AddTransition(transition);
                }
            }
            return transitionSupport;
        }

        /// <summary>
        /// Registers a condition that has been added in this <see cref="GraphModel"/>.
        /// </summary>
        /// <param name="conditionModel">The condition to register.</param>
        public void RegisterCondition(ConditionModel conditionModel)
        {
            RegisterElement(conditionModel);
        }

        /// <summary>
        /// Unregisters a condition that is removed from this <see cref="GraphModel"/>.
        /// </summary>
        /// <param name="conditionModel">The condition to unregister.</param>
        public void UnregisterCondition(ConditionModel conditionModel)
        {
            UnregisterElement(conditionModel);
        }

        /// <summary>
        /// Registers a transition that has been added in this <see cref="GraphModel"/>.
        /// </summary>
        /// <param name="transitionModel">The transition to register</param>
        public void RegisterTransition(TransitionModel transitionModel)
        {
            RegisterElement(transitionModel);
        }

        /// <summary>
        /// Unregisters a transition that is removed from this <see cref="GraphModel"/>.
        /// </summary>
        /// <param name="transitionModel">The transition to unregister.</param>
        public void UnregisterTransition(TransitionModel transitionModel)
        {
            UnregisterElement(transitionModel);
        }
    }
}
