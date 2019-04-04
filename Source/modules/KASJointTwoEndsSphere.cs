// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using System;
using UnityEngine;

namespace KAS {

/// <summary>Module that offers a highly configurable setup of two PhysX joints.</summary>
/// <remarks>
/// One spherical joint is located at the source part, another spherical joint is located at the
/// target part. The joints are rigidly connected to each other via an invisible game object with a
/// rigid body. This object has little or none mass, so it doesn't give much physical effect by
/// itself. Its main purpose to be a connector between the two joints.
/// </remarks>
/// <seealso cref="ILinkJoint.CreateJoint"/>
/// <seealso href="http://docs.nvidia.com/gameworks/content/gameworkslibrary/physx/guide/Manual/Joints.html#spherical-joint">
/// PhysX: Spherical joint</seealso>
public class KASJointTwoEndsSphere : AbstractJoint,
    // KSP interfaces.
    IJointLockState,
    // KAS interfaces.
    IKasJointEventsListener,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsDestroyable, IKSPDevJointLockState {

  #region Part's config fields
  /// <summary>Spring force of the prismatic joint that limits the distance.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Strut spring force")]
  public float strutSpringForce = Mathf.Infinity;

  /// <summary>Damper force of the spring that limits the distance.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Strut damper ratio")]
  public float strutSpringDamperRatio = 0.1f;  // 10% of the force.
  #endregion

  #region Inheritable properties
  /// <summary>Source sphere joint.</summary>
  /// <value>PhysX joint at the source part. <c>null</c> if there is no joint established.</value>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis.</remarks>
  /// <seealso cref="AbstractJoint.sourceLinkAngleLimit"/>.
  protected ConfigurableJoint srcJoint { get; private set; }

  /// <summary>Target sphere joint.</summary>
  /// <value>PhysX joint at the target part. <c>null</c> if there is no joint established.</value>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis.</remarks>
  /// <seealso cref="AbstractJoint.targetLinkAngleLimit"/>
  protected ConfigurableJoint trgJoint { get; private set; }

  /// <summary>Object that connects two sphere joints together.</summary>
  /// <remarks>
  /// Both the <see cref="srcJoint"/> and the <see cref="trgJoint"/> are bound to this object's
  /// rigidbody. To minimize the physical effect of this artifical RB, its mass is set to the bare
  /// minimum, which is <c>0.001t</c>.
  /// </remarks>
  /// <value>The game object.</value>
  protected GameObject middleObj { get; private set; }
  #endregion

  #region AbstractLinkJoint overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    GameEvents.onProtoPartSnapshotSave.Add(OnProtoPartSnapshotSave);
  }

  /// <inheritdoc/>
  public override void OnDestroy() {
    base.OnDestroy();
    GameEvents.onProtoPartSnapshotSave.Remove(OnProtoPartSnapshotSave);
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    if (isLinked) {
      // Note that the part itself has already been saved into the config with the incorrect data.
      // This data will be fixed in onProtoPartSnapshotSave.
      vessel.parts.ForEach(x => x.UpdateOrgPosAndRot(vessel.rootPart));
    }
  }

