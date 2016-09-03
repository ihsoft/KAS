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

namespace KAS {

public class KASModuleStockJoint : PartModule, IModuleInfo, ILinkJoint {
  public float cfgMinLinkLength { get { return minLinkLength; } }

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  //FIXME: move to superclass
  [KSPField]
  public float minLinkLength = 0;
  [KSPField]
  public float maxLinkLength = Mathf.Infinity;
  //FIXME: separate to src & trg angle limits
  //FIXME: BETTER: get target limits from the target. it sounds cool
  [KSPField]
  public float linkAngleLimit = 0f;
  /// <summary>Breaking force for the strut connecting the two parts.</summary>
  [KSPField]
  public float jointBreakForce = Mathf.Infinity;
  [KSPField]
  public float jointDamperRatio = Mathf.Infinity;
  #endregion

  //FIXME: move to super class
  protected ILinkSource linkSource { get; private set; }
  protected ILinkTarget linkTarget { get; private set; }
  protected float originalLength { get; private set; }

  #region Localizable GUI strings
  protected static string LengthMaxLimitReachedMsg =
      "Link length is over the limit: {0:F2} m > {1:F2} m";
  protected static string LengthMinLimitReachedMsg =
      "Link length is below the limit: {0:F2} m < {1:F2} m";
  protected static string TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0} deg > {1:F0} deg";
  protected static string SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0} deg > {1:F0} deg";
  #endregion

  #region ILinkJoint implementation
  public override void OnAwake() {
    Debug.LogWarningFormat("*************** ONAWAKE! {0}", part.name);
    base.OnAwake();
  }

  //FIXME: make it virtual from the super class
  /// <inheritdoc/>
  public void SetupJoint(ILinkSource source, ILinkTarget target) {
    CleanupJoint();
    linkSource = source;
    linkTarget = target;
  }

  //FIXME: make it virtual from the super class
  /// <inheritdoc/>
  public void CleanupJoint() {
    linkSource = null;
    linkTarget = null;
  }

  //FIXME: make it virtual from the super class
  /// <inheritdoc/>
  public string CheckLengthLimit(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(source.nodeTransform.position, targetTransform.position);
    if (length < minLinkLength) {
      return string.Format(LengthMinLimitReachedMsg, length, minLinkLength);
    }
    if (length > maxLinkLength) {
      return string.Format(LengthMaxLimitReachedMsg, length, maxLinkLength);
    }
    return null;
  }
  
  /// <inheritdoc/>
  public string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform) {
    var linkVector = targetTransform.position - source.nodeTransform.position;
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return angle > linkAngleLimit
        ? string.Format(SourceNodeAngleLimitReachedMsg, angle, linkAngleLimit)
        : null;
  }

  /// <inheritdoc/>
  public string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform) {
    var linkVector = source.nodeTransform.position - targetTransform.position;
    var angle = Vector3.Angle(targetTransform.rotation * Vector3.forward, linkVector);
    return angle > linkAngleLimit
        ? string.Format(TargetNodeAngleLimitReachedMsg, angle, linkAngleLimit)
        : null;
  }
  #endregion

  /// <summary>Destroys child objects when joint is destroyed.</summary>
  /// <para>Overridden from <see cref="MonoBehaviour"/>.</para>
  //FIXME: move to the super class
  protected virtual void OnDestroy() {
    CleanupJoint();
  }

  #region IModuleInfo implementation
  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>Declared as virtual is <see cref="PartModule"/> and, hence, needs to be overridden.
  /// Though, it's also a part of <see cref="IModuleInfo"/>.</remarks>
  /// <returns>HTML formatted text to show the in GUI.</returns>
  /// FIXME: move strings to constants.
  /// FIXME: is it HTML?
  /// FIXME: move to the super class
  public override string GetInfo() {
    //FIXME: get super class strings first
    var sb = new StringBuilder();
    sb.AppendFormat("Link break force: {0:F1} N\n", jointBreakForce);
    sb.AppendFormat("Minimum link length: {0:F1} m\n", minLinkLength);
    sb.AppendFormat("Maximum link length: {0:F1} m\n", maxLinkLength);
    return sb.ToString();
  }

  /// <summary>Returns module title to show in the editor part's details panel.</summary>
  /// <returns>Title of the module.</returns>
  public virtual string GetModuleTitle() {
    return "KAS Joint";
  }

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  public virtual Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  public virtual string GetPrimaryField() {
    return null;
  }
  #endregion
}

}  // namespace
