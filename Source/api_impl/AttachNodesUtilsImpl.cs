// Kerbal Attachment System API
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.ProcessingUtils;
using System;
using UnityEngine;

// ReSharper disable UseStringInterpolation
// ReSharper disable once CheckNamespace
namespace KASImpl {

/// <summary>Implements KASAPIv2.IAttachNodesUtils.</summary>
internal class AttachNodesUtilsImpl : KASAPIv2.IAttachNodesUtils {
  /// <inheritdoc/>
  public AttachNode CreateNode(Part part, string nodeName, Transform nodeTransform) {
    ArgumentGuard.NotNull(part, "part");
    ArgumentGuard.NotNullOrEmpty(nodeName, "nodeName", context: part);
    ArgumentGuard.NotNull(part, "nodeTransform", context: part);
    // Attach node wants the local coordinates! May be due to the prefab setup.
    var localNodeTransform = new GameObject(nodeName + "-autonode").transform;
    localNodeTransform.parent = part.transform;
    localNodeTransform.position = part.transform.InverseTransformPoint(nodeTransform.position);
    localNodeTransform.rotation = part.transform.rotation.Inverse() * nodeTransform.rotation;
    localNodeTransform.localScale = Vector3.one;  // The position has already the scale applied. 
    var attachNode = part.FindAttachNode(nodeName);
    if (attachNode != null) {
      HostedDebugLog.Warning(part, "Not creating attach node, already exists: id={0}", nodeName);
    } else {
      attachNode = new AttachNode(nodeName, localNodeTransform, 0, AttachNodeMethod.FIXED_JOINT,
                                  crossfeed: true, rigid: false);
    }
    attachNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
    attachNode.nodeType = AttachNode.NodeType.Stack;
    attachNode.nodeTransform = localNodeTransform;
    attachNode.owner = part;
    AddNode(part, attachNode);
    return attachNode;
  }

  /// <inheritdoc/>
  public void AddNode(Part part, AttachNode attachNode) {
    ArgumentGuard.NotNull(part, "part");
    ArgumentGuard.NotNull(attachNode, "attachNode", context: part);
    if (attachNode.owner != part) {
      HostedDebugLog.Warning(
          part, "Former owner of the attach node doesn't match the new one: {0}", attachNode.owner);
      attachNode.owner = part;
    }
    if (part.attachNodes.IndexOf(attachNode) == -1) {
      HostedDebugLog.Fine(part, "Adding node: {0}", NodeId(attachNode));
      part.attachNodes.Add(attachNode);
    }
  }

  /// <inheritdoc/>
  public void DropNode(Part part, AttachNode attachNode) {
    ArgumentGuard.NotNull(part, "part");
    ArgumentGuard.NotNull(attachNode, "attachNode", context: part);
    if (attachNode.attachedPart != null) {
      HostedDebugLog.Error(part, "Not dropping an attached node: {0}", NodeId(attachNode));
      return;
    }
    if (part.attachNodes.IndexOf(attachNode) != -1) {
      HostedDebugLog.Fine(part, "Drop attach node: {0}", NodeId(attachNode));
      part.attachNodes.Remove(attachNode);
      attachNode.attachedPartId = 0;  // Just in case.
    }
  }

  /// <inheritdoc/>
  public string NodeId(AttachNode an) {
    return an == null
        ? "[AttachNode:NULL]"
        : string.Format(
            "[AttachNode:id={0},host={1},to={2}]",
            an.id, DebugEx.ObjectToString(an.owner), DebugEx.ObjectToString(an.attachedPart));
  }

  /// <inheritdoc/>
  public AttachNode ParseNodeFromString(Part ownerPart, string def, string nodeId) {
    ArgumentGuard.NotNull(ownerPart, "ownerPart");
    ArgumentGuard.NotNullOrEmpty(def, "def", context: ownerPart);
    ArgumentGuard.NotNullOrEmpty(nodeId, "nodeId", context: ownerPart);
    var array = def.Split(',');
    ArgumentGuard.InRange(array.Length, "def", 6, 10,
                          message: "Unexpected number of components", context: ownerPart);
    try {
      // The logic is borrowed from PartLoader.ParsePart.
      var attachNode = new AttachNode {
          owner = ownerPart,
          id = nodeId
      };
      var factor = ownerPart.rescaleFactor;
      attachNode.position = new Vector3(
          float.Parse(array[0]), float.Parse(array[1]), float.Parse(array[2])) * factor;
      attachNode.orientation = new Vector3(
          float.Parse(array[3]), float.Parse(array[4]), float.Parse(array[5])) * factor;
      attachNode.originalPosition = attachNode.position;
      attachNode.originalOrientation = attachNode.orientation;
      attachNode.size = array.Length >= 7 ? int.Parse(array[6]) : 1;
      attachNode.attachMethod = array.Length >= 8
          ? (AttachNodeMethod)int.Parse(array[7])
          : AttachNodeMethod.FIXED_JOINT;
      if (array.Length >= 9) {
        attachNode.ResourceXFeed = int.Parse(array[8]) > 0;
      }
      if (array.Length >= 10) {
        attachNode.rigid = int.Parse(array[9]) > 0;
      }
      attachNode.nodeType = AttachNode.NodeType.Stack;
      return attachNode;
    }
    catch (Exception ex) {
      HostedDebugLog.Error(ownerPart, "Cannot parse node '{0}' from: {1}\nError: {2}",
                           nodeId, def, ex.Message);
      return null;
    }
  }

  /// <inheritdoc/>
  public Transform GetTransformForNode(Part ownerPart, AttachNode an) {
    ArgumentGuard.NotNull(ownerPart, "ownerPart");
    ArgumentGuard.NotNull(an, "an", context: ownerPart);
    if (an.owner != ownerPart) {
      HostedDebugLog.Warning(
          ownerPart, "Attach node doesn't belong to the part: {0}", NodeId(an));
    }
    var partModel = Hierarchy.GetPartModelTransform(ownerPart);
    var objectName = "attachNode-" + an.id;
    var nodeTransform = partModel.Find(objectName)
        ?? new GameObject(objectName).transform;
    Hierarchy.MoveToParent(
        nodeTransform, partModel,
        newPosition: an.position / ownerPart.rescaleFactor,
        newRotation: Quaternion.LookRotation(an.orientation));
    return nodeTransform;
  }
}

}  // namespace
