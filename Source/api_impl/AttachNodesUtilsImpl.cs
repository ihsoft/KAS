// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASImpl {

/// <summary>Implements KASAPIv1.IAttachNodesUtils.</summary>
class AttachNodesUtilsImpl : KASAPIv1.IAttachNodesUtils {
  /// <inheritdoc/>
  public AttachNode CreateAttachNode(Part part, string nodeName, Transform nodeTransform) {
    var attachNode = part.FindAttachNode(nodeName);
    if (attachNode != null) {
      Debug.LogWarningFormat(
          "Not creating attach node {0} for {1} - already exists", nodeName, part.name);
    } else {
      attachNode = new AttachNode(nodeName, nodeTransform, 0, AttachNodeMethod.FIXED_JOINT,
                                  crossfeed: true, rigid: false);
      part.attachNodes.Add(attachNode);
    }
    attachNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
    attachNode.nodeType = AttachNode.NodeType.Stack;
    attachNode.nodeTransform = nodeTransform;
    attachNode.owner = part;
    return attachNode;
  }

  /// <inheritdoc/>
  public void DropAttachNode(Part part, string nodeName) {
    var attachNode = part.FindAttachNode(nodeName);
    if (attachNode == null) {
      Debug.LogWarningFormat(
          "Not dropping attach node {0} on {1} - not found", nodeName, part.name);
      return;
    }
    if (attachNode.attachedPart != null) {
      Debug.LogWarningFormat(
          "Attach node {0} on {1} is attached to {2} - decouple callbacks will be impacted",
          nodeName, part.name, attachNode.attachedPart.name);
    }
    part.attachNodes.Remove(attachNode);
  }
}

}  // namespace
