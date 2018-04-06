// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.LogUtils;
using UnityEngine;

namespace KAS {

/// <summary>Module that controls a stock-alike physical joint on a KAS part.</summary>
/// <remarks>
/// The joint is rigid. It's similar to what is created between the parts, coupled in the editor.
/// </remarks>
public class KASJointRigid : AbstractLinkJoint {

  #region AbstractLinkJoint overrides
  /// <inheritdoc/>
  protected override void SetupPhysXJoints() {
    if (isCoupled) {
      return;  // We're fine with the stock joint, created by the game core.
    }
    HostedDebugLog.Fine(this, "Create a rigid link between: {0} <=> {1}", linkSource, linkTarget);
    var rigidJoint = linkSource.part.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(rigidJoint);
    rigidJoint.enablePreprocessing = true;
    rigidJoint.autoConfigureConnectedAnchor = false;
    rigidJoint.connectedBody = linkTarget.part.Rigidbody;
    rigidJoint.anchor = linkSource.part.Rigidbody.transform.InverseTransformPoint(
        GetSourcePhysicalAnchor(linkSource));
    rigidJoint.connectedAnchor = linkTarget.part.Rigidbody.transform.InverseTransformPoint(
        GetSourcePhysicalAnchor(linkSource));
    SetBreakForces(rigidJoint);
    SetCustomJoints(new[] {rigidJoint});
  }
  #endregion
}

}  // namespace
