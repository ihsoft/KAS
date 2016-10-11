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

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public override void DropJoint() {
    base.DropJoint();
    var joint = part.GetComponent<ConfigurableJoint>();
    UnityEngine.Object.Destroy(joint);
  }

  /// <inheritdoc/>
  public override void AdjustJoint(bool isIndestructible = false) {
    //FIXME
    Debug.LogWarningFormat("** ON AdjustJoint: isIndestructible = {0}", isIndestructible);
  }
  #endregion
}

}  // namespace
