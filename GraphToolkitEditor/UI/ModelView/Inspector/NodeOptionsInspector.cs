using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.CSO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.GraphToolkit.Editor
{
    [UnityRestricted]
    internal class NodeOptionsInspector : GraphElementFieldInspector
    {
        /// <summary>
        /// The USS class name added to a <see cref="NodeOptionsInspector"/>.
        /// </summary>
        public new static readonly string ussClassName = "ge-node-options-inspector-part";

        /// <summary>
        /// The USS class name when the node is collapsed.
        /// </summary>
        public static readonly string collapsedNodeOptionsUssClassName = ussClassName.WithUssModifier(GraphElementHelper.collapsedUssModifier);

        /// <summary>
        /// Creates a new instance of the <see cref="NodeOptionsInspector"/> class.
        /// </summary>
        /// <param name="name">The name of the part.</param>
        /// <param name="models">The models displayed in this part.</param>
        /// <param name="ownerElement">The owner of the part.</param>
        /// <param name="parentClassName">The class name of the parent.</param>
        /// <param name="filter">A filter function to select which fields are displayed in the inspector. If null, defaults to <see cref="NodeOptionsInspector.CanBeInspected"/>.</param>
        /// <returns>A new instance of <see cref="NodeOptionsInspector"/>.</returns>
        public new static NodeOptionsInspector Create(string name, IReadOnlyList<Model> models, ChildView ownerElement,
            string parentClassName, Func<FieldInfo, bool> filter = null)
        {
            return new NodeOptionsInspector(name, models, ownerElement, parentClassName, filter);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeOptionsInspector"/> class.
        /// </summary>
        /// <param name="name">The name of the part.</param>
        /// <param name="models">The models displayed in this part.</param>
        /// <param name="ownerElement">The owner of the part.</param>
        /// <param name="parentClassName">The class name of the parent.</param>
        /// <param name="filter">A filter function to select which fields are displayed in the inspector. If null, defaults to <see cref="NodeOptionsInspector.CanBeInspected"/>.</param>
        NodeOptionsInspector(string name, IReadOnlyList<Model> models, ChildView ownerElement, string parentClassName, Func<FieldInfo, bool> filter)
            : base(name, models, ownerElement, parentClassName, filter) {}

        struct OptionFieldInfo
        {
            public string name;
            public TypeHandle type;
            public bool inspectorOnly;
        }

        List<OptionFieldInfo> m_MutableFieldInfos = new List<OptionFieldInfo>();

        /// <inheritdoc />
        protected override IReadOnlyList<BaseModelPropertyField> GetFields()
        {
            var fieldList = new List<BaseModelPropertyField>();

            m_MutableFieldInfos.Clear();

            var targets = GetInspectedObjects().ToList();

            var inspectorOrderFields = new SortedDictionary<int, List<BaseModelPropertyField>>();

            AddFieldsFromNodeOptions(inspectorOrderFields, fieldList);
            AddFieldsFromTypes(targets, inspectorOrderFields, fieldList);
            GetCustomFields(fieldList);
            foreach (var fieldAtPositionList in inspectorOrderFields.Values)
            {
                fieldList.AddRange(fieldAtPositionList);
            }

            return fieldList;
        }

        void AddFieldsFromNodeOptions(SortedDictionary<int, List<BaseModelPropertyField>> inspectorOrderFields, List<BaseModelPropertyField> outFieldList)
        {
            var nodeOptionLists = GetNodeOptionsToDisplay();
            if (nodeOptionLists != null)
            {
                foreach (var nodeOptionList in nodeOptionLists)
                {
                    var order = nodeOptionList[0].Order;
                    if (order != 0)
                    {
                        AddFieldToInspectorOrderFields(order, GetFieldFromNodeOptions(nodeOptionList), inspectorOrderFields);
                        continue;
                    }

                    outFieldList.Add(GetFieldFromNodeOptions(nodeOptionList));
                }
            }

            BaseModelPropertyField GetFieldFromNodeOptions(IReadOnlyList<NodeOption> options)
            {
                var optionTitle = options[0].PortModel.Title ?? "";
                var optionId = options[0].Id;

                // Build constants and owner models for the common path
                var constants = new List<Constant>();
                var ownerModels = new List<GraphElementModel>();
                foreach (var option in options)
                {
                    constants.Add(option.PortModel.EmbeddedValue);
                    ownerModels.Add(option.PortModel);
                }

                // Check for variable-reference options via IBlackboardVariableReference
                if (options[0].PortModel.EmbeddedValue is Constant<string> &&
                    options[0].PortModel is PortModel portModel &&
                    portModel.GraphModel != null)
                {
                    NodeModel ownerNode = null;
                    if (portModel.NodeModel is NodeModel nm)
                        ownerNode = nm;
                    if (ownerNode == null && m_Models.Count > 0)
                        ownerNode = m_Models[0] as NodeModel;

                    if (ownerNode != null)
                    {
                        var varRefOptions = ownerNode.GetVariableReferenceOptionNames();
                        if (varRefOptions != null)
                        {
                            foreach (var refName in varRefOptions)
                            {
                                if (optionId == refName)
                                {
                                    var stringConstants = new List<Constant<string>>();
                                    var variableNames = new List<string>();
                                    var expectedType = ownerNode.GetExpectedVariableType(refName);

                                    foreach (var decl in portModel.GraphModel.VariableDeclarations)
                                    {
                                        if (expectedType != null)
                                        {
                                            var resolved = decl.DataType.Resolve();
                                            if (resolved == null || !expectedType.IsAssignableFrom(resolved))
                                                continue;
                                        }
                                        variableNames.Add(decl.Title ?? string.Empty);
                                    }

                                    foreach (var c in constants)
                                        if (c is Constant<string> sc) stringConstants.Add(sc);

                                    if (stringConstants.Count > 0)
                                        return new VariablePickerDropdown(OwnerRootView, variableNames, stringConstants, optionTitle, portModel.GraphModel, expectedType);
                                }
                            }
                        }
                    }
                }

                return InlineValueEditor.CreateEditorForConstants(OwnerRootView, ownerModels, constants, optionTitle);
            }
        }

        List<List<NodeOption>> GetNodeOptionsToDisplay()
        {
            var nodeOptionsDict = new Dictionary<string, List<NodeOption>>();
            var isInspectorModelView = OwnerRootView is ModelInspectorView;

            for (var i = 0; i < m_Models.Count; i++)
            {
                if (m_Models[i] is NodeModel nodeModel)
                {
                    if (i == 0)
                    {
                        foreach (var option in nodeModel.NodeOptions)
                        {
                            m_MutableFieldInfos.Add(new OptionFieldInfo
                            {
                                name = option.PortModel.Title,
                                type = option.PortModel.DataTypeHandle,
                                inspectorOnly = option.IsInInspectorOnly
                            });
                            if (!option.IsInInspectorOnly || isInspectorModelView)
                                nodeOptionsDict[option.Id] = new List<NodeOption> { option };
                        }
                        continue;
                    }

                    // If multiple models are inspected, we only want to display the node options that are present in all models.
                    foreach (var id in nodeOptionsDict.Keys.ToList())
                    {
                        var otherOptions = nodeModel.NodeOptions.Where(o =>
                            id == o.Id && o.PortModel.DataTypeHandle == nodeOptionsDict[id].First().PortModel.DataTypeHandle).ToList();
                        if (otherOptions.Any())
                            nodeOptionsDict[id].AddRange(otherOptions);
                        else
                            nodeOptionsDict.Remove(id);
                    }
                }
            }

            return nodeOptionsDict.Values.ToList();
        }

        /// <inheritdoc />
        public override void UpdateUIFromModel(UpdateFromModelVisitor visitor)
        {
            // When the node is collapsed, the node options shouldn't be displayed.
            m_Root.EnableInClassList(collapsedNodeOptionsUssClassName, m_Models[0] is ICollapsible { Collapsed: true });

            if (ShouldRebuildFields())
            {
                BuildFields();
            }

            base.UpdateUIFromModel(visitor);
        }

        bool ShouldRebuildFields()
        {
            if (m_Models.Count != 1)
                return false;
            var nodeModel = m_Models[0] as NodeModel;

            if (nodeModel == null)
                return false;

            if (nodeModel.NodeOptions.Count != m_MutableFieldInfos.Count)
                return true;

            var isInspectorModelView = OwnerRootView is ModelInspectorView;

            foreach (var oldNCurrent in nodeModel.NodeOptions.Zip(m_MutableFieldInfos, (a, b) => new { old = b, current = a }))
            {
                if (oldNCurrent.current.PortModel.Title != oldNCurrent.old.name)
                    return true;
                if (oldNCurrent.current.PortModel.DataTypeHandle != oldNCurrent.old.type)
                    return true;
                if (!isInspectorModelView)
                    if (oldNCurrent.current.IsInInspectorOnly != oldNCurrent.old.inspectorOnly)
                        return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Dropdown field for blackboard variable reference options.
    /// Renders a PopupField&lt;string&gt; populated from graph variable declarations,
    /// filtered by an optional expected type.
    /// </summary>
    internal class VariablePickerDropdown : BaseModelPropertyField
    {
        PopupField<string> m_Popup;
        readonly IReadOnlyList<Constant<string>> m_Constants;
        int m_LastVarCount;

        public VariablePickerDropdown(
            ICommandTarget commandTarget,
            List<string> variableNames,
            IReadOnlyList<Constant<string>> constants,
            string label,
            GraphModel graphModel = null,
            Type expectedType = null)
            : base(commandTarget)
        {
            m_Constants = constants;
            m_LastVarCount = variableNames.Count;

            var choices = BuildChoices(graphModel, expectedType);
            var currentValue = constants.Count > 0 ? (constants[0].Value ?? string.Empty) : string.Empty;
            int idx = choices.IndexOf(currentValue);
            if (idx < 0) idx = 0;

            m_Popup = new PopupField<string>(label ?? "", choices, idx);
            Add(m_Popup);

            m_Popup.RegisterValueChangedCallback(evt =>
            {
                foreach (var c in m_Constants) c.Value = evt.newValue;
            });

            // Subscribe to graph changes so the dropdown stays in sync
            if (graphModel != null)
            {
                RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    // Rebuild on next frame when panel is attached
                    if (graphModel != null)
                        schedule.Execute(() => Refresh(graphModel, expectedType)).Every(500);
                });
                RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    schedule.Execute(() => {}).Pause();
                });
            }
        }

        static List<string> BuildChoices(GraphModel graphModel, Type expectedType)
        {
            var choices = new List<string> { string.Empty };
            if (graphModel != null)
            {
                foreach (var decl in graphModel.VariableDeclarations)
                {
                    if (expectedType != null)
                    {
                        var resolved = decl.DataType.Resolve();
                        if (resolved == null || !expectedType.IsAssignableFrom(resolved))
                            continue;
                    }
                    choices.Add(decl.Title ?? string.Empty);
                }
            }
            return choices;
        }

        void Refresh(GraphModel graphModel, Type expectedType)
        {
            var newChoices = BuildChoices(graphModel, expectedType);
            if (newChoices.Count != m_LastVarCount)
            {
                var current = m_Constants.Count > 0 ? (m_Constants[0].Value ?? string.Empty) : string.Empty;
                int idx = newChoices.IndexOf(current);
                if (idx < 0) idx = 0;
                m_Popup.choices = newChoices;
                m_Popup.index = idx;
                m_LastVarCount = newChoices.Count;
            }
        }

        public override void UpdateDisplayedValue()
        {
            if (m_Constants.Count > 0)
            {
                var current = m_Constants[0].Value ?? string.Empty;
                if (m_Popup.value != current)
                {
                    int idx = m_Popup.choices.IndexOf(current);
                    if (idx >= 0) m_Popup.index = idx;
                }
            }
        }
    }
}
