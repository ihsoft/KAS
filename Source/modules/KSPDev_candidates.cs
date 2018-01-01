// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

using System;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils {

/// <summary>Helper methods to align transformations relative to each other.</summary>
public static class AlignTransforms2 {
  /// <summary>Aligns two vessels via the attach nodes.</summary>
  /// <remarks>
  /// The source vessel is positioned and rotated so that its attach node matches the target vessel
  /// attach node, and the nodes are "looking" at each other. 
  /// </remarks>
  /// <param name="srcAttachNode">The node of the source vessel.</param>
  /// <param name="tgtAttachNode">The node of the traget vessel.</param>
  public static void SnapAlignNodes(AttachNode srcAttachNode, AttachNode tgtAttachNode) {
    // The sequence of the calculations below is very order dependent! Don't change it.
    var srcVessel = srcAttachNode.owner.vessel;
    var srcNodeFwd = srcAttachNode.owner.transform.TransformDirection(srcAttachNode.orientation);
    var srcNodeRotation = Quaternion.LookRotation(srcNodeFwd);
    var localChildRot = srcVessel.vesselTransform.rotation.Inverse() * srcNodeRotation;
    var tgtNodeFwd = tgtAttachNode.owner.transform.TransformDirection(tgtAttachNode.orientation);
    var tgtNodePos = tgtAttachNode.owner.transform.TransformPoint(tgtAttachNode.position);
    srcVessel.SetRotation(Quaternion.LookRotation(-tgtNodeFwd) * localChildRot.Inverse());
    // The vessel position must be CALCULATED and UPDATED *after* the rotation is set, since it must
    // take into account the NEW vessel's rotation.
    var srcNodePos = srcAttachNode.owner.transform.TransformPoint(srcAttachNode.position);
    srcVessel.SetPosition(
        srcVessel.vesselTransform.position - (srcNodePos - tgtNodePos),
        usePristineCoords: true);
  }
}

}  // namespace
