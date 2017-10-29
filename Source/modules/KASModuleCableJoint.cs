// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.PartUtils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that ties two parts with a cable. It allows any movement that doesn't try to increase the
/// maximum cable length.
/// </summary>
/// <remarks>
/// It can link either parts of the same vessel or parts of two different vessels.
/// </remarks>
public sealed class KASModuleCableJoint : KASModuleJointBase,
    // KAS interfaces.
    IKasJointEventsListener, IHasContextMenu,
    // KSPDev sugar interfaces.
    IsPhysicalObject {

  #region Localizable strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NotStretchedMsg = new Message(
      "#kasLOC_06000",
      defaultTemplate: "Cable is not stretched",
      description: "Message to show when cable stretch is checked, and it's close to zero.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.PercentType']/*"/>
  static readonly Message<PercentType> StretchRatioMsg = new Message<PercentType>(
      "#kasLOC_06001",
      defaultTemplate: "Cable stretch: <<1>>",
      description: "Message to report the cable stretch ratio when it's not zero."
      + "\nArgument <<1>> is a ratio between the joint limit and the actual length.",
      example: "Cable stretch: 1.25%");
  #endregion

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

  /// <summary>Threshold for determining if there is no cable stretch.</summary>
  const float MinViableStretch = 0.0001f;

  /// <summary>Intermediate game object used to keep the joints.</summary>
  GameObject jointObj;

  /// <summary>Actual joint object.</summary>
  ConfigurableJoint springJoint {
    get {
      return customJoints != null && customJoints.Count > 0
          ? customJoints[0]
          : null;
    }
  }

  /// <summary>Renderer for the link. Can be <c>null</c>.</summary>
  ILinkRenderer renderer;

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

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(
        this, CheckCableStretchContextMenuAction, e => e.active = isLinked);
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public void FixedUpdate() {
    if (renderer != null) {
      // Adjust texture on the cable to simulate stretching.
      var jointLength = maxJointDistance;
      var length = currentJointDistance;
      renderer.stretchRatio = length > jointLength ? length / jointLength : 1.0f;
    }
  }
  #endregion

  #region PartModule overrides 
  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    UpdateContextMenu();
  }
  #endregion

  #region KASModuleJointBase overrides
  /// <inheritdoc/>
  protected override void AttachParts() {
    renderer = part.FindModuleImplementing<ILinkRenderer>();

    jointObj = new GameObject("RopeConnectorHead");
    jointObj.AddComponent<BrokenJointListener>().hostPart = part;
    // Joints behave crazy when the connected rigidbody masses differ to much. So use the average.
    var rb = jointObj.AddComponent<Rigidbody>();
    rb.mass = (linkSource.part.mass + linkTarget.part.mass) / 2;
    rb.useGravity = false;

    // Temporarily align to the source to have the spring joint remembered zero length.
    jointObj.transform.parent = linkSource.physicalAnchorTransform;
    jointObj.transform.localPosition = Vector3.zero;
    var cableJoint = jointObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(cableJoint);
    KASAPI.JointUtils.SetupDistanceJoint(cableJoint,
                                         springForce: cableStrength,
                                         springDamper: cableSpringDamper,
                                         maxDistance: originalLength);
    cableJoint.autoConfigureConnectedAnchor = false;
    cableJoint.anchor = Vector3.zero;
    cableJoint.connectedBody = linkSource.part.Rigidbody;
    cableJoint.connectedAnchor = linkSource.part.Rigidbody.transform.InverseTransformPoint(
        linkSource.physicalAnchorTransform.position);
    SetBreakForces(cableJoint);
    
    // Move plug head to the target and adhere it there at the attach node transform.
    jointObj.transform.parent = linkTarget.physicalAnchorTransform;
    jointObj.transform.localPosition = Vector3.zero;
    var fixedJoint = jointObj.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(fixedJoint);
    KASAPI.JointUtils.SetupFixedJoint(fixedJoint);
    cableJoint.enablePreprocessing = true;
    fixedJoint.autoConfigureConnectedAnchor = false;
    fixedJoint.anchor = Vector3.zero;
    fixedJoint.connectedBody = linkTarget.part.Rigidbody;
    fixedJoint.connectedAnchor = linkTarget.part.Rigidbody.transform.InverseTransformPoint(
        linkTarget.physicalAnchorTransform.position);
    SetBreakForces(fixedJoint);
    jointObj.transform.parent = jointObj.transform;

    // The order of adding the joints is important!
    customJoints = new List<ConfigurableJoint>();
    customJoints.Add(cableJoint);
    customJoints.Add(fixedJoint);
  }

  /// <inheritdoc/>
  protected override void DetachParts() {
    base.DetachParts();
    Destroy(jointObj);
    jointObj = null;
    renderer = null;
  }

  /// <inheritdoc/>
  protected override void OnStateChanged(bool oldIsLinked) {
    UpdateContextMenu();
  }
  #endregion

  #region IKasJointEventsListener implementation
  /// <inheritdoc/>
  public void OnKASJointBreak(GameObject hostObj, float breakForce) {
    linkSource.BreakCurrentLink(LinkActorType.Physics);
  }
  #endregion

  #region GUI action handlers
  /// <summary>
  /// Context menu action that triggers current stretch ration check. Result is reported to UI.
  /// </summary>
  [KSPEvent(guiName = "Check cable stretch", guiActive = true, guiActiveUnfocused = true)]
  public void CheckCableStretchContextMenuAction() {
    var stretchRatio = GetCableStretch();
    if (stretchRatio <= MinViableStretch) {
      ScreenMessages.PostScreenMessage(
          NotStretchedMsg, ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_LEFT);
    } else {
      ScreenMessages.PostScreenMessage(
          StretchRatioMsg.Format(stretchRatio * 100),
          ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_LEFT);
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Returns ratio of the current cable stretch.</summary>
  /// <returns>
  /// <c>0</c> if cable is not stretched. Percentile of the stretching otherwsie. I.e. if cable's
  /// original length was <c>100</c> and the current length is <c>110</c> then stretch ratio is
  /// <c>0.1</c> (10%).
  /// </returns>
  float GetCableStretch() {
    var stretch = currentJointDistance - maxJointDistance;
    if (stretch < MinViableStretch) {
      return 0f;
    }
    return stretch / maxJointDistance;
  }
  #endregion
}

}  // namespace
