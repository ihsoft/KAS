// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using System;
using UnityEngine;

namespace KASImpl {

/// <summary>Implements KASAPIv1.IAttachNodesUtils.</summary>
class AttachNodesUtilsImpl : KASAPIv1.IAttachNodesUtils {
  /// <inheritdoc/>
  public AttachNode CreateAttachNode(Part part, string nodeName, Transform nodeTransform) {
    var attachNode = part.FindAttachNode(nodeName);
    if (attachNode != null) {
      DebugEx.Warning("Not creating attach node {0} for {1} - already exists", nodeName, part);
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
      DebugEx.Warning("Not dropping attach node {0} on {1} - not found", nodeName, part);
      return;
    }
    if (attachNode.attachedPart != null) {
      DebugEx.Warning(
          "Attach node {0} on {1} is attached to {2} - decouple callbacks will be impacted",
          nodeName, part, attachNode.attachedPart);
    }
    part.attachNodes.Remove(attachNode);
  }

  /// <inheritdoc/>
  public string DumpAttachNode(AttachNode an) {
    return an == null
        ? "[AttachNode:NULL]"
        : string.Format(
            "[AttachNode:id={0},host={1},to={2}]",
            an.id, DebugEx.ObjectToString(an.owner), DebugEx.ObjectToString(an.attachedPart));
  }
}

}  // namespace
