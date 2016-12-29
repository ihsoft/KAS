// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.KSPInterfaces;
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that offers normal KAS joint logic basing on joint created by KSP. The joint is not
/// modified in any way, and it behavior is very similar to the behavior of a regular joint that
/// normally connects two parts together.
/// </summary>
public class KASModuleStockJoint :
    // KAS parents.
    AbstractJointModule,
    // Syntax sugar parents.
    IJointEventsListener {

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public override void AdjustJoint(bool isUnbreakable = false) {
    if (isUnbreakable) {
      SetBreakForces(stockJoint.Joint, Mathf.Infinity, Mathf.Infinity);
    } else {
      SetBreakForces(stockJoint.Joint, cfgLinkBreakForce, cfgLinkBreakTorque);
    }
  }
  #endregion

  #region IJointEventsListener implementation
  /// <inheritdoc/>
  public virtual void OnJointBreak(float breakForce) {
    // It's not absolutely required since part will get decoupled anyways, but pro-active link
    // break allows specifying the right actor.
    linkSource.BreakCurrentLink(KASAPIv1.LinkActorType.Physics);
  }
  #endregion
}

}  // namespace
