// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KASAPIv1;
using System;
using System.Collections;
using UnityEngine;

namespace KAS {

//FIXME docs
public sealed class KASModuleTwoEndsSphereJoint : AbstractJointModule {
  #region Helper class to detect joint breakage
  /// <summary>Helper class to detect sphere joint ends breakage.</summary>
  /// <remarks>When joint breaks the source part is get decoupled from the parent.</remarks>
  class BrokenJointListener : MonoBehaviour {
    /// <summary>Part to decouple on joint break.</summary>
    public Part host;

    /// <summary>Triggers when joint break force if exceeded.</summary>
    /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
    /// <param name="breakForce">Actual force that broke the joint.</param>
    void OnJointBreak(float breakForce) {
      if (host.parent != null) {
        Debug.LogFormat("Joint {0} broken with force: {1}", this.gameObject.name, breakForce);
        host.OnPartJointBreak(breakForce);
      }
    }
  }
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public float strutSpringForce = Mathf.Infinity;
  [KSPField]
  public float strutSpringDamperRatio = 0.1f;  // 10% of the force.
  #endregion

  /// <summary>Name of the source joint game object.</summary>
  /// <remarks>This object is connected to the target counter part either by a joint (stretch link)
  /// or as a parent (in case of rigid link).</remarks>
  const string SrcJointName = "KASJointSrc";
  /// <summary>Name of the target joint game object.</summary>
  /// <remarks>This object is connected to the source counter part either by a joint or as a
  /// transfrom child (in case of rigid link).</remarks>
  const string TargetJointName = "KASJointTrg";

  /// <summary>Source sphere joint.</summary>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis. Rotation
  /// can be limited via a configuration parameter <see cref="sourceLinkAngleLimit"/>. The joint is
  /// unbreakable by the linear force but can be broken by torque when angle limit is exhausted.
  /// </remarks>
  ConfigurableJoint srcJoint;
  /// <summary>Target sphere joint.</summary>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis. Rotation
  /// can be limited via a configuration parameter <see cref="targetLinkAngleLimit"/>. The joint is
  /// unbreakable by the linear force but can be broken by torque when angle limit is exhausted.
  /// </remarks>
  ConfigurableJoint trgJoint;
  //FIXME
  ConfigurableJoint strutJoint;

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public override void CreateJoint(ILinkSource source, ILinkTarget target) {
    var stockJoint = part.attachJoint;
    base.CreateJoint(source, target);
    srcJoint = CreateKinematicJointEnd(source.attachNode, SrcJointName, sourceLinkAngleLimit);
    trgJoint = CreateKinematicJointEnd(target.attachNode, TargetJointName, targetLinkAngleLimit);
    StartCoroutine(WaitAndConnectJointEnds(stockJoint));
  }

  /// <inheritdoc/>
  public override void DropJoint() {
    base.DropJoint();
    UnityEngine.Object.Destroy(srcJoint);
    srcJoint = null;
    UnityEngine.Object.Destroy(trgJoint);
    trgJoint = null;
    strutJoint = null;
  }

  /// <inheritdoc/>
  public override void AdjustJoint(bool isUnbreakable = false) {
    //FIXME
    Debug.LogWarningFormat("** ON AdjustJoint: isIndestructible = {0}", isUnbreakable);
    if (isUnbreakable) {
      SetupUnbreakableJoint(srcJoint);
      SetupUnbreakableJoint(trgJoint);
      SetupUnbreakableJoint(strutJoint);
    } else {
      SetupNormalEndJoint(srcJoint, sourceLinkAngleLimit);
      SetupNormalEndJoint(trgJoint, targetLinkAngleLimit);
      SetupNormalStrutJoint(strutJoint);
    }
  }
  #endregion

  #region Private utility methods
  /// <summary>Creates a game object joined with the attach node.</summary>
  /// <remarks>The created object has kinematic rigidbody and its transform is parented to the
  /// node. This object must be promoted to physical once PhysX received the configuration.
  /// </remarks>
  /// <param name="an">Node to attach a new spheric joint to.</param>
  /// <param name="objName">Name of the game object for the new joint.</param>
  /// <param name="angleLimit">Degree of freedom for the joint.</param>
  /// <returns>Object that owns the joint.</returns>
  ConfigurableJoint CreateKinematicJointEnd(AttachNode an, string objName, float angleLimit) {
    var objJoint = new GameObject(objName);
    objJoint.AddComponent<BrokenJointListener>().host = part;
    var jointRb = objJoint.AddComponent<Rigidbody>();
    jointRb.isKinematic = true;
    objJoint.transform.parent = an.nodeTransform;
    objJoint.transform.localPosition = Vector3.zero;
    objJoint.transform.localRotation = Quaternion.identity;
    var joint = objJoint.AddComponent<ConfigurableJoint>();
    SetupNormalEndJoint(joint, angleLimit);
    joint.connectedBody = an.owner.rb;
    return joint;
  }

