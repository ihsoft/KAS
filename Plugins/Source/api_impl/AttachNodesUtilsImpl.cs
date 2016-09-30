// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KSPDev.ModelUtils;
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
      attachNode.nodeTransform.gameObject.DestroyGameObject();
    }
    var nodeTransform = new GameObject().transform;
    attachNode.nodeTransform = nodeTransform;
    //nodeTransform.parent = attachNode.owner.transform;
    nodeTransform.parent = Hierarchy.GetPartModelTransform(attachNode.owner);
    nodeTransform.localPosition = attachNode.position;
    nodeTransform.localScale = Vector3.one;
    nodeTransform.localRotation = Quaternion.LookRotation(attachNode.orientation);
    return nodeTransform;
  }

  /// <inheritdoc/>
  public AttachNode CreateAttachNode(Part part, string nodeName, Transform nodeTransform) {
    var attachNode = part.findAttachNode(nodeName);
    if (attachNode != null) {
      Debug.LogWarningFormat(
          "Not creating attach node {0} for {1} - already exists", nodeName, part.name);
    } else {
      attachNode = new AttachNode(nodeName, nodeTransform, 0, AttachNodeMethod.FIXED_JOINT);
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
    var attachNode = part.findAttachNode(nodeName);
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
