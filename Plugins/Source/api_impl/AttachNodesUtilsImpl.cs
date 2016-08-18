// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

namespace KASImpl {

class AttachNodesUtilsImpl : KASAPIv1.IAttachNodesUtils {
  /// <inheritdoc/>
  public AttachNode GetAttachNode(Part part, string attachNodeName) {
    var attachNode = part.findAttachNode(attachNodeName);
    if (attachNode != null && attachNode.owner == null) {
      Debug.LogWarningFormat("Fix null owner on part {0}, attach node {1}",
                             part.name, attachNodeName);
      attachNode.owner = part;
    }
    return attachNode;
  }
    
  /// <inheritdoc/>
  public Transform GetOrCreateNodeTransform(Part part, string attachNodeName) {
    var attachNode = GetAttachNode(part, attachNodeName);
    if (attachNode == null) {
      Debug.LogWarningFormat("Cannot find attach node {0} on part {1}", attachNodeName, part.name);
      return part.transform;
    }
    return GetOrCreateNodeTransform(attachNode);
  }

  /// <inheritdoc/>
  public Transform GetOrCreateNodeTransform(AttachNode attachNode) {
    if (attachNode.nodeTransform != null) {
      return attachNode.nodeTransform;
    }
    var nodeTransform = new GameObject().transform;
    nodeTransform.parent = attachNode.owner.transform;
    nodeTransform.localPosition = attachNode.position;
    nodeTransform.localRotation = Quaternion.LookRotation(attachNode.orientation);
    return nodeTransform;
  }
}

}  // namespace
