// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using UnityEngine;

namespace KAS {

/// <summary>Module for handling physics on a flexible link connector.</summary>
/// <remarks>
/// <para>
/// This is an <i>internal</i> module. It must not be instantiated or accessed outside of the KAS
/// mod. The module must only be created thru the factory method.
/// </para>
/// <para>
/// The promoted object becomes independent from the creator. When the module is destoyed, its
/// rigidbody gets destroyed as well, and the model returns back to the owner part. The position has
/// to be adjusted by the caller.
/// </para>
/// </remarks>
/// <seealso cref="Promote"/>
/// <seealso cref="Demote"/>
sealed class KASInternalPhysicalConnector : MonoBehaviour {
  #region Factory methods (static)
  /// <summary>Promotes the specified object into a physical connector object.</summary>
  /// <remarks>
  /// The physics will immediately start on the object. If it doesn't have a rigidbody, the one will
  /// be created.
  /// </remarks>
  /// <param name="ownerModule">The part's module which will control the connector.</param>
  /// <param name="obj">The object to be promoted.</param>
  /// <param name = "interactionDistance"></param>
  public static KASInternalPhysicalConnector Promote(
      PartModule ownerModule, GameObject obj, float interactionDistance = 0) {
    var connectorRb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();
    connectorRb.useGravity = false;
    connectorRb.isKinematic = ownerModule.part.packed;
    connectorRb.velocity = ownerModule.part.rb.velocity;
    connectorRb.angularVelocity = ownerModule.part.rb.angularVelocity;
    connectorRb.ResetInertiaTensor();
    connectorRb.ResetCenterOfMass();
    var connectorModule = obj.AddComponent<KASInternalPhysicalConnector>();
    connectorModule.ownerModule = ownerModule;

    // Create the interaction collider if requested.
    if (interactionDistance > 0) {
      // This mesh is placed on a special layer which is not rendered in the game. It's only
      // used to detect the special zones triggers, so keep it simple.
      var interactionTriggerObj = Meshes.CreatePrimitive(
          PrimitiveType.Quad, Vector3.one, null, obj.transform);
      //interactionTriggerObj.SetActive(true);
      interactionTriggerObj.name = InteractionAreaCollider;
      var collider = interactionTriggerObj.AddComponent<SphereCollider>();
      collider.isTrigger = true;
      collider.radius = interactionDistance;
      interactionTriggerObj.layer = (int) KspLayer.TriggerCollider;
      interactionTriggerObj.gameObject.GetComponent<Collider>().isTrigger = true;
      connectorModule.interactionTriggerObj = interactionTriggerObj;
    }

    return connectorModule;
  }

  /// <summary>Removes the physical behavior from the connector object.</summary>
  /// <param name="obj">The connector object to remove the behavior from.</param>
  /// <param name="cleanupMode">
  /// Tells the owner part is being cleaned up and the object don't need to die immediately.
  /// </param>
  /// <returns><c>false</c> if the connector was not physical.</returns>
  public static bool Demote(GameObject obj, bool cleanupMode) {
    var connectorModule = obj.GetComponent<KASInternalPhysicalConnector>();
    if (connectorModule == null) {
      return false;
    }
    // Don't wait for the module destruction and cleanup immediately.
    connectorModule.CleanupModule(destroyImmediate: !cleanupMode);
    Destroy(connectorModule);
    return true;
  }
  #endregion

  #region Public properties
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

  /// <summary>Connector's rigidbody.</summary>
  /// <value>Rigidbody or <c>null</c>.</value>
  public Rigidbody connectorRb { get; private set; }
  #endregion

