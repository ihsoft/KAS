// Kerbal Attachment System API
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using UnityEngine;

namespace KASImpl {

class PhysicsUtilsImpl : IPhysicsUtils {
  /// <inheritdoc/>
  public void ApplyGravity(Rigidbody rb, Vessel vessel, double rbAirDragMultiplier = 1.0) {
    // Apply the gravity as it's done in FlightIntegrator for the physical object.
    var geeForce = FlightGlobals.getGeeForceAtPosition(vessel.CoMD, vessel.mainBody)
                   + FlightGlobals.getCoriolisAcc(vessel.velocityD, vessel.mainBody)
                   + FlightGlobals.getCentrifugalAcc(vessel.CoMD, vessel.mainBody);
    rb.AddForce(geeForce * PhysicsGlobals.GraviticForceMultiplier, ForceMode.Acceleration);
    // Apply the atmosphere drag force as it's done in FlightIntegrator for the physical object.
    if (PhysicsGlobals.ApplyDrag && vessel.atmDensity > 0) {
      const int pseudoReDragMultiplier = 1; //TODO(ihsoft): find out what it is
      var d = 0.0005 * pseudoReDragMultiplier * vessel.atmDensity * rbAirDragMultiplier
          * (rb.velocity + Krakensbane.GetFrameVelocity()).sqrMagnitude
          * PhysicsGlobals.DragMultiplier;
      if (!double.IsNaN(d) && !double.IsInfinity(d)) {
        var atmDragForce = -(rb.velocity + Krakensbane.GetFrameVelocity()).normalized * d;
        rb.AddForce(
            atmDragForce, PhysicsGlobals.DragUsesAcceleration
                ? ForceMode.Acceleration
                : ForceMode.Force);
      }
    }
  }
}

}  // namespace
