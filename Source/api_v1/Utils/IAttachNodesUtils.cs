// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

// Name of the namespace denotes the API version.
namespace KASAPIv1 {

/// <summary>Various methods to deal with part's attach nodes.</summary>
public interface IAttachNodesUtils {
  /// <summary>Creates a new attach node on the part.</summary>
  /// <remarks>
  /// It's expected there is no node with the same name on the part already. If there is one, then
  /// no extra node will be created, and the properties of the existing node will be updated instead
  /// (see below). However, it's an unexpected situation, so a warning record will be logged.
  /// <para>
  /// The node will have the following properties:
  /// <list type="bullet">
  /// <item>Size is "small".</item>
  /// <item>Attach method is <c>FIXED_JOINT</c>.</item>
  /// <item>Node type is <c>Stack</c>.</item>
  /// </list>
  /// </para>
  /// </remarks>
  /// <param name="part">Part to create node for.</param>
  /// <param name="nodeName">Name of the node to create.</param>
  /// <param name="nodeTransform">Transform that specifies node position and orientation.</param>
  /// <returns>New attach node atatched to the part.</returns>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_attach_node.html">
  /// KSP: AttachNode</seealso>
  /// <seealso href="https://kerbalspaceprogram.com/api/_attach_node_8cs.html#ad750801f509bb71dc93caffbca90ad3d">
  /// KSP: AttachNodeMethod</seealso>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_attach_node.html#a96e7fbc9722efd10a0e225bb6a6778cc">
  /// KSP: AttachNode.NodeType</seealso>
  AttachNode CreateAttachNode(Part part, string nodeName, Transform nodeTransform);

  /// <summary>Adds an existing atatch node into the part.</summary>
  /// <remarks>
  /// If the node doesn't belong to the part, then the owner will be fixed and a warning logged.
  /// Normally, it's not expected to add an attach node into part that doesn't own it.
  /// </remarks>
  /// <param name="part">The part to add the node into.</param>
  /// <param name="attachNode">The attach node to add.</param>
  void AddNode(Part part, AttachNode attachNode);

  /// <summary>Drops the attach node on the part.</summary>
  /// <remarks>
  /// Don't drop an connected node until the part is decoupled. Otherwise,
  /// decouple callback (<see cref="IActivateOnDecouple"/>) won't be called on the part.
  /// </remarks>
  /// <param name="part">Part to drop node on.</param>
  /// <param name="nodeName">Name of the node to drop.</param>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_attach_node.html">
  /// KSP: AttachNode</seealso>
  /// <seealso href="https://kerbalspaceprogram.com/api/interface_i_activate_on_decouple.html">
  /// KSP: IActivateOnDecouple</seealso>
  void DropAttachNode(Part part, string nodeName);

  /// <summary>Returns a user friendly attach node representation.</summary>
  /// <remarks>It gives on the node and it's peers, which is useful when logging.</remarks>
  /// <param name="an">The node to get the string for.</param>
  /// <returns>The user friendly node description.</returns>
  string DumpAttachNode(AttachNode an);
}

}  // namespace
