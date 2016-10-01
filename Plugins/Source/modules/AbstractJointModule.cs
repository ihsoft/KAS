// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.GUIUtils;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

//FIXME docs
// Callbacks:
// In-flight load: RestoreJoint => AdjustJoint
// In-flight new: CreateJoint => X * (AdjustJoint(false) => AdjustJoint(true)) on time warp.
// In-flight drop: DropJoint
public abstract class AbstractJointModule :
    // KSP parents.
    PartModule, IModuleInfo,
    // KAS parents.
    ILinkJoint, ILinkStateEventListener,
    // Syntax sugar parents.
    IPartModule, IsPackable, IsDestroyable, IKSPDevModuleInfo {

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
  protected static Message<float> InfoLinkLinearStrength = "Link break force: {0:F1}N";
  protected static Message<float> InfoLinkBreakStrength = "Link torque force: {0:F1}N";
  protected static Message<float> InfoMinimumLinkLength = "Minimum link length: {0:F1}m";
  protected static Message<float> InfoMaximumLinkLength = "Maximum link length: {0:F1}m";
  protected static Message<float> InfoSourceJointFreedom = "Source angle limit: {0}deg";
  protected static Message<float> InfoTargetJointFreedom = "Target angle limit: {0}deg";
  #endregion

  #region ILinkJoint CFG properties
  /// <inheritdoc/>
  public float cfgMinLinkLength { get { return minLinkLength; } }
  /// <inheritdoc/>
  public float cfgMaxLinkLength { get { return maxLinkLength; } }
  /// <inheritdoc/>
  public float cfgLinkBreakForce { get { return linkBreakForce; } }
  /// <inheritdoc/>
  public float cfgLinkBreakTorque { get { return linkBreakTorque; } }
  /// <inheritdoc/>
  public int cfgSourceLinkAngleLimit { get { return sourceLinkAngleLimit; } }
  /// <inheritdoc/>
  public int cfgTargetLinkAngleLimit { get { return targetLinkAngleLimit; } }
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public float linkBreakForce = 0;
  [KSPField]
  public float linkBreakTorque = 0;
  [KSPField]
  public int sourceLinkAngleLimit = 0;
  [KSPField]
  public int targetLinkAngleLimit = 0;
  [KSPField]
  public float minLinkLength = 0;
  [KSPField]
  public float maxLinkLength = 0;
  #endregion

  //FIXME docs
  protected ILinkSource linkSource { get; private set; }
  protected ILinkTarget linkTarget { get; private set; }
  protected float originalLength { get; private set; }
  protected bool isLinked { get; private set; }
  protected PartJoint stockJoint { get; private set; }

  #region Configurable joint settings used by the KSP stock joints as of KSP 1.1.3.  
  protected const float StockJointBreakingForce = 9600;
  protected const float StockJointBreakingTorque = 16000;
  protected const float StockJointAngleLimit = 177;
  protected const float StockJointLinearLimit = 1;
  protected const float StockJointSpring = 30000;
  #endregion

  bool isRestored;

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public virtual void CreateJoint(ILinkSource source, ILinkTarget target) {
    //FIXME
    Debug.LogWarningFormat("** CreateJoint: {0}", DumpJoint(source, target));
    DropJoint();
    linkSource = source;
    linkTarget = target;
    if (part.attachJoint != null && part.attachJoint.Target == target.part) {
      //FIXME
      Debug.LogWarningFormat("** DROP stock joint");
      stockJoint = part.attachJoint;
      part.attachJoint = null;
    }
    originalLength = Vector3.Distance(source.nodeTransform.position, target.nodeTransform.position);
    isLinked = true;
  }

  /// <inheritdoc/>
  public virtual void DropJoint() {
    //FIXME
    Debug.LogWarningFormat("** DropJoint: {0}", DumpJoint(linkSource, linkTarget));
    linkSource = null;
    linkTarget = null;
    stockJoint = null;
    isLinked = false;
  }

  /// <inheritdoc/>
  public abstract void AdjustJoint(bool isUnbreakable = false);

  /// <inheritdoc/>
  public virtual string CheckLengthLimit(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(source.nodeTransform.position, targetTransform.position);
    if (maxLinkLength > 0 && length > maxLinkLength) {
      return MaxLengthLimitReachedMsg.Format(length, maxLinkLength);
    }
    if (minLinkLength > 0 && length < minLinkLength) {
      return MinLengthLimitReachedMsg.Format(length, minLinkLength);
    }
    return null;
  }

  /// <inheritdoc/>
  public virtual string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform) {
    var linkVector = targetTransform.position - source.nodeTransform.position;
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return sourceLinkAngleLimit > 0 && angle > sourceLinkAngleLimit
        ? SourceNodeAngleLimitReachedMsg.Format(angle, sourceLinkAngleLimit)
        : null;
  }

  /// <inheritdoc/>
  public virtual string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform) {
    var linkVector = source.nodeTransform.position - targetTransform.position;
    var angle = Vector3.Angle(targetTransform.rotation * Vector3.forward, linkVector);
    return targetLinkAngleLimit > 0 && angle > targetLinkAngleLimit
        ? TargetNodeAngleLimitReachedMsg.Format(angle, targetLinkAngleLimit)
        : null;
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    if (linkBreakForce > 0) {
      sb.AppendLine(InfoLinkLinearStrength.Format(linkBreakForce));
    }
    if (linkBreakTorque > 0) {
      sb.AppendLine(InfoLinkBreakStrength.Format(linkBreakTorque));
    }
    if (minLinkLength > 0) {
      sb.AppendLine(InfoMinimumLinkLength.Format(minLinkLength));
    }
    if (maxLinkLength > 0) {
      sb.AppendLine(InfoMaximumLinkLength.Format(maxLinkLength));
    }
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

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    DropJoint();
  }
  #endregion

  #region IsPackable implementation
  /// <inheritdoc/>
  public virtual void OnPartUnpack() {
    //FIXME: make joints normal
    Debug.LogWarningFormat("** JOINT UNPACK: joint={0}", part.attachJoint);
    // Restore joint state. Don't do it in OnStart since we need partJoint created. 
    if (!isRestored) {
      var source = part.FindModulesImplementing<ILinkSource>()
          .FirstOrDefault(x => x.linkState == LinkState.Linked);
      var target = source != null ? source.linkTarget : null;
      if (source != null && target != null) {
        var limitError =
            CheckAngleLimitAtSource(source, target.nodeTransform)
            ?? CheckAngleLimitAtTarget(source, target.nodeTransform)
            ?? CheckLengthLimit(source, target.nodeTransform);
        if (limitError != null) {
          Debug.LogErrorFormat(
              "Cannot restore joint {0}: {1}", DumpJoint(source, target), limitError);
          // Cannot restore. Disconnect.
          StartCoroutine(WaitAndDisconnectPart(limitError, part.parent));
        } else {
          CreateJoint(source, target);  // Restore joint state.
        }
      }
      isRestored = true;
    }

    if (isLinked) {
      AdjustJoint();
    }
  }

  /// <inheritdoc/>
  public virtual void OnPartPack() {
    //FIXME: make joints undestructable
    Debug.LogWarning("** JOINT PACK");
    if (isLinked) {
      AdjustJoint(isUnbreakable: true);
    }
  }
  #endregion

  #region ILinkEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    //FIXME
    Debug.LogWarningFormat("** JOINT: created event");
    CreateJoint(info.source, info.target);
  }

  /// <inheritdoc/>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    //FIXME
    Debug.LogWarningFormat("** JOINT: broken event, DROPPING!");
    DropJoint();
  }
  #endregion

  #region Utility methods
  /// <summary>Returns a logs friendly string description of the link.</summary>
  protected static string DumpJoint(ILinkSource source, ILinkTarget target) {
    var srcTitle = source != null && source.part != null 
        ? string.Format("{0} at {1} (id={2})",
                        source.part.name, source.cfgAttachNodeName, source.part.flightID)
        : "NOTHING";
    var trgTitle = target != null && target.part != null
        ? string.Format("{0} at {1} (id={2})",
                        target.part.name, target.cfgAttachNodeName, target.part.flightID)
        : "NOTHING";
    return srcTitle + " => " + trgTitle;
  }

  /// <summary>
  /// Setups joint break force and torque while handling special values from config.
  /// </summary>
  /// <param name="joint">Joint to set forces for.</param>
  /// <param name="forceFromConfig">
  /// Break force from the config. If it's <c>0</c> then force will be the same as for the stock
  /// joints.
  /// </param>
  /// <param name="torqueFromConfig">
  /// Break torque from the config. If it's <c>0</c> then torque will be the same as for the stock
  /// joints.
  /// </param>
  /// <seealso cref="StockJointBreakingForce"/>
  /// <seealso cref="StockJointBreakingTorque"/>
  protected static void SetBreakForces(
      ConfigurableJoint joint, float forceFromConfig, float torqueFromConfig) {
    joint.breakForce =
        Mathf.Approximately(forceFromConfig, 0) ? StockJointBreakingForce : forceFromConfig;
    joint.breakTorque =
        Mathf.Approximately(torqueFromConfig, 0) ? StockJointBreakingTorque : torqueFromConfig;
  }
  #endregion

  #region Local utility methods
  /// <summary>Disconnects part at the end of the frame.</summary>
  /// <remarks>It triggers external link breaking.</remarks>
  /// <param name="reason">Error string to report on UI.</param>
  /// <param name="oldParent">
  /// Parent of the part at the moment of making decouple decision. A safety measure to not trigger
  /// NRE if part got disconnected before end of frame.
  /// </param>
  IEnumerator WaitAndDisconnectPart(string reason, Part oldParent) {
    yield return new WaitForEndOfFrame();
    if (part.parent == oldParent) {
      Debug.LogWarningFormat(
          "Detach part {0} from the parent since the link is invalid.", part.name);
      ScreenMessaging.ShowErrorScreenMessage(reason);
      part.decouple();  // Link source is expected to react on decouple event.
    }
  }
  #endregion
}

}  // namespace
