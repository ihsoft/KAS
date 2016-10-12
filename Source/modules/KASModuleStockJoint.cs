// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

// FIXME: start handling jointBreakForce
public class KASModuleStockJoint : AbstractJointModule {
  /// <summary>Intermediate field to store joint settings in AdjustJoint.</summary>
  protected JointState jointState = new JointState();

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public override void AdjustJoint(bool isUnbreakable = false) {
    if (isUnbreakable) {
      SetupUnbreakableJoint(stockJoint.Joint, jointState: jointState);
    } else {
      RestoreJointState(stockJoint.Joint, jointState);
    }
  }
  #endregion
}

}  // namespace
