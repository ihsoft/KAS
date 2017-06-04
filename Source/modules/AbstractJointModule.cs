// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using KSPDev.KSPInterfaces;
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Module that controls joint on a KAS part.</summary>
/// <remarks>
/// This module reacts on KAS initated events to created/remove a physical joint between soucre and
/// target. This module only deals with joining two parts together. It does not deal with
/// collider(s) or rigid body masses (see <see cref="ILinkRenderer"/>).
/// </remarks>
public abstract class AbstractJointModule : PartModule,
    // KSP interfaces.
    IModuleInfo,
    // KAS interfaces.
    ILinkJoint, ILinkStateEventListener,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPackable, IsDestroyable, IKSPDevModuleInfo {

  #region Localizable GUI strings
  /// <summary>Message to display when link cannot be established because it's too short.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  protected readonly static Message<float, float> MinLengthLimitReachedMsg =
      "Link is too short: {0:F2}m < {1:F2}m";

  /// <summary>Message to display when link cannot be established because it's too long.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  protected readonly static Message<float, float> MaxLengthLimitReachedMsg =
      "Link is too long: {0:F2}m > {1:F2}m";

  /// <summary>
  /// Message to display when link cannot be established because maximum angle between link vector
  /// and the joint normal at the source part is to big.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  protected readonly static Message<float, int> SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0}deg > {1}deg";

  /// <summary>
  /// Message to display when link cannot be established because maximum angle between link vector
  /// and the joint normal at the target part is to big.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  protected readonly static Message<float, int> TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0}deg > {1}deg";

  /// <summary>Info string in the editor for link break force setting.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<float> LinkLinearStrengthInfo = "Link break force: {0:F1}N";

  /// <summary>Info string in the editor for link break torque setting.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<float> LinkBreakStrengthInfo = "Link torque force: {0:F1}N";

  /// <summary>Info string in the editor for minimum link length setting.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<float> MinimumLinkLengthInfo = "Minimum link length: {0:F1}m";

  /// <summary>Info string in the editor for maximum link length setting.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<float> MaximumLinkLengthInfo = "Maximum link length: {0:F1}m";

  /// <summary>Info string in the editor for maximum allowed angle at the source.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<float> SourceJointFreedomInfo = "Source angle limit: {0}deg";

  /// <summary>Info string in the editor for maximum allowed angle at the target.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected readonly static Message<float> TargetJointFreedomInfo = "Target angle limit: {0}deg";

  /// <summary>Title of the module to present in the editor details window.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message ModuleTitle = "KAS Joint";
  #endregion

  #region ILinkJoint CFG properties
  /// <inheritdoc/>
  /// <remarks>
  /// When calculating the strength, the minimum of the source and the target breaking forces is
  /// used as a base. Then, the value is scaled to the node size assuming it's a stack node.
  /// </remarks>
  /// <seealso cref="ScaleForceToNode"/>
  public float cfgLinkBreakForce { get { return linkBreakForce; } }

  /// <inheritdoc/>
  /// <remarks>
  /// When calculating the torque, the minimum of the source and the target breaking torque is
  /// used as a base. Then, the value is scaled to the node size assuming it's a stack node.
  /// </remarks>
  /// <seealso cref="ScaleForceToNode"/>
  public float cfgLinkBreakTorque { get { return linkBreakTorque; } }

  /// <inheritdoc/>
  public int cfgSourceLinkAngleLimit { get { return sourceLinkAngleLimit; } }

  /// <inheritdoc/>
  public int cfgTargetLinkAngleLimit { get { return targetLinkAngleLimit; } }

  /// <inheritdoc/>
  public float cfgMinLinkLength { get { return minLinkLength; } }

  /// <inheritdoc/>
  public float cfgMaxLinkLength { get { return maxLinkLength; } }
  #endregion

  #region Part's config fields
  /// <summary>Defines how the physics joint breaking force and torque are scaled.</summary>
  /// <remarks>
  /// The larger is the scale, the higher are the actual values used in physics. Size <c>0</c>
  /// matches the game's "tiny".
  /// </remarks>
  /// <seealso cref="linkBreakForce"/>
  /// <seealso cref="linkBreakTorque"/>
  /// <seealso cref="SetBreakForces"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int attachNodeSize = 0;

  /// <summary>
  /// The unscaled maximum force that can be applied on the joint before it breaks.
  /// </summary>
  /// <seealso cref="attachNodeSize"/>
  /// <seealso cref="cfgLinkBreakForce"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float linkBreakForce = 0;

  /// <summary>
  /// The unscaled maximum torque that can be applied on the joint before it breaks.
  /// </summary>
  /// <seealso cref="attachNodeSize"/>
  /// <seealso cref="cfgLinkBreakTorque"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float linkBreakTorque = 0;

  /// <summary>
  /// Maximum allowed angle between the attach node normal and the link at the source part.
  /// </summary>
  /// <seealso cref="cfgSourceLinkAngleLimit"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int sourceLinkAngleLimit = 0;

  /// <summary>
  /// Maximum allowed angle between the attach node normal and the link at the target part.
  /// </summary>
  /// <seealso cref="cfgTargetLinkAngleLimit"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int targetLinkAngleLimit = 0;

  /// <summary>Minumum allowed distance between the source and target parts.</summary>
  /// <seealso cref="cfgMinLinkLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float minLinkLength = 0;

  /// <summary>Maximum allowed distance between the source and target parts.</summary>
  /// <seealso cref="cfgMaxLinkLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float maxLinkLength = 0;
  #endregion

  #region Inheritable properties
  /// <summary>Source of the link.</summary>
  /// <value>Link module on the source part.</value>
  /// <remarks>
  /// When a vessel is loaded, the joint is restored in the "physics" method
  /// <see cref="OnPartUnpack"/>. Before this method had a chance to work the source is <c>null</c>.
  /// </remarks>
  protected ILinkSource linkSource { get; private set; }

  /// <summary>Target of the link.</summary>
  /// <value>Link module on the target part.</value>
  /// <remarks>
  /// When a vessel is loaded, the joint is restored in the "physics" method
  /// <see cref="OnPartUnpack"/>. Before this method had a chance to work the target is <c>null</c>.
  /// </remarks>
  protected ILinkTarget linkTarget { get; private set; }

  /// <summary>Length at the moment of creating the joint.</summary>
  /// <value>Distance in meters.</value>
  /// <remarks>
  /// The elastic joints may allow the length deviation. This value can be used as a base.
  /// </remarks>
  protected float originalLength { get; private set; }

  /// <summary>Tells if there is a physical joint created.</summary>
  /// <value><c>true</c> if the source and target parts are physically linked.</value>
  protected bool isLinked { get; private set; }

  /// <summary>Joint that was created by the KSP core to connect the two parts.</summary>
  /// <value>Joint object.</value>
  /// <remarks>
  /// Once the physics starts on part, the KSP core creates a joint and assigns it to
  /// <see cref="Part.attachJoint"/>. This module resets the stock joint to <c>null</c> to prevent
  /// the KSP logic on it, but it <i>does not</i> change the joint component on the part.
  /// The descendants must take care of the stock joint either by delegating the relevant events to
  /// it or by destroying it altogether.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_part.html#aa5a1e018fa5b47c5723aa0879e23c647">
  /// KSP: Part.attachJoint</seealso>
  protected PartJoint stockJoint { get; private set; }
  #endregion

  // Internal setting to determine if the joint has restored its state on the physics start.
  bool isRestored;

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public virtual void CreateJoint(ILinkSource source, ILinkTarget target) {
    if (isLinked) {
      HostedDebugLog.Warning(this, "Joint is already linked");
      return;
    }
    linkSource = source;
    linkTarget = target;
    if (part.attachJoint != null && part.attachJoint.Target == target.part) {
      stockJoint = part.attachJoint;
      part.attachJoint = null;
    }
    originalLength = Vector3.Distance(source.nodeTransform.position, target.nodeTransform.position);
    isLinked = true;
  }

  /// <inheritdoc/>
  public virtual void DropJoint() {
    linkSource = null;
    linkTarget = null;
    DropStockJoint();
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
      sb.AppendLine(LinkLinearStrengthInfo.Format(linkBreakForce));
    }
    if (linkBreakTorque > 0) {
      sb.AppendLine(LinkBreakStrengthInfo.Format(linkBreakTorque));
    }
    if (minLinkLength > 0) {
      sb.AppendLine(MinimumLinkLengthInfo.Format(minLinkLength));
    }
    if (maxLinkLength > 0) {
      sb.AppendLine(MaximumLinkLengthInfo.Format(maxLinkLength));
    }
    if (sourceLinkAngleLimit > 0) {
      sb.AppendLine(SourceJointFreedomInfo.Format(sourceLinkAngleLimit));
    }
    if (targetLinkAngleLimit > 0) {
      sb.AppendLine(TargetJointFreedomInfo.Format(targetLinkAngleLimit));
    }
    return sb.ToString();
  }

  /// <inheritdoc/>
  public virtual string GetModuleTitle() {
    return ModuleTitle;
  }

  /// <inheritdoc/>
  public virtual Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc/>
  public virtual string GetPrimaryField() {
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
          ScreenMessaging.ShowErrorScreenMessage(limitError);
          var oldParent = part.parent;
          AsyncCall.CallOnEndOfFrame(this, () => {
            // Ensure part's state hasn't been changed by the other modules.
            if (part.parent == oldParent) {
              HostedDebugLog.Warning(
                  this, "Detach from the parent since joint limits are not met: {0}", limitError);
              source.BreakCurrentLink(LinkActorType.Physics);
            } else {
              HostedDebugLog.Warning(this, "Skip detaching since the part is already detached");
            }
          });
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
    if (isLinked) {
      AdjustJoint(isUnbreakable: true);
    }
  }
  #endregion

  #region ILinkEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    CreateJoint(info.source, info.target);
  }

  /// <inheritdoc/>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    DropJoint();
  }
  #endregion

  #region Utility methods
  /// <summary>Destroys stock joint on the part if one exists.</summary>
  /// <remarks>
  /// Note, that this will trigger <see cref="GameEvents.onPartJointBreak"/> event.
  /// </remarks>
  protected void DropStockJoint() {
    if (stockJoint != null) {
      stockJoint.DestroyJoint();
    }
    stockJoint = null;
  }

  /// <summary>
  /// Setups joint break force and torque while handling special values from config.
  /// </summary>
  /// <remarks>
  /// The forces are set so what they are not contradicting with the attached parts. Normally, joint
  /// must get destroyed by the physics before the attached part did.
  /// </remarks>
  /// <param name="joint">Joint to set forces for.</param>
  /// <param name="forceFromConfig">
  /// Break force from the config. If it's <c>0</c> then maxium acceptable force will be used.
  /// </param>
  /// <param name="torqueFromConfig">
  /// Break torque from the config. If it's <c>0</c> then maxium acceptable torque will be used.
  /// </param>
  /// <seealso cref="GetClampedBreakingForce"/>
  protected void SetBreakForces(
      ConfigurableJoint joint, float forceFromConfig, float torqueFromConfig) {
    joint.breakForce = GetClampedBreakingForce(forceFromConfig);
    joint.breakTorque = GetClampedBreakingTorque(torqueFromConfig);
  }

  /// <summary>
  /// Rounds down the value so what it doesn't contradict with source and target breaking forces.
  /// </summary>
  /// <remarks>
  /// It's a bad idea to make joint more durable than the parts that are connected with it. It's
  /// always best to have joint broken before the parts destruction. Custom code is encouraged to
  /// use this method to get the right force.
  /// </remarks>
  /// <param name="value">
  /// Breaking force value to round. If it's <c>0</c> then maximum possible value will be returned.
  /// </param>
  /// <param name="isStack">
  /// Type of the connection. Stack connections are much stronger than surface ones.
  /// </param>
  /// <returns>Force value that relates to the source and target parts durability.</returns>
  /// <seealso cref="attachNodeSize"/>
  protected float GetClampedBreakingForce(float value, bool isStack = true) {
    return Mathf.Approximately(value, 0)
        ? ScaleForceToNode(
            Mathf.Min(linkSource.part.breakingForce, linkTarget.part.breakingForce),
            isStack: isStack)
        : ScaleForceToNode(
            Mathf.Min(value, linkSource.part.breakingForce, linkTarget.part.breakingForce),
            isStack: isStack);
  }
  
  /// <summary>
  /// Rounds down the value so what it doesn't contradict with source and target breaking torques.
  /// </summary>
  /// <remarks>
  /// It's a bad idea to make joint more durable than the parts that are connected with it. It's
  /// always best to have joint broken before the parts destruction. Custom code is encouraged to
  /// use this method to get the right torque.
  /// </remarks>
  /// <param name="value">
  /// Breaking force value to round. If it's <c>0</c> then maximum possible value will be returned.
  /// </param>
  /// <param name="isStack">
  /// Type of the connection. Stack connections are much stronger than surface ones.
  /// </param>
  /// <returns>Force value that relates to the source and target parts durability.</returns>
  /// <seealso cref="attachNodeSize"/>
  protected float GetClampedBreakingTorque(float value, bool isStack = true) {
    return Mathf.Approximately(value, 0)
        ? ScaleForceToNode(
            Mathf.Min(linkSource.part.breakingTorque, linkTarget.part.breakingTorque),
            isStack: isStack)
        : ScaleForceToNode(
            Mathf.Min(value, linkSource.part.breakingTorque, linkTarget.part.breakingTorque),
            isStack: isStack);
  }

  /// <summary>Scales the force value to the node size.</summary>
  /// <remarks>Uses same approach as in <see cref="PartJoint"/>.</remarks>
  /// <param name="force">Base force to scale.</param>
  /// <param name="isStack">
  /// Type of the connection. Stack connections are much stronger than surface ones.
  /// </param>
  /// <returns>Force scaled to the node size.</returns>
  /// <seealso cref="attachNodeSize"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_part_joint.html">
  /// KSP: PartJoint</seealso>
  protected float ScaleForceToNode(float force, bool isStack = true) {
    return force * (1.0f + attachNodeSize) * (isStack ? 2.0f : 0.8f);
  }
  #endregion
}

}  // namespace
