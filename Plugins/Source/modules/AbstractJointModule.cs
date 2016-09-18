// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md
using System;
//using System.Linq;
using System.Text;
using UnityEngine;
using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.GUIUtils;

namespace KAS {

//FIXME docs
public abstract class AbstractJointModule : PartModule, IPartModule,
                                            IModuleInfo, IKSPDevModuleInfo,
                                            ILinkJoint {
  #region ILinkJoint properties.
  /// <inheritdoc/>
  public virtual float cfgMinLinkLength {
      set { minLinkLength = value; }
      get { return minLinkLength; }
  }
  /// <inheritdoc/>
  public virtual float cfgMaxLinkLength {
      set { maxLinkLength = value; }
      get { return maxLinkLength; }
  }
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  //FIXME: move all settings into interface
  /// <summary>Breaking force for the strut connecting the two parts.</summary>
  /// <remarks>If <c>0</c> then joint is unbreakable. Note, that this force is measured in any
  /// direction of the joint ends movement. E.g. if limit is <c>10</c> then the joint will break
  /// when either source or target object get affected by a force of 10+. The direction of the force
  /// is not important, i.e. this limit is <i>not</i> a link stretch force limit.</remarks>
  /// FIXME: investigate
  [KSPField]
  public float linkBreakForce = 0f;
  /// <summary>Breaking torque for the link connecting the two parts.</summary>
  [KSPField]
  public float linkBreakTorque = 0f;
  /// <summary>Degree of freedom at the source part.</summary>
  [KSPField]
  public int sourceLinkAngleLimit = 0;
  /// <summary>Degree of freedom at the target part.</summary>
  [KSPField]
  public int targetLinkAngleLimit = 0;
  [KSPField]
  public float minLinkLength = 0f;
  [KSPField]
  public float maxLinkLength = Mathf.Infinity;
  #endregion

  #region Localizable GUI strings
  protected static Message<float, float> MinLengthLimitReachedMsg =
      "Link is too short: {0:F2}m < {1:F2}m";
  protected static Message<float, float> MaxLengthLimitReachedMsg =
      "Link is too long: {0:F2}m > {1:F2}m";
  protected static Message<float, int> SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0}deg > {1}deg";
  protected static Message<float, int> TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0}deg > {1}deg";
  protected static Message ModuleTitle = "KAS Joint";
  protected static MessageSpecialFloatValue InfoLinkLinearStrength = new MessageSpecialFloatValue(
      "Link break force: {0:F1}N", 0, "Link break force: UNBREAKABLE");
  protected static MessageSpecialFloatValue InfoLinkBreakStrength =new MessageSpecialFloatValue(
      "Link torque force: {0:F1}N", 0, "Link torque force: UNBREAKABLE");
  protected static Message<float> InfoMinimumLinkLength = "Minimum link length: {0:F1}m";
  protected static Message<float> InfoMaximumLinkLength = "Maximum link length: {0:F1}m";
  protected static MessageSpecialFloatValue InfoSourceJointFreedom = new MessageSpecialFloatValue(
      "Source joint freedom: {0}deg", 0, "Source joint freedom: LOCKED");
  protected static MessageSpecialFloatValue InfoTargetJointFreedom = new MessageSpecialFloatValue(
      "Target joint freedom: {0}deg", 0, "Target joint freedom: LOCKED");
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public abstract void SetupJoint(ILinkSource source, ILinkTarget target);

  /// <inheritdoc/>
  public abstract void CleanupJoint();

  /// <inheritdoc/>
  public virtual string CheckLengthLimit(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(source.nodeTransform.position, targetTransform.position);
    if (length > maxLinkLength) {
      return MaxLengthLimitReachedMsg.Format(length, maxLinkLength);
    }
    if (length < minLinkLength) {
      return MinLengthLimitReachedMsg.Format(length, minLinkLength);
    }
    return null;
  }

  /// <inheritdoc/>
  public virtual string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform) {
    var linkVector = targetTransform.position - source.nodeTransform.position;
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return angle > sourceLinkAngleLimit
        ? SourceNodeAngleLimitReachedMsg.Format(angle, sourceLinkAngleLimit)
        : null;
  }

  /// <inheritdoc/>
  public virtual string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform) {
    var linkVector = source.nodeTransform.position - targetTransform.position;
    var angle = Vector3.Angle(targetTransform.rotation * Vector3.forward, linkVector);
    return angle > targetLinkAngleLimit
        ? TargetNodeAngleLimitReachedMsg.Format(angle, targetLinkAngleLimit)
        : null;
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.AppendLine(InfoLinkLinearStrength.Format(linkBreakForce));
    sb.AppendLine(InfoLinkBreakStrength.Format(linkBreakTorque));
    sb.AppendLine(InfoMinimumLinkLength.Format(minLinkLength));
    sb.AppendLine(InfoMaximumLinkLength.Format(maxLinkLength));
    sb.AppendLine(InfoSourceJointFreedom.Format(sourceLinkAngleLimit));
    sb.AppendLine(InfoTargetJointFreedom.Format(targetLinkAngleLimit));
    return sb.ToString();
  }

  /// <inheritdoc/>
  public string GetModuleTitle() {
    return ModuleTitle;
  }

  /// <inheritdoc/>
  public Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc/>
  public string GetPrimaryField() {
    return null;
  }
  #endregion

  /// <summary>Destroys child objects when joint is destroyed.</summary>
  /// <para>Overridden from <see cref="MonoBehaviour"/>.</para>
  void OnDestroy() {
    CleanupJoint();
  }
}

}  // namespace
