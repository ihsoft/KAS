// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md
using System;
using System.Linq;
using System.Collections;
using System.Text;
using UnityEngine;
using KASAPIv1;
using KSPDev.KSPInterfaces;

namespace KAS {

//FIXME docs
public abstract class AbstractJoint : ILinkJoint {
  #region ILinkJoint properties.
  /// <inheritdoc/>
  public virtual float cfgMinLinkLength {set; get; }
  /// <inheritdoc/>
  public virtual float cfgMaxLinkLength {set; get; }
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  /// <summary>Breaking force for the strut connecting the two parts.</summary>
  /// <remarks>If <c>0</c> then joint is unbreakable. Note, that this force is measured in any
  /// direction of the joint ends movement. E.g. if limit is <c>10</c> then the joint will break
  /// when either source or target object get affected by a force of 10+. The direction of the force
  /// is not important, i.e. this limit is <i>not</i> a link stretch force limit.</remarks>
  /// FIXME: investigate
  [KSPField]
  public float strutBreakForce = 0f;
  /// <summary>Breaking torque for the strut connecting the two parts.</summary>
  /// <remarks>If <c>0</c> then joint is unbreakable by the torque.</remarks>
  [KSPField]
  public float strutBreakTorque = 0f;
  /// <summary>Degree of freedom for the sphere joint at the source part.</summary>
  /// <remarks>If <c>0</c> then joint becomes locked.</remarks>
  [KSPField]
  public int sourceLinkAngleLimit = 0;
  /// <summary>Degree of freedom for the sphere joint at the target part.</summary>
  /// <remarks>If <c>0</c> then joint becomes locked.</remarks>
  [KSPField]
  public int targetLinkAngleLimit = 0;
  [KSPField]
  public float minLinkLength = 0f;
  [KSPField]
  public float maxLinkLength = Mathf.Infinity;
  #endregion

  #region Localizable GUI strings
  protected const string MinLengthLimitReachedMsg = "Link is too short: {0:F2} m < {1:F2} m";
  protected const string MaxLengthLimitReachedMsg = "Link is too long: {0:F2} m > {1:F2} m";
  protected const string SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0} deg > {1} deg";
  protected const string TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0} deg > {1} deg";
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public abstract void SetupJoint(ILinkSource source, ILinkTarget target);

  /// <inheritdoc/>
  public abstract void CleanupJoint();

  /// <inheritdoc/>
  public virtual string CheckLengthLimit(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(source.nodeTransform.position, targetTransform.position);
    return length > maxLinkLength
        ? string.Format(MaxLengthLimitReachedMsg, length, maxLinkLength)
        : null;
  }

  /// <inheritdoc/>
  public virtual string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform) {
    var linkVector = targetTransform.position - source.nodeTransform.position;
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return angle > sourceLinkAngleLimit
        ? string.Format(SourceNodeAngleLimitReachedMsg, angle, sourceLinkAngleLimit)
        : null;
  }

  /// <inheritdoc/>
  public virtual string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform) {
    var linkVector = source.nodeTransform.position - targetTransform.position;
    var angle = Vector3.Angle(targetTransform.rotation * Vector3.forward, linkVector);
    return angle > targetLinkAngleLimit
        ? string.Format(TargetNodeAngleLimitReachedMsg, angle, targetLinkAngleLimit)
        : null;
  }
  #endregion

  /// <summary>Destroys child objects when joint is destroyed.</summary>
  /// <para>Overridden from <see cref="MonoBehaviour"/>.</para>
  void OnDestroy() {
    CleanupJoint();
  }
}

}  // namespace
