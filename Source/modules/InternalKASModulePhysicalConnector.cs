// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.ModelUtils;
using UnityEngine;

namespace KAS {

/// <summary>Module for handling physics on a flexible link connector.</summary>
/// <remarks>
/// This is an <i>internal</i> module. It must not be instantiated or accessed outside of the KAS
/// mod. The module must only be created thru a factory method.
/// </remarks>
/// <seealso cref="Promote"/>
sealed class InternalKASModulePhysicalConnector : MonoBehaviour {
  /// <summary>Promotes the specified object into a physical connector object.</summary>
  /// <remarks>
  /// The physics will immediately start on the object. If it doesn't have a rigidbody, the one will
  /// be created.
  /// </remarks>
  /// <param name="ownerModule">The part's module which will control the connector.</param>
  /// <param name="obj">The object to be promoted.</param>
  /// <param name="connectorMass">The mass of the connector.</param>
  /// <param name = "interactionDistance"></param>
  public static InternalKASModulePhysicalConnector Promote(
      PartModule ownerModule, GameObject obj, float connectorMass, float interactionDistance = 0) {
    var connectorRb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();
    connectorRb.useGravity = false;
    connectorRb.velocity = ownerModule.part.rb.velocity;
    connectorRb.angularVelocity = ownerModule.part.rb.angularVelocity;
    connectorRb.ResetInertiaTensor();
    connectorRb.ResetCenterOfMass();
    connectorRb.mass = connectorMass;
    var connectorModule = obj.AddComponent<InternalKASModulePhysicalConnector>();
    connectorModule.ownerModule = ownerModule;

    // Create the interaction collider if requested.
    if (interactionDistance > 0) {
      // This mesh is placed on a special layer which is not rendered in the game. It's only
      // used to detect the special zones triggers (like ladders, hatches, etc.).
      var interactionTriggerObj = Meshes.CreateSphere(
          2 * interactionDistance, null, obj.transform, Colliders.PrimitiveCollider.Shape);
      interactionTriggerObj.name = InteractionAreaCollider;
      interactionTriggerObj.layer = (int) KspLayer.TriggerCollider;
      interactionTriggerObj.gameObject.GetComponent<Collider>().isTrigger = true;
      connectorModule.interactionTriggerObj = interactionTriggerObj;
    }

    return connectorModule;
  }

  /// <summary>Module which controls this head.</summary>
  /// <value>The module instance.</value>
  public PartModule ownerModule { get; private set; }

  /// <summary>
  /// Name of the collider which is used for detection of the head's interaction area in EVA.
  /// </summary>
  /// <remarks>
  /// Other objects may check for the collider name to quickly figure out if the the trigger event
  /// came from a head interaction area.
  /// </remarks>
  public const string InteractionAreaCollider = "InternalKASModulePhysicalHead_InteractionCollider";

  Rigidbody headRb;
  GameObject interactionTriggerObj;

  #region MonoBehaviour messages
  void Awake() {
    headRb = GetComponent<Rigidbody>();
    headRb.gameObject.transform.parent = null;  // Detach from the hierarchy.
  }

  void OnDestroy() {
    if (interactionTriggerObj != null) {
      Destroy(interactionTriggerObj);
    }
  }

  void FixedUpdate() {
    if (headRb != null && ownerModule != null && !headRb.isKinematic) {
      KASAPI.PhysicsUtils.ApplyGravity(headRb, ownerModule.vessel);
    }
  }
  #endregion
}

}  // namespace
