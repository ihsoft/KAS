// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.LogUtils;

namespace KAS {

/// <summary>Module that controls a stock-alike physical joint on a KAS part.</summary>
/// <remarks>This module handles all the stock attach node settings.</remarks>
public class KASJointRigid : AbstractJoint {

  #region AbstractLinkJoint overrides
  /// <inheritdoc/>
  protected override void SetupPhysXJoints() {
    if (isCoupled) {
      MaybeCreateStockJoint();
    } else {
      CreateCustomJoint();
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// Creates a stock joint between the coupled parts, given there is none already created.
  /// </summary>
  /// <remarks>The created joint (if any) is populated to the hosting part.</remarks>
  void MaybeCreateStockJoint() {
    if (linkTarget.part.attachJoint == null) {
      HostedDebugLog.Fine(
          this, "Create a stock joint between: {0} <=> {1}", linkSource, linkTarget);
      linkTarget.part.CreateAttachJoint(AttachModes.STACK);
    }
  }

  /// <summary>Creates a stock-alike joint between the unrealted parts.</summary>
  /// <remarks>The physical joints will be controlled by the module.</remarks>
  void CreateCustomJoint() {
    HostedDebugLog.Fine(
        this, "Create a stock-alike joint between: {0} <=> {1}", linkSource, linkTarget);
    var stockJoint = PartJoint.Create(linkSource.part, linkTarget.part,
                                      linkSource.coupleNode, linkTarget.coupleNode,
                                      AttachModes.STACK);
    SetCustomJoints(stockJoint.joints.ToArray());
  }
  #endregion
}

}  // namespace
