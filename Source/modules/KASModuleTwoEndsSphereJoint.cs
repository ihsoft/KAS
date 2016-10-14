// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KASAPIv1;
using System;
using System.Collections;
using UnityEngine;

namespace KAS {

/// <summary>Module that offers a highly configurable setup of three PhysX joints.</summary>
/// <remarks>
/// One spherical joint is located at the source part, another spherical joint is located at the
/// target part. The joints are connected with a third joint that is setup as prismatic. Such setup
/// allows soucre and target parts rotationg relative to each other. Distance between the parts is
/// limited by the prismatic joint.
/// </remarks>
/// <seealso href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#spherical-joint">
/// PhysX: Spherical joint</seealso>
/// <seealso href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#prismatic-joint">
/// PhysX: Prismatic joint</seealso>
// TODO(ihsoft): Add an image.
// TODO(ihsoft): Implement prismatic joint linear limits.
// FIXME(ihsoft): Fix initial state setup for the sphere joints.
public sealed class KASModuleTwoEndsSphereJoint : AbstractJointModule {
  #region Helper class to detect joint breakage
  /// <summary>
  /// Helper class to detect sphere joint ends breakage and deliver event to the host part.
  /// </summary>
  class BrokenJointListener : MonoBehaviour {
    /// <summary>Part to decouple on joint break.</summary>
    public Part host;

    /// <summary>Triggers when joint break force if exceeded.</summary>
    /// <param name="breakForce">Actual force that broke the joint.</param>
    void OnJointBreak(float breakForce) {
      if (host.parent != null) {
        if (gameObject != host.gameObject) {
          host.gameObject.SendMessage(
              "OnJointBreak", breakForce, SendMessageOptions.DontRequireReceiver);
        } else {
          Debug.LogWarning("Owner and host of the joint break listener are the same!");
        }
      }
    }
  }
  #endregion

  #region Part's config fields
  /// <summary>
  /// Config setting. Spring force of the prismatic joint that limits the distance.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float strutSpringForce = Mathf.Infinity;
  /// <summary>Config setting. Damper force of the spring that limits the distance.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public float strutSpringDamperRatio = 0.1f;  // 10% of the force.
  #endregion

  /// <summary>Name of the source joint game object.</summary>
  /// <remarks>
  /// This object is connected to the target counter part either by a joint (stretch link) or as a
  /// parent (in case of rigid link).
  /// </remarks>
  const string SrcJointName = "KASJointSrc";
  /// <summary>Name of the target joint game object.</summary>
  /// <remarks>This object is connected to the source counter part either by a joint or as a
  /// transfrom child (in case of rigid link).</remarks>
  const string TargetJointName = "KASJointTrg";

  /// <summary>Source sphere joint.</summary>
  /// <remarks>
  /// It doesn't allow linear movements but does allow rotation around any axis. Rotation can be
  /// limited via a configuration parameter
  /// <see cref="AbstractJointModule.cfgSourceLinkAngleLimit"/>. The joint is unbreakable by the
  /// linear force but can be broken by torque when angle limit is exhausted.
  /// </remarks>
  ConfigurableJoint srcJoint;
  /// <summary>Target sphere joint.</summary>
  /// <remarks>
  /// It doesn't allow linear movements but does allow rotation around any axis. Rotation can be
  /// limited via a configuration parameter
  /// <see cref="AbstractJointModule.cfgTargetLinkAngleLimit"/>. The joint is unbreakable by the
  /// linear force but can be broken by torque when angle limit is exhausted.
  /// </remarks>
  ConfigurableJoint trgJoint;
  /// <summary>Joint that ties two sphere joints together.</summary>
  /// <remarks>It only makes sense <see cref="strutSpringForce"/> is not <c>Infinite</c>.</remarks>
  // TODO(ihsoft): Don't create it if spring is infinte</para>
  ConfigurableJoint strutJoint;

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public override void CreateJoint(ILinkSource source, ILinkTarget target) {
    base.CreateJoint(source, target);
    srcJoint = CreateKinematicJointEnd(source.attachNode, SrcJointName, sourceLinkAngleLimit);
    trgJoint = CreateKinematicJointEnd(target.attachNode, TargetJointName, targetLinkAngleLimit);
    if (!float.IsInfinity(strutSpringForce)) {
      strutJoint = srcJoint.gameObject.AddComponent<ConfigurableJoint>();
    }
    StartCoroutine(WaitAndConnectJointEnds());
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
    if (isUnbreakable) {
      SetupUnbreakableJoint(srcJoint);
      SetupUnbreakableJoint(trgJoint);
      SetupUnbreakableJoint(strutJoint);
    } else {
      SetupNormalEndJoint(srcJoint, sourceLinkAngleLimit);
      SetupNormalEndJoint(trgJoint, targetLinkAngleLimit);
      SetupNormalStrutJoint();
    }
  }
  #endregion