  /// <summary>Waits for the next fixed update and connects the joint ends.</summary>
  /// <remarks>Waiting is required to have the spherical joints info sent to the PhysX engine. This
  /// must happen when both ends are at their "kinematic" positions to let engine capture the
  /// initial angles. Once it's done it's time to rotate the ends so what they look at each other,
  /// and attach them with a fixed or prismatic joint.</remarks>
  /// <returns><c>null</c> when it's time to terminate.</returns>
  /// FIXME don't create prismatic joint when spring is infinite
  /// FIXME when spring is set init prismatic joitn from the zero length
  IEnumerator WaitAndConnectJointEnds(PartJoint partAttachJoint) {
    // We'll spend several fixed update cycles to setup the joints, so make stock joint absolutely
    // rigid to avoid parts moving during the process.
    SetupUnbreakableJoint(partAttachJoint.Joint);
  
    // Allow fixed update to have the joint info sent to PhysX. This will capture initial sphere
    // joints rotations.
    yield return new WaitForFixedUpdate();

    // Now rotate both sphere joints towards each other, and connect them with a prismatic joint.
    var srcRb = srcJoint.gameObject.GetComponent<Rigidbody>();
    //FIXME
    Debug.LogWarningFormat("** RB MASS: {0}", srcRb.mass);
    var trgRb = trgJoint.gameObject.GetComponent<Rigidbody>();
    srcJoint.transform.LookAt(trgJoint.transform);
    trgJoint.transform.LookAt(srcJoint.transform);
    strutJoint = srcJoint.gameObject.AddComponent<ConfigurableJoint>();
    SetupNormalStrutJoint(strutJoint);
    strutJoint.connectedBody = trgRb;
    
    // Allow another fixed update to happen to remember strut positions.
    //FIXME
    Debug.LogWarning("Wait for strut joints to populate");
    yield return new WaitForFixedUpdate();

    // Setup breaking forces and handlers.
    srcJoint.gameObject.AddComponent<BrokenJointListener>().host = part;
    trgJoint.gameObject.AddComponent<BrokenJointListener>().host = part;
    AdjustJoint();

    // Promote source and target rigid bodies to independent physical objects. From now on they live
    // in a system of three joints.
    //FIXME: for non spring strut target should be child of source. 
    srcRb.isKinematic = false;
    srcJoint.transform.parent = null;
    trgRb.isKinematic = false;
    trgJoint.transform.parent = null;
    
    //FIXME
    Debug.LogWarning("Joint promoted to physics");
    // Note, that this will trigger GameEvents.onPartJointBreak event.
    partAttachJoint.DestroyJoint();
  }

  /// <summary>Makes the joint unbreakable and locked.</summary>
  void SetupUnbreakableJoint(ConfigurableJoint joint) {
    //FIXME
    if (joint == null) {
      Debug.LogWarning("set UNBREAKABLE: joint doesn't exist!");
      return;
    }
    joint.angularXMotion = ConfigurableJointMotion.Locked;
    joint.angularYMotion = ConfigurableJointMotion.Locked;
    joint.angularZMotion = ConfigurableJointMotion.Locked;
    joint.xMotion = ConfigurableJointMotion.Locked;
    joint.yMotion = ConfigurableJointMotion.Locked;
    joint.zMotion = ConfigurableJointMotion.Locked;
    joint.enablePreprocessing = true;
    SetBreakForces(joint, Mathf.Infinity, Mathf.Infinity);
  }

  /// <summary>Sets sphere joint parameters.</summary>
  /// <param name="joint">Joint to setup.</param>
  /// <param name="angleLimit">Angle of freedom at the pivot.</param>
  void SetupNormalEndJoint(ConfigurableJoint joint, float angleLimit) {
    KASAPI.JointUtils.ResetJoint(joint);
    KASAPI.JointUtils.SetupSphericalJoint(joint, angleLimit: angleLimit);
    joint.enablePreprocessing = true;
    SetBreakForces(joint, Mathf.Infinity, linkBreakTorque);
  }

  /// <summary>Sets parameters of the joint that connects the pivots.</summary>
  /// <remarks>If no strut joint were created than this call is NO-OP.</remarks>
  /// <param name="joint">Strut joint to setup.</param>
  void SetupNormalStrutJoint(ConfigurableJoint joint) {
    //FIXME
    if (joint == null) {
      Debug.LogWarning("set NORMAL: joint doesn't exist!");
      return;
    }
    KASAPI.JointUtils.ResetJoint(joint);
    //FIXME use spring force from the settings
    KASAPI.JointUtils.SetupPrismaticJoint(joint, springForce: Mathf.Infinity);
    joint.enablePreprocessing = true;
    SetBreakForces(joint, linkBreakForce, Mathf.Infinity);
  }
  #endregion
}

}  // namespace
