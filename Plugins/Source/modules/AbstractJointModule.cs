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
    PartModule, IModuleInfo, IActivateOnDecouple,
    // KAS parents.
    ILinkJoint,
    // Syntax sugar parents.
    IPartModule, IsPackable, IsDestroyable, IKSPDevModuleInfo, IKSPActivateOnDecouple {

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

  // Configurable joint settings used by the KSP stock joints as of KSP 1.1.3.  
  protected const float StockJointBreakingForce = 9600;
  protected const float StockJointBreakingTorque = 16000;
  protected const float StockJointAngleLimit = 177;
  protected const float StockJointLinearLimit = 1;
  protected const float StockJointSpring = 30000;

  #region PartModule overrides
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    // Source & target modules may not be started at this moment yet but they are loaded.
    ILinkSource source;
    ILinkTarget target;
    if (FindLinkEnds(part, out source, out target)) {
      CreateJoint(source, target);  // Restore joint state.
    }
  }
  #endregion

  public static bool FindLinkEnds(Part p, out ILinkSource source, out ILinkTarget target) {
    // Only one source on the part can be linked.
    source = p.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(x => x.linkState == LinkState.Linked);
    if (source == null || source.attachNode == null || source.attachNode.attachedPart == null) {
      target = null;
      return false;
    }
    // Any number of targets can be linked but only one is linked with this part.
    target = source.attachNode.attachedPart.FindModulesImplementing<ILinkTarget>()
        .FirstOrDefault(x => x.attachNode != null && x.attachNode.attachedPart == p);
    return target != null;
  }


  public static bool IsPartLinked(Part p) {
    ILinkSource source;
    ILinkTarget target;
    return FindLinkEnds(p, out source, out target);
  }

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public virtual void CreateJoint(ILinkSource source, ILinkTarget target) {
    //FIXME
    Debug.LogWarningFormat("** CreateJoint: {0}", DumpJoint(source, target));
    DropJoint();
    linkSource = source;
    linkTarget = target;
    originalLength = Vector3.Distance(source.nodeTransform.position, target.nodeTransform.position);
    isLinked = true;
    // Prevent stock game logic to behave on the joint if there is any.
    if (part.attachJoint != null && part.attachJoint.Target == target.part) {
      //FIXME
      Debug.LogWarningFormat("** DROP stock joint");
      part.attachJoint = null;
    }
  }

  /// <inheritdoc/>
  public virtual void DropJoint() {
    //FIXME
    Debug.LogWarningFormat("** DropJoint: {0}", DumpJoint(linkSource, linkTarget));
    linkSource = null;
    linkTarget = null;
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
    // Check 
//    if (!isLinked) {
//      // The source is already initialized at this moment. Its state can be used to determine the
//      // link state.
//      var source = part.FindModulesImplementing<ILinkSource>()
//          .FirstOrDefault(x => x.linkState == LinkState.Linked);
//      var target = source != null ? source.linkTarget : null;
//      if (isLinked && source != null && target != null) {
//        var limitError =
//            CheckAngleLimitAtSource(source, target.nodeTransform)
//            ?? CheckAngleLimitAtTarget(source, target.nodeTransform)
//            ?? CheckLengthLimit(source, target.nodeTransform);
//        if (limitError != null) {
//          Debug.LogErrorFormat(
//              "Cannot restore joint {0}: {1}", DumpJoint(source, target), limitError);
//          StartCoroutine(WaitAndDisconnectPart());  // Cannot restore. Disconnect.
//          isLinked = false;
//        } else {
//          CreateJoint(source, target);  // Restore joint state.
//        }
//      }
//    }
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

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    //FIXME
    Debug.LogWarningFormat("** ON DECOUPLE: nodename={0}, weDcouple={1}", nodeName, weDecouple);
    if (weDecouple) {
      DropJoint();
    }
  }
  #endregion

  #region Utility methods
  /// <summary>Returns a logs friendly string description of the link.</summary>
  protected static string DumpJoint(ILinkSource source, ILinkTarget target) {
    var srcTitle = source != null
        ? string.Format("{0} at {1} (id={2})",
                        source.part.name, source.cfgAttachNodeName, source.part.flightID)
        : "NOTHING";
    var trgTitle = target != null
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
  /// <remarks>It's not a normal unlinking action. Only part's connection is broken.</remarks>
  IEnumerator WaitAndDisconnectPart() {
    yield return new WaitForEndOfFrame();
    Debug.LogErrorFormat("Detach part {0} from the parent since the link is invalid.", part.name);
    part.decouple();  // Link source is expected to react on decouple event.
  }
  #endregion
}

}  // namespace
