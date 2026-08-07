using System;

namespace Unity.GraphToolkit.Editor
{
    /// <summary>
    /// Implement on node types that have string options referencing blackboard variables.
    /// The Graph Toolkit inspector will render matching options as dropdowns populated
    /// from the graph's blackboard variable declarations, filtered by expected type.
    /// </summary>
    /// <remarks>
    /// Graph tools like behavior graphs use this to provide a typed variable picker
    /// instead of manual string entry for options that reference blackboard variables.
    /// </remarks>
    public interface IBlackboardVariableReference
    {
        /// <summary>
        /// Returns the names of string options that should render as blackboard variable dropdowns.
        /// These must match the names passed to <c>IOptionDefinitionContext.AddOption&lt;string&gt;()</c>.
        /// </summary>
        string[] GetVariableReferenceOptions();

        /// <summary>
        /// Returns the expected System.Type for a variable referenced by the given option name,
        /// or <c>null</c> for no filtering (show all variables).
        /// </summary>
        Type GetExpectedVariableType(string optionName);
    }
}