  #region Public methods
  /// <summary>Highglights the conenctor model or removes the highlighting.</summary>
  /// <remarks>
  /// <para>
  /// When color is set to <c>null</c>, the behavior is "cleanup", i.e. it's OK to call this method
  /// multiple times and in any object state.
  /// </para>
  /// <para>
  /// In order for the highglighting to work, the object must have a highlighter component (a KSP
  /// specific component). If one doesn't exist on the object, then it's created.
  /// </para>
  /// </remarks>
  /// <param name="color">
  /// The color to use in the highlighting. If not specified, then any existing highlighting will
  /// be removed.
  /// </param>
  public void SetHighlighting(Color? color) {
    if (!color.HasValue && connectorRb != null) {
      var headHighlighter = connectorRb.gameObject.GetComponent<Highlighting.Highlighter>();
      if (headHighlighter != null) {
        headHighlighter.ConstantOff();
      }
    } else if (connectorRb != null) {
      var headHighlighter = connectorRb.gameObject.GetComponent<Highlighting.Highlighter>()
          ?? connectorRb.gameObject.AddComponent<Highlighting.Highlighter>();
      headHighlighter.ReinitMaterials();
      headHighlighter.ConstantOn(color.Value);
    }
  }
  #endregion

  GameObject interactionTriggerObj;

  #region MonoBehaviour messages
  void Awake() {
    connectorRb = GetComponent<Rigidbody>();
    // Update the highlighters. For this we need changing the hierarchy.
    var oldParent = connectorRb.gameObject.transform.parent;
    connectorRb.gameObject.transform.parent = null;
    PartModel.UpdateHighlighters(oldParent);
    PartModel.UpdateHighlighters(connectorRb.gameObject.transform);
    if (connectorRb.isKinematic) {
      // The kinematic RB must be parented, or else it's considered static.
      connectorRb.transform.parent = ownerModule.gameObject.transform;
    }

    GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);
    GameEvents.onVesselGoOnRails.Add(OnVesselGoOnRails);
  }

  void OnDestroy() {
    CleanupModule(destroyImmediate: false);
    GameEvents.onVesselGoOffRails.Remove(OnVesselGoOffRails);
    GameEvents.onVesselGoOnRails.Remove(OnVesselGoOnRails);
  }

  void FixedUpdate() {
    if (connectorRb != null && ownerModule != null && !connectorRb.isKinematic) {
      KASAPI.PhysicsUtils.ApplyGravity(connectorRb, ownerModule.vessel);
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Destroys all the module's physical objects.</summary>
  /// <remarks>It doesn't (and must not) do it immediately.</remarks>
  /// <param name="destroyImmediate">
  /// Tells if all the object on the connector have to be dropped immediately. Never request it in
  /// the cleanup methods like <c>OnDestroy</c>.
  /// </param>
  void CleanupModule(bool destroyImmediate = true) {
    SetHighlighting(null);
    if (ownerModule != null) {
      // Bring the model back to the part or to the new host.
      var oldParent = gameObject.transform.parent;
      gameObject.transform.parent = Hierarchy.GetPartModelTransform(ownerModule.part);
      PartModel.UpdateHighlighters(oldParent);
      PartModel.UpdateHighlighters(ownerModule.part);
    }
    if (destroyImmediate) {
      DestroyImmediate(connectorRb);
      DestroyImmediate(interactionTriggerObj);
    } else {
      Destroy(connectorRb);
      Destroy(interactionTriggerObj);
    }
    interactionTriggerObj = null;
    connectorRb = null;
    ownerModule = null;
  }

  /// <summary>
  /// Makes the connector object physical and ensures it's not atatched to any parent model.
  /// </summary>
  /// <param name="vessel">The vessel which went physical.</param>
  void OnVesselGoOffRails(Vessel vessel) {
    if (connectorRb != null && ownerModule != null && vessel == ownerModule.vessel) {
      connectorRb.isKinematic = false;
      connectorRb.transform.parent = null;
    }
  }

  /// <summary>
  /// Freezes the physics on the connector and ensures the model is atatched to the owner.
  /// </summary>
  /// <param name="vessel">The vessel which went kinematic.</param>
  void OnVesselGoOnRails(Vessel vessel) {
    if (connectorRb != null && ownerModule != null && vessel == ownerModule.vessel) {
      connectorRb.isKinematic = true;
      connectorRb.transform.parent = ownerModule.gameObject.transform;
    }
  }
  #endregion
}

}  // namespace
