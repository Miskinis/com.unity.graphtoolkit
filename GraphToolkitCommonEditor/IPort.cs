using System;
using System.Collections.Generic;

namespace Unity.GraphToolkit.Editor
{
    /// <summary>
    /// Interface for a node port.
    /// </summary>
    /// <remarks>
    /// Use this interface to access port metadata and connectivity.
    /// <br/>
    /// <br/>
    /// A port represents an input or output slot on a node and users can connect ports to other ports to form graph relationships.
    /// Each port has a unique name within its node context, a data type, and a direction.
    /// Users can only connect a port to another port if they are compatible as follows:
    /// <list type="bullet">
    /// <item><description>The ports must have opposite <see cref="PortDirection"/> (i.e., one input and one output).</description></item>
    /// <item><description>Either both ports must have the same data type or the output port must be a derived data type and the input port, a base data type.
    /// Of course the derived data type must be derived from the same base type.
    /// </description></item>
    /// </list>
    /// To retrieve the node that owns the port, use <c>INodeExtensions.GetNode</c>.
    /// </remarks>
    public interface IPort
    {
        /// <summary>
        /// Gets the data type associated with the port.
        /// </summary>
        Type dataType { get; }

        /// <summary>
        /// Gets the unique identifier name of the port.
        /// </summary>
        /// <remarks>
        /// The name is used to retrieve the port programmatically using methods like
        /// <c>Node.GetInputPortByName(string)</c> or <c>Node.GetOutputPortByName(string)</c>.
        /// It must be unique within its category (input or output) for a node.
        /// </remarks>
        string name { get; }

        /// <summary>
        /// Gets the label displayed in the UI for the port.
        /// </summary>
        string displayName { get; }

        /// <summary>
        /// Gets the direction of the port.
        /// </summary>
        /// <remarks>
        /// The direction indicates whether the port is an input or output.
        /// Use <see cref="PortDirection.Input"/> or <see cref="PortDirection.Output"/> to determine behavior.
        /// </remarks>
        PortDirection direction { get; }

        /// <summary>
        /// Indicates whether the port is currently connected to any other port.
        /// </summary>
        /// <remarks>
        /// Use this property to check when a port has at least one connection.
        /// </remarks>
        bool isConnected { get; }

        /// <summary>
        /// Gets the first port connected to this port, if any.
        /// </summary>
        /// <remarks>
        /// If multiple connections exist, only the first connected port is returned.
        /// </remarks>
        IPort firstConnectedPort { get; }

        /// <summary>
        /// Retrieves all ports connected to this port.
        /// </summary>
        /// <param name="outConnectedPorts">A list to populate with the connected ports.</param>
        /// <remarks>
        /// This method adds all connected ports to the provided list.
        /// It clears the list before adding items.
        /// <br/><br/>
        /// Ports are returned in the port's connection order — the same order shown by the wire-order
        /// bubbles on a multi-connected output port (1-based in the UI). The order is user-editable
        /// by reordering wires and is preserved across serialization.
        /// </remarks>
        void GetConnectedPorts(List<IPort> outConnectedPorts);

        /// <summary>
        /// Gets the 0-based connection order of a port connected to this port.
        /// </summary>
        /// <param name="connectedPort">A port connected to this port.</param>
        /// <returns>
        /// The 0-based connection order, or <c>-1</c> if <paramref name="connectedPort"/> is not
        /// connected to this port.
        /// </returns>
        /// <remarks>
        /// The order matches the sequence returned by <see cref="GetConnectedPorts"/> and the
        /// wire-order bubbles displayed on a multi-connected output port (1-based in the UI).
        /// </remarks>
        int GetConnectedPortOrder(IPort connectedPort);

        /// <summary>
        /// Tries to retrieve the current value assigned to the port’s UI field.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="value">When successful, contains the value assigned to the port’s field.</param>
        /// <returns>
        /// <c>true</c> if the port is not connected and a field value is available; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method is intended for editor-time inspection of an input port’s value, configured through a field
        /// displayed in the UI. If the port is connected, the field is hidden and no value is available, so the method returns <c>false</c>.
        /// If the value was never explicitly set, this method still returns <c>true</c>, and <paramref name="value"/> will contain the default
        /// value for type <typeparamref name="T"/>.
        /// </remarks>
        bool TryGetValue<T>(out T value);
    }
}
