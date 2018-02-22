// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.PartUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that ties two parts with a cable. It allows any movement that doesn't try to increase the
/// maximum cable length.
/// </summary>
/// <remarks>
/// It can link either parts of the same vessel or parts of two different vessels.
/// </remarks>
public sealed class KASModuleCableJoint : AbstractLinkJoint,
    // KAS interfaces.
    IKasJointEventsListener,
    // KSPDev sugar interfaces.
    IsPhysicalObject {

  #region Part's config fields
  /// <summary>
  /// Force per one meter of the stretched cable to apply to keep the object close to each other.
  /// </summary>
  /// <remarks>A too high value may result in the joint destruction.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableStrength = 1000f;

  /// <summary>Force to apply to damper oscillations.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableSpringDamper = 1f;
  #endregion

  #region Local fields and properties
  /// <summary>Threshold for determining if there is no cable stretch.</summary>
  const float MinViableStretch = 0.0001f;

  /// <summary>Intermediate game object used to keep the joints.</summary>
  GameObject jointObj;

  /// <summary>Actual joint object.</summary>
  ConfigurableJoint springJoint {
    get {
      return customJoints.Count > 0 ? customJoints[0] : null;
    }
  }

  /// <summary>Maximum allowed distance between the linked objects.</summary>
  float maxJointDistance {
    get { return springJoint.linearLimit.limit; }
  }

  /// <summary>Gets current distance between the joint ends.</summary>
  float currentJointDistance {
    get {
      return Vector3.Distance(
          linkTarget.part.rb.transform.TransformPoint(springJoint.anchor),
          springJoint.connectedBody.transform.TransformPoint(springJoint.connectedAnchor));
    }
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public void FixedUpdate() {
    if (isLinked && linkSource.linkRenderer != null) {
      // Adjust texture on the cable to simulate stretching.
      var jointLength = maxJointDistance;
      var length = currentJointDistance;
      linkSource.linkRenderer.stretchRatio = length > jointLength ? length / jointLength : 1.0f;
    }
  }
  #endregion

  #region AbstractLinkJoint overrides
  /// <inheritdoc/>
  protected override void SetupPhysXJoints() {
    jointObj = new GameObject("RopeConnectorHead");
    jointObj.AddComponent<BrokenJointListener>().hostPart = part;
    // Joints behave crazy when the connected rigidbody masses differ to much. So use the average.
    var rb = jointObj.AddComponent<Rigidbody>();
    rb.mass = (linkSource.part.mass + linkTarget.part.mass) / 2;
    rb.useGravity = false;

    var cableJoint = jointObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(cableJoint);
    KASAPI.JointUtils.SetupDistanceJoint(cableJoint,
                                         springForce: cableStrength,
                                         springDamper: cableSpringDamper,
                                         maxDistance: originalLength);
    cableJoint.autoConfigureConnectedAnchor = false;
    cableJoint.connectedBody = linkSource.part.Rigidbody;
    cableJoint.anchor = Vector3.zero;
    cableJoint.connectedAnchor = linkSource.part.Rigidbody.transform.InverseTransformPoint(
        GetSourcePhysicalAnchor(linkSource));
    SetBreakForces(cableJoint);
    
    // Move plug head to the target and adhere it there at the attach node transform.
    jointObj.transform.position = GetTargetPhysicalAnchor(linkSource, linkTarget);
    var fixedJoint = jointObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(fixedJoint);
    KASAPI.JointUtils.SetupFixedJoint(fixedJoint);
    cableJoint.enablePreprocessing = true;
    fixedJoint.autoConfigureConnectedAnchor = false;
    fixedJoint.connectedBody = linkTarget.part.Rigidbody;
    fixedJoint.anchor = Vector3.zero;
    fixedJoint.connectedAnchor = linkTarget.part.Rigidbody.transform.InverseTransformPoint(
        GetTargetPhysicalAnchor(linkSource, linkTarget));
    SetBreakForces(fixedJoint);

    // The order of adding the joints is important!
    SetCustomJoints(new[] {cableJoint, fixedJoint});
  }

  /// <inheritdoc/>
  protected override void CleanupPhysXJoints() {
    base.CleanupPhysXJoints();
    Destroy(jointObj);
    jointObj = null;
  }
  #endregion

  #region IKasJointEventsListener implementation
  /// <inheritdoc/>
  public void OnKASJointBreak(GameObject hostObj, float breakForce) {
    linkSource.BreakCurrentLink(LinkActorType.Physics);
  }
  #endregion
}

}  // namespace
