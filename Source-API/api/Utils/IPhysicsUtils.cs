// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

// Name of the namespace denotes the API version.
namespace KASAPIv2 {

/// <summary>Various tools to deal with the gme's physics.</summary>
public interface IPhysicsUtils {
  /// <summary>Applies the forces that affect a rigidbody on a selectial body.</summary>
  /// <remarks>
  /// This method replicates the logic from <see cref="FlightIntegrator"/> for the physical objects.
  /// Alas, this method is not available for a plain rigidbody.
  /// </remarks>
  /// <param name="rb">The rigidbody to apply the forces to.</param>
  /// <param name="vessel">
  /// The vessel to use as a base point for the gravity and atmosphere properties. When there is no
  /// good choice, just pick the closest one.
  /// </param>
  /// <param name="rbAirDragMult">
  /// The multiplier that tells how significantly the rigidbody is resisting to the air flow.
  /// </param>
  /// <returns>Target or <c>null</c> if no valid target was found.</returns>
  void ApplyGravity(Rigidbody rb, Vessel vessel, double rbAirDragMult = 1.0);
}

}  // namespace
