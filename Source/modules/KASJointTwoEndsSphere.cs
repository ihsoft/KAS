// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
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
public class KASJointTwoEndsSphere : AbstractLinkJoint,
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
  public float strutSpringForce = Mathf.Infinity;

  /// <summary>Damper force of the spring that limits the distance.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float strutSpringDamperRatio = 0.1f;  // 10% of the force.

  /// <summary>Tells if joined parts can move relative to each other.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public bool isUnlockedJoint;
  #endregion

  #region Inheritable properties
  /// <summary>Source sphere joint.</summary>
  /// <value>PhysX joint at the source part. <c>null</c> if there is no joint established.</value>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis.</remarks>
  /// <seealso cref="AbstractLinkJoint.sourceLinkAngleLimit"/>.
  protected ConfigurableJoint srcJoint { get; private set; }

  /// <summary>Target sphere joint.</summary>
  /// <value>PhysX joint at the target part. <c>null</c> if there is no joint established.</value>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis.</remarks>
  /// <seealso cref="AbstractLinkJoint.targetLinkAngleLimit"/>
  protected ConfigurableJoint trgJoint { get; private set; }

  /// <summary>Object that connects two sphere joints together.</summary>
  /// <remarks>
  /// Both the <see cref="srcJoint"/> and the <see cref="trgJoint"/> are bound to this object's
  /// rigidbody. To minimize the physical effect of this artifical RB, its mass is set to the bare
  /// minimum, which is <c>0.001t</c>.
  /// </remarks>
  /// <value>The game object.</value>
  protected GameObject connectorObj { get; private set; }
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
    var srcAchorPos = GetSourcePhysicalAnchor(linkSource);
    var tgtAchorPos = GetTargetPhysicalAnchor(linkSource, linkTarget);
    
    // TODO(ihsoft): Assign the renderer's colliders to the real RBs instead of the part's RB.
    connectorObj = new GameObject("ConnectorObj");
    connectorObj.AddComponent<KASInternalBrokenJointListener>().hostPart = part;
    connectorObj.transform.position = (srcAchorPos + tgtAchorPos) / 2;
    var connectorRb = connectorObj.AddComponent<Rigidbody>();
    connectorRb.mass = 0.001f;  // Set the minimum allowed mass.
    connectorRb.useGravity = false;
    connectorRb.velocity = linkSource.part.rb.velocity;
    connectorRb.angularVelocity = linkSource.part.rb.angularVelocity;

    srcJoint = connectorObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(srcJoint);
    KASAPI.JointUtils.SetupSphericalJoint(srcJoint, angleLimit: sourceLinkAngleLimit);
    srcJoint.enablePreprocessing = true;
    srcJoint.autoConfigureConnectedAnchor = false;
    srcJoint.connectedBody = linkSource.part.rb;
    srcJoint.anchor = connectorObj.transform.InverseTransformPoint(srcAchorPos);
    srcJoint.connectedAnchor = srcJoint.connectedBody.transform.InverseTransformPoint(srcAchorPos);
    SetBreakForces(srcJoint, linkBreakForce, linkBreakTorque);

    trgJoint = connectorObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(trgJoint);
    KASAPI.JointUtils.SetupSphericalJoint(trgJoint, angleLimit: targetLinkAngleLimit);
    trgJoint.enablePreprocessing = true;
    trgJoint.autoConfigureConnectedAnchor = false;
    trgJoint.connectedBody = linkTarget.part.rb;
    trgJoint.anchor = connectorObj.transform.InverseTransformPoint(tgtAchorPos);
    trgJoint.connectedAnchor = trgJoint.connectedBody.transform.InverseTransformPoint(tgtAchorPos);
    SetBreakForces(trgJoint, linkBreakForce, linkBreakTorque);
    
    // This "joint" is only needed to disable the collisions between the parts.
    var collisionJoint = linkSource.part.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(collisionJoint);
    KASAPI.JointUtils.SetupDistanceJoint(collisionJoint);
    collisionJoint.xMotion = ConfigurableJointMotion.Free;
    collisionJoint.yMotion = ConfigurableJointMotion.Free;
    collisionJoint.zMotion = ConfigurableJointMotion.Free;
    collisionJoint.enablePreprocessing = true;
    collisionJoint.connectedBody = linkTarget.part.rb;

    SetCustomJoints(new[] {srcJoint, trgJoint, collisionJoint},
                    extraObjects: new[] {connectorObj});
  }
  #endregion

  #region IJointLockState implemenation
  /// <inheritdoc/>
  public bool IsJointUnlocked() {
    return isUnlockedJoint;
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
    if (isUnlockedJoint && isLinked && action.to != null && action.from.partRef == part) {
      var node = action.to;
      node.SetValue("position", part.orgPos);
      node.SetValue("rotation", part.orgRot);
    }
  }
  #endregion
}

}  // namespace
