// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

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
  AttachNode CreateNode(Part part, string nodeName, Transform nodeTransform);

  /// <summary>Adds an existing atatch node into the part.</summary>
  /// <remarks>
  /// If the node doesn't belong to the part, then the owner will be fixed and a warning logged.
  /// Normally, it's not expected to add an attach node into a part that doesn't own it. If the node
  /// is alaready in the part, then this method does nothing.
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
  void DropNode(Part part, string nodeName);

  /// <summary>Returns a user friendly attach node representation.</summary>
  /// <remarks>It gives on the node and it's peers, which is useful when logging.</remarks>
  /// <param name="an">The node to get the string for.</param>
  /// <returns>The user friendly node description.</returns>
  string NodeId(AttachNode an);

  /// <summary>Creates an attach node form the part's config definition string.</summary>
  /// <remarks>
  /// <para>
  /// The string format is exactly the same as for the part's attach node definition. It consists of
  /// 10 parts, separated by a comma. Only the first 6 parts are mandatory, the others are optional.
  /// The format is the following:
  /// <c>Position(X,Y,Z), Orientation(X,Y,Z), Size, AttachMethod, CrossFeedAllowed, IsRigid</c>
  /// </para>
  /// <list type="bullet">
  /// <item><c>Position</c> is defined by the first 3 float numbers.</item>
  /// <item><c>Orientation</c> is defined by the next 3 float numbers.</item>
  /// <item>
  /// <c>Size</c> is an integer number starting from <c>0</c> (tiny). If the size is omitted, it's 
  /// assumed to be <c>1</c> (small). When coupling two nodes, the minumim size of the two is
  /// selected to create the actual joint.
  /// </item>
  /// <item><c>AttachMethod</c> is a node attach type, which must be <c>0</c>.</item>
  /// <item>
  /// <c>CrossFeedAllowed</c> is <c>1</c> when the resources can flow thru this node, and <c>0</c>
  /// when the flow must be forbidden. Note, that in order to enable the cross feed mode, the
  /// oppossing node must be allowing it as well. If ommited, then the value is <c>1</c>.
  /// </item>
  /// <item>
  /// <c>IsRigid</c> is <c>0</c> for the normal part joint, which allows some degree of freedom
  /// under a strong force. Value <c>1</c> will instruct to create a completely locked joint. If
  /// omitted, then the value is <c>0</c>. Note, the the rigid joint will be created if <i>any</i>
  /// of the two coupling nodes require it.
  /// </item>
  /// </list>
  /// </remarks>
  /// <param name="ownerPart">
  /// The part to parse the node for. The new node will <i>not</i> be added to this part, but the
  /// required settings from this part will be used to produce the node (e.g. the <i>rescale
  /// factor</i>).
  /// </param>
  /// <param name="def">The string to parse.</param>
  /// <param name="nodeId">The ID of the new node. Keep it unique in scope of the part.</param>
  /// <returns>
  /// The new node or <c>null</c> if parsing has failed. The created node will not be automatically
  /// added to the part.
  /// </returns>
  AttachNode ParseNodeFromString(Part ownerPart, string def, string nodeId);
}

}  // namespace
