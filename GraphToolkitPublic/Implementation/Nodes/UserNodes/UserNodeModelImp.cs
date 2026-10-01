
//Do not edit UserNodeModelImp.cs directly : this file is auto-generated. Do not edit it directly. Make changes in UserNodeModelImp.inc.cs.t4 and re-run the template. ( Right click on .tt file in Rider).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Editor.Implementation
{

    [Serializable]
    class UserNodeModelImp : NodeModel, IUserNodeModelImp
    {
        [SerializeReference]
        Node m_Node;

        Node IUserNodeModelImp.Node => m_Node;
        public Node Node => m_Node;

        Dictionary<string,INodeOption> m_NodeOptionsByName = new ();
        Dictionary<string, INodeOption> IUserNodeModelImp.NodeOptionsByName => m_NodeOptionsByName;

        // Cached from IBlackboardVariableReference on the Node, if implemented.
        string[] m_VariableReferenceOptionNames;
        System.Collections.Generic.Dictionary<string, Type> m_VariableExpectedTypes;

        public override string Title
        {
            get
            {
                if (m_Node == null)
                    return "Missing Node";
                return string.IsNullOrEmpty(m_Node.DefaultTitle) ? m_Node.GetType().Name : m_Node.DefaultTitle;
            }
        }

        /// <inheritdoc/>
        public override string[] GetVariableReferenceOptionNames() =>
            m_VariableReferenceOptionNames ?? Array.Empty<string>();

        /// <inheritdoc/>
        public override Type GetExpectedVariableType(string optionName)
        {
            if (m_VariableExpectedTypes != null && m_VariableExpectedTypes.TryGetValue(optionName, out var t))
                return t;
            return null;
        }

        protected override void OnDefineNode(NodeDefinitionScope definitionScope)
        {
            ((IUserNodeModelImp)this).CustomOnDefineNode(definitionScope);
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            m_Node?.SetImplementation(this);

            // Re-bridge IBlackboardVariableReference after deserialization
            if (m_Node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        public void InitCustomNode(Node node)
        {
            m_Node = node;
            Node.SetImplementation(this);

            // Bridge IBlackboardVariableReference to NodeModel metadata
            if (node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        public override void OnDuplicateNode(AbstractNodeModel sourceNode)
        {
            ((IUserNodeModelImp)this).CallOnEnable();
            base.OnDuplicateNode(sourceNode);
        }

        public override void OnCreateNode()
        {
            ((IUserNodeModelImp)this).CallOnEnable();
            base.OnCreateNode();

            if (m_Node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        protected override PortModel CreatePort(PortDirection direction, PortOrientation orientation, string portName, PortType portType, TypeHandle dataType, string portId, PortModelOptions options, Attribute[] attributes, PortModel parentPort)
        {
            return new PortModelImp(this, direction, orientation, portName, portType, dataType, portId, options, attributes, parentPort);
        }


    }

    partial class UserBlockNodeModelImp
    {
        [SerializeReference]
        BlockNode m_Node;

        Node IUserNodeModelImp.Node => m_Node;
        public BlockNode Node => m_Node;

        Dictionary<string,INodeOption> m_NodeOptionsByName = new ();
        Dictionary<string, INodeOption> IUserNodeModelImp.NodeOptionsByName => m_NodeOptionsByName;

        // Cached from IBlackboardVariableReference on the Node, if implemented.
        string[] m_VariableReferenceOptionNames;
        System.Collections.Generic.Dictionary<string, Type> m_VariableExpectedTypes;

        public override string Title
        {
            get
            {
                if (m_Node == null)
                    return "Missing Node";
                return string.IsNullOrEmpty(m_Node.DefaultTitle) ? m_Node.GetType().Name : m_Node.DefaultTitle;
            }
        }

        /// <inheritdoc/>
        public override string[] GetVariableReferenceOptionNames() =>
            m_VariableReferenceOptionNames ?? Array.Empty<string>();

        /// <inheritdoc/>
        public override Type GetExpectedVariableType(string optionName)
        {
            if (m_VariableExpectedTypes != null && m_VariableExpectedTypes.TryGetValue(optionName, out var t))
                return t;
            return null;
        }

        protected override void OnDefineNode(NodeDefinitionScope definitionScope)
        {
            ((IUserNodeModelImp)this).CustomOnDefineNode(definitionScope);
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            m_Node?.SetImplementation(this);

            // Re-bridge IBlackboardVariableReference after deserialization
            if (m_Node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        public void InitCustomNode(BlockNode node)
        {
            m_Node = node;
            Node.SetImplementation(this);

            // Bridge IBlackboardVariableReference to NodeModel metadata
            if (node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        public override void OnDuplicateNode(AbstractNodeModel sourceNode)
        {
            ((IUserNodeModelImp)this).CallOnEnable();
            base.OnDuplicateNode(sourceNode);
        }

        public override void OnCreateNode()
        {
            ((IUserNodeModelImp)this).CallOnEnable();
            base.OnCreateNode();

            if (m_Node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        protected override PortModel CreatePort(PortDirection direction, PortOrientation orientation, string portName, PortType portType, TypeHandle dataType, string portId, PortModelOptions options, Attribute[] attributes, PortModel parentPort)
        {
            return new PortModelImp(this, direction, orientation, portName, portType, dataType, portId, options, attributes, parentPort);
        }


    }

    partial class UserContextNodeModelImp
    {
        [SerializeReference]
        ContextNode m_Node;

        Node IUserNodeModelImp.Node => m_Node;
        public ContextNode Node => m_Node;

        Dictionary<string,INodeOption> m_NodeOptionsByName = new ();
        Dictionary<string, INodeOption> IUserNodeModelImp.NodeOptionsByName => m_NodeOptionsByName;

        // Cached from IBlackboardVariableReference on the Node, if implemented.
        string[] m_VariableReferenceOptionNames;
        System.Collections.Generic.Dictionary<string, Type> m_VariableExpectedTypes;

        public override string Title
        {
            get
            {
                if (m_Node == null)
                    return "Missing Node";
                return string.IsNullOrEmpty(m_Node.DefaultTitle) ? m_Node.GetType().Name : m_Node.DefaultTitle;
            }
        }

        /// <inheritdoc/>
        public override string[] GetVariableReferenceOptionNames() =>
            m_VariableReferenceOptionNames ?? Array.Empty<string>();

        /// <inheritdoc/>
        public override Type GetExpectedVariableType(string optionName)
        {
            if (m_VariableExpectedTypes != null && m_VariableExpectedTypes.TryGetValue(optionName, out var t))
                return t;
            return null;
        }

        protected override void OnDefineNode(NodeDefinitionScope definitionScope)
        {
            ((IUserNodeModelImp)this).CustomOnDefineNode(definitionScope);
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            m_Node?.SetImplementation(this);

            // Re-bridge IBlackboardVariableReference after deserialization
            if (m_Node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        public void InitCustomNode(ContextNode node)
        {
            m_Node = node;
            Node.SetImplementation(this);

            // Bridge IBlackboardVariableReference to NodeModel metadata
            if (node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        public override void OnDuplicateNode(AbstractNodeModel sourceNode)
        {
            ((IUserNodeModelImp)this).CallOnEnable();
            base.OnDuplicateNode(sourceNode);
        }

        public override void OnCreateNode()
        {
            ((IUserNodeModelImp)this).CallOnEnable();
            base.OnCreateNode();

            if (m_Node is IBlackboardVariableReference varRef)
            {
                m_VariableReferenceOptionNames = varRef.GetVariableReferenceOptions();
                m_VariableExpectedTypes = new System.Collections.Generic.Dictionary<string, Type>();
                foreach (var name in m_VariableReferenceOptionNames ?? Array.Empty<string>())
                    m_VariableExpectedTypes[name] = varRef.GetExpectedVariableType(name);
            }
        }

        protected override PortModel CreatePort(PortDirection direction, PortOrientation orientation, string portName, PortType portType, TypeHandle dataType, string portId, PortModelOptions options, Attribute[] attributes, PortModel parentPort)
        {
            return new PortModelImp(this, direction, orientation, portName, portType, dataType, portId, options, attributes, parentPort);
        }


    }

}