  /// <summary>Creates the joins to make a physical link.</summary>
  protected override void SetupPhysXJoints() {
    // The stock joint is rigid, drop it.
    if (partJoint != null) {
      HostedDebugLog.Fine(this, "Dropping the stock joint to: {0}", partJoint.Child);
      partJoint.DestroyJoint();
      partJoint.Child.attachJoint = null;
    }

    HostedDebugLog.Fine(this, "Creating a 2-joints assembly");
    var srcAnchorPos = GetSourcePhysicalAnchor(linkSource);
    var tgtAnchorPos = GetTargetPhysicalAnchor(linkSource, linkTarget);
    var pipeHalfLength = (tgtAnchorPos - srcAnchorPos).magnitude / 2;
    
    middleObj = new GameObject("ConnectorObj");
    middleObj.AddComponent<KASInternalBrokenJointListener>().hostPart = part;
    var middleRb = middleObj.AddComponent<Rigidbody>();
    // PhysX behaves weird if the linked rigidbodies are too different in mass, so make the
    // connector object "somewhat" the same in mass as the both ends of the link.
    middleRb.mass = (linkSource.part.rb.mass + linkTarget.part.rb.mass) / 2;
    middleRb.useGravity = false;
    middleRb.velocity = linkSource.part.rb.velocity;
    middleRb.angularVelocity = linkSource.part.rb.angularVelocity;

    // Build all joints aligned to the source node direction to have the angle limits set correctly.
    middleObj.transform.position = srcAnchorPos + linkSource.nodeTransform.forward * pipeHalfLength;
    middleObj.transform.rotation =
        Quaternion.LookRotation(linkSource.nodeTransform.forward, linkSource.nodeTransform.up);
    srcJoint = middleObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(srcJoint);
    KASAPI.JointUtils.SetupSphericalJoint(srcJoint, angleLimit: sourceLinkAngleLimit);
    srcJoint.autoConfigureConnectedAnchor = false;
    srcJoint.anchor = middleRb.transform.InverseTransformPoint(srcAnchorPos);
    srcJoint.connectedBody = linkSource.part.rb;
    srcJoint.connectedAnchor = srcJoint.connectedBody.transform.InverseTransformPoint(srcAnchorPos);
    SetBreakForces(srcJoint, linkBreakForce, linkBreakTorque);
    
    middleObj.transform.position = tgtAnchorPos + linkTarget.nodeTransform.forward * pipeHalfLength;
    middleObj.transform.rotation =
        Quaternion.LookRotation(-linkTarget.nodeTransform.forward, linkTarget.nodeTransform.up);
    trgJoint = middleObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(trgJoint);
    KASAPI.JointUtils.SetupSphericalJoint(trgJoint, angleLimit: targetLinkAngleLimit);
    trgJoint.autoConfigureConnectedAnchor = false;
    trgJoint.anchor = middleRb.transform.InverseTransformPoint(tgtAnchorPos);
    trgJoint.connectedBody = linkTarget.part.rb;
    trgJoint.connectedAnchor = trgJoint.connectedBody.transform.InverseTransformPoint(tgtAnchorPos);
    SetBreakForces(trgJoint, linkBreakForce, linkBreakTorque);

    middleObj.transform.position = (tgtAnchorPos + srcAnchorPos) / 2;
    middleObj.transform.rotation = Quaternion.LookRotation(
        tgtAnchorPos - middleObj.transform.position, linkSource.nodeTransform.up);
    
    // This "joint" is only needed to disable the collisions between the parts.
    var collisionJoint = linkSource.part.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(collisionJoint);
    KASAPI.JointUtils.SetupDistanceJoint(collisionJoint);
    collisionJoint.xMotion = ConfigurableJointMotion.Free;
    collisionJoint.yMotion = ConfigurableJointMotion.Free;
    collisionJoint.zMotion = ConfigurableJointMotion.Free;
    collisionJoint.connectedBody = linkTarget.part.rb;

    SetCustomJoints(new[] {srcJoint, trgJoint, collisionJoint},
                    extraObjects: new[] {middleObj});
  }
  #endregion

  #region IJointLockState implemenation
  /// <inheritdoc/>
  public bool IsJointUnlocked() {
    return true;
  }
  #endregion

  #region IKasJointEventsListener implementation
  /// <inheritdoc/>
  public virtual void OnKASJointBreak(GameObject hostObj, float breakForce) {
    // Check for the linked state since there can be multiple joints destroyed in the same frame.
    if (isLinked) {
      linkSource.BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion

  #region Private utility methods
  /// <summary>
  /// Fixes the stored org position and rotation since they are saved before UpdateOrgPosAndRot
  /// happens.
  /// </summary>
  void OnProtoPartSnapshotSave(GameEvents.FromToAction<ProtoPartSnapshot, ConfigNode> action) {
    if (isLinked && action.to != null && action.from.partRef == part) {
      var node = action.to;
      node.SetValue("position", part.orgPos);
      node.SetValue("rotation", part.orgRot);
    }
  }
  #endregion
}

}  // namespace
