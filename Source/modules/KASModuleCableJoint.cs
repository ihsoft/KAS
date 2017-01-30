// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using System;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that ties two parts with a cable. It allows any movement that doesn't try to increase the
/// maximum cable length.
/// </summary>
/// <remarks>
/// It can link either parts of the same vessel or parts of two different vessels.
/// </remarks>
public sealed class KASModuleCableJoint : AbstractJointModule,
    // KAS interfaces.
    IKasJointEventsListener,
    // KSPDev sugar interfaces.
    IsPhysicalObject {

  #region Localizable strings
  /// <summary>Message to show when cable stretch is checked, and it's close to zero.</summary>
  readonly static Message NotStretchedMsg = "Cable is not stretched";
  /// <summary>Message to report cable stretch ratio when it's not zero.</summary>
  readonly static Message<float> StretchRatioMsg = "Cable stretch: {0:0.##}%";
  #endregion

  #region Part's config fields
  /// <summary>
  /// Config setting. Force per one meter of the stretched cable to apply to keep the object close
  /// to each other. Too high value may result in joint destruction.
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
  public float cableStrength = 1000f;

  /// <summary>Config setting. Force to apply to damper oscillations.</summary>
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
  public float cableSpringDamper = 1f;
  #endregion

  /// <summary>Threshold for determining if there is no cable stretch.</summary>
  const float MinViableStretch = 0.0001f;

  /// <summary>Intermediate game object used to keep the joints.</summary>
  GameObject jointObj;

  /// <summary>Actual joint object.</summary>
  SpringJoint springJoint;

  /// <summary>Renderer for the link. Can be <c>null</c>.</summary>
  ILinkRenderer renderer;

  /// <summary>Maximum allowed distance between the linked objects.</summary>
  float maxJointLength {
    get { return springJoint.maxDistance; }
    set { springJoint.maxDistance = value; }
  }

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public void FixedUpdate() {
    if (renderer != null) {
      var jointLength = maxJointLength;
      var length =
          Vector3.Distance(linkSource.nodeTransform.position, linkTarget.nodeTransform.position);
      renderer.stretchRatio = length > jointLength ? length / jointLength : 1.0f;
    }
  }
  #endregion

  #region Override AbstractJointModule
  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    
    //FIXME
    linkBreakForce = 100;
    linkBreakTorque = 100;
    
    UpdateMenuItems();
  }
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public override void CreateJoint(ILinkSource source, ILinkTarget target) {
    base.CreateJoint(source, target);
    renderer = part.FindModuleImplementing<ILinkRenderer>();
    CreateDistanceJoint(source, target);
    UpdateMenuItems();
  }

  /// <inheritdoc/>
  public override void DropJoint() {
    base.DropJoint();
    Destroy(springJoint);
    springJoint = null;
    Destroy(jointObj);
    jointObj = null;
    renderer = null;
    UpdateMenuItems();
  }

  /// <inheritdoc/>
  public override void AdjustJoint(bool isUnbreakable = false) {
    springJoint.breakForce =
        isUnbreakable ? Mathf.Infinity : GetClampedBreakingForce(linkBreakTorque);
    springJoint.breakTorque =
        isUnbreakable ? Mathf.Infinity : GetClampedBreakingTorque(linkBreakForce);
  }
  #endregion

  #region IKasJointEventsListener implementation
  /// <inheritdoc/>
  public void OnKASJointBreak(GameObject hostObj, float breakForce) {
    linkSource.BreakCurrentLink(LinkActorType.Physics);
  }
  #endregion

  #region GUI action handlers
  /// <summary>Sets cable limit to the maxumum length.</summary>
  [KSPEvent(guiName = "Release cable", guiActive = true, guiActiveUnfocused = true)]
  public void ReleaseLockContextMenuAction() {
    maxJointLength = maxLinkLength;
    UpdateMenuItems();
  }

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

  /// <summary>Returns ratio of the current cable stretch.</summary>
  /// <returns>
  /// <c>0</c> if cable is not stretched. Percentile of the stretching otherwsie. I.e. if cable's
  /// original length was <c>100</c> and the current length is <c>110</c> then stretch ratio is
  /// <c>0.1</c> (10%).
  /// </returns>
  public float GetCableStretch() {
    var length =
        Vector3.Distance(linkSource.nodeTransform.position, linkTarget.nodeTransform.position);
    var stretch = length - maxJointLength;
    if (stretch < MinViableStretch) {
      return 0f;
    }
    return stretch / maxJointLength;
  }

  #region Loacl utility methods
  /// <summary>Creates a distant joint between the source and the target.</summary>
  void CreateDistanceJoint(ILinkSource source, ILinkTarget target) {
    jointObj = new GameObject("RopeConnectorHead");
    jointObj.AddComponent<BrokenJointListener>().hostPart = part;
    // Joints behave crazy when connected rigidbody masses differ to much. So use the average.
    var rb = jointObj.AddComponent<Rigidbody>();
    rb.mass = (source.part.mass + target.part.mass) / 2;

    // Temporarily align to the source to have spring joint remembered zero length.
    jointObj.transform.parent = source.part.transform;
    jointObj.transform.localPosition = Vector3.zero;

    springJoint = jointObj.AddComponent<SpringJoint>();
    springJoint.spring = cableStrength;
    springJoint.damper = cableSpringDamper;
    springJoint.enableCollision = true;
    springJoint.breakTorque = GetClampedBreakingTorque(linkBreakForce);
    springJoint.breakForce = GetClampedBreakingForce(linkBreakTorque);
    springJoint.maxDistance = originalLength;
    springJoint.connectedBody = source.part.rb;
    springJoint.enablePreprocessing = false;

    // Move plug head to the target and adhere it there.
    jointObj.transform.parent = target.part.transform;
    jointObj.transform.localPosition = Vector3.zero;
    var fixedJoint = jointObj.AddComponent<FixedJoint>();
    fixedJoint.connectedBody = target.part.rb;
    fixedJoint.breakForce = Mathf.Infinity;
    fixedJoint.breakTorque = Mathf.Infinity;
    jointObj.transform.parent = jointObj.transform;
  }

  /// <summary>Updates GUI context menu items to the current state of the module.</summary>
  void UpdateMenuItems() {
    Events["ReleaseLockContextMenuAction"].active =
        isLinked && !Mathf.Approximately(maxJointLength, maxLinkLength);
    Events["CheckCableStretchContextMenuAction"].active = isLinked;
  }
  #endregion
}

}  // namespace
