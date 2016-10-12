// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;

namespace KAS {

/// <summary>
/// Module that offers normal KAS joint logic basing on joint created by KSP. The joint is not
/// modified in any way, and it behavior is very similar to the behavior of a regular joint that
/// normally connets two parts together.
/// </summary>
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