  #region Private utility methods
  /// <summary>Creates a game object joined with the attach node.</summary>
  /// <remarks>
  /// The created object has kinematic rigidbody and its transform is parented to the node. This
  /// object must be promoted to physical once PhysX received the configuration.
  /// </remarks>
  /// <param name="an">Node to attach a new spheric joint to.</param>
  /// <param name="objName">Name of the game object for the new joint.</param>
  /// <param name="angleLimit">Degree of freedom for the joint.</param>
  /// <returns>Object that owns the joint.</returns>
  // FIXME(ihsoft): Revise approach to not have two joints on one end (+strut joint)
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
  /// <remarks>
  /// Waiting is required to have the spherical joints info sent to the PhysX engine. This must
  /// happen when both ends are at their "kinematic" positions to let engine capture the initial
  /// angles. Once it's done it's time to rotate the ends so what they look at each other, and
  /// attach them with a fixed or prismatic joint.
  /// </remarks>
  /// <returns><c>null</c> when it's time to terminate.</returns>
  // TODO(ihsoft): Don't create prismatic joint when spring is infinite
  IEnumerator WaitAndConnectJointEnds() {
    // We'll spend several fixed update cycles to setup the joints, so make stock joint absolutely
    // rigid to avoid parts moving during the process.
    if (stockJoint != null) {
      SetupUnbreakableJoint(stockJoint.Joint);
    }
  
    // Allow fixed update to have the joint info sent to PhysX. This will capture initial sphere
    // joints rotations.
    yield return new WaitForFixedUpdate();

    // Now rotate both sphere joints towards each other, and connect them with a prismatic joint.
    var srcRb = srcJoint.gameObject.GetComponent<Rigidbody>();
    // FIXME(ihsoft): Handle mass via joint instead of renderer.  
    Debug.LogWarningFormat("** RB MASS: {0}", srcRb.mass);
    var trgRb = trgJoint.gameObject.GetComponent<Rigidbody>();
    srcJoint.transform.LookAt(trgJoint.transform);
    trgJoint.transform.LookAt(srcJoint.transform);
    strutJoint = srcJoint.gameObject.AddComponent<ConfigurableJoint>();
    SetupNormalStrutJoint();
    strutJoint.connectedBody = trgRb;
    
    // Allow another fixed update to happen to remember strut positions.
    yield return new WaitForFixedUpdate();

    // Setup breaking forces.
    AdjustJoint();

    // Promote source and target rigid bodies to independent physical objects. From now on they live
    // in a system of three joints.
    srcRb.isKinematic = false;
    srcJoint.transform.parent = null;
    trgRb.isKinematic = false;
    trgJoint.transform.parent = null;

    DropStockJoint();
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
  void SetupNormalStrutJoint() {
    if (strutJoint == null) {
      return;
    }
    KASAPI.JointUtils.ResetJoint(strutJoint);
    KASAPI.JointUtils.SetupPrismaticJoint(
        strutJoint, springForce: strutSpringForce, springDamperRatio: strutSpringDamperRatio);
    strutJoint.enablePreprocessing = true;
    SetBreakForces(strutJoint, linkBreakForce, Mathf.Infinity);
  }
  #endregion
}

}  // namespace
