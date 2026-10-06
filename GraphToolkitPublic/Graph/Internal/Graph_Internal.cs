using System;
using Unity.GraphToolkit.Editor.Implementation;

namespace Unity.GraphToolkit.Editor
{
    public partial class Graph
    {
        internal GraphModelImp m_Implementation;

        internal void SetImplementation(GraphModelImp implementation)
        {
            m_Implementation = implementation;
        }

        /// <summary>
        /// Gets the implementation model backing this graph.
        /// </summary>
        /// <returns>The <see cref="GraphModelImp"/> that owns this graph's models.</returns>
        /// <remarks>
        /// Internal fork accessor for friend assemblies (see the <c>InternalsVisibleTo</c> grants in this
        /// assembly). It exists so editor tooling can read the raw wire, transition, and condition models
        /// that the public <see cref="Graph"/> surface does not expose.
        /// </remarks>
        internal GraphModelImp GetImplementationModel()
        {
            CheckImplementation();
            return m_Implementation;
        }

        internal void CheckImplementation()
        {
            if( m_Implementation == null )
            {
                throw new InvalidOperationException("Only Graph instances returned by either GraphDatabase.LoadGraph or GraphDatabase.CreateGraph are valid.");
            }
        }
    }
}
