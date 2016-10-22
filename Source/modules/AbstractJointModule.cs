// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KASAPIv1;
using KSPDev.GUIUtils;
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
public abstract class AbstractJointModule :
    // KSP parents.
    PartModule, IModuleInfo,
    // KAS parents.
    ILinkJoint, ILinkStateEventListener,
    // Syntax sugar parents.
    IPartModule, IJointEventsListener, IsPackable, IsDestroyable, IKSPDevModuleInfo {

  #region Helper class to save joint state
  /// <summary>
  /// Container class to store joint state when changing from breakable&lt;=&gt;unbreakable state.
  /// </summary>
  /// <remarks>
  /// Not whole joint state is saved. Only the following settings will be remembered:
  /// <list type="bullet">
  /// <item>Angular motion modes.</item>
  /// <item>Linear motion modes.</item>
  /// <item>Break force and torque.</item>
  /// <item>Joint optimization settings.</item>
  /// </list>
  /// <para>Field names match the related names in <see cref="ConfigurableJoint"/>.</para>
  /// </remarks>
  /// <seealso href="https://docs.unity3d.com/356/Documentation/ScriptReference/ConfigurableJoint.html">
  /// Unity 3D: ConfigurableJoint</seealso>
  /// <seealso cref="SetupUnbreakableJoint"/>
  protected class JointState {
    /// <exlcude/>
    public ConfigurableJointMotion angularXMotion;
    /// <exlcude/>
    public ConfigurableJointMotion angularYMotion;
    /// <exlcude/>
    public ConfigurableJointMotion angularZMotion;
    /// <exlcude/>
    public ConfigurableJointMotion xMotion;
    /// <exlcude/>
    public ConfigurableJointMotion yMotion;
    /// <exlcude/>
    public ConfigurableJointMotion zMotion;
    /// <exlcude/>
    public float breakForce;
    /// <exlcude/>
    public float breakTorque;
    /// <exlcude/>
    public bool enablePreprocessing;

    /// <summary>Creates default object. State should be saved via <see cref="SaveState"/></summary>
    public JointState() {
    }

    /// <summary>Creates state object and stores joint's state into it.</summary>
    /// <param name="joint">Joint to save state for.</param>
    public JointState(ConfigurableJoint joint) {
      SaveState(joint);
    }

    /// <summary>Stores joint's state in the instance.</summary>
    /// <remarks>Not whole state is saved. See <see cref="JointState"/>.</remarks>
    /// <param name="sourceJoint">Joint to copy state from.</param>
    public void SaveState(ConfigurableJoint sourceJoint) {
      angularXMotion = sourceJoint.angularXMotion;
      angularYMotion = sourceJoint.angularYMotion;
      angularZMotion = sourceJoint.angularZMotion;
      xMotion = sourceJoint.xMotion;
      yMotion = sourceJoint.yMotion;
      zMotion = sourceJoint.zMotion;
      breakForce = sourceJoint.breakForce;
      breakTorque = sourceJoint.breakTorque;
      enablePreprocessing = sourceJoint.enablePreprocessing;
    }

    /// <summary>retsores joint's state from the instance.</summary>
    /// <remarks>Not whole state is restored. See <see cref="JointState"/>.</remarks>
    /// <param name="targetJoint">Joint to restore state for.</param>
    public void RestoreState(ConfigurableJoint targetJoint) {
      targetJoint.angularXMotion = angularXMotion;
      targetJoint.angularYMotion = angularYMotion;
      targetJoint.angularZMotion = angularZMotion;
      targetJoint.xMotion = xMotion;
      targetJoint.yMotion = yMotion;
      targetJoint.zMotion = zMotion;
      targetJoint.breakForce = breakForce;
      targetJoint.breakTorque = breakTorque;
      targetJoint.enablePreprocessing = enablePreprocessing;
    }
  }
  #endregion
  
  #region Localizable GUI strings
  /// <summary>Message to display when link cannot be established because it's too short.</summary>
  protected static Message<float, float> MinLengthLimitReachedMsg =
      "Link is too short: {0:F2}m < {1:F2}m";
  /// <summary>Message to display when link cannot be established because it's too long.</summary>
  protected static Message<float, float> MaxLengthLimitReachedMsg =
      "Link is too long: {0:F2}m > {1:F2}m";
  /// <summary>
  /// Message to display when link cannot be established because maximum angle between link vector
  /// and the joint normal at the source part is to big.
  /// </summary>
  protected static Message<float, int> SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0}deg > {1}deg";
  /// <summary>
  /// Message to display when link cannot be established because maximum angle between link vector
  /// and the joint normal at the target part is to big.
  /// </summary>
  protected static Message<float, int> TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0}deg > {1}deg";
  /// <summary>Info string in the editor for link break force setting.</summary>
  protected static Message<float> LinkLinearStrengthInfo = "Link break force: {0:F1}N";
  /// <summary>Info string in the editor for link break torque setting.</summary>
  protected static Message<float> LinkBreakStrengthInfo = "Link torque force: {0:F1}N";
  /// <summary>Info string in the editor for minimum link length setting.</summary>
  protected static Message<float> MinimumLinkLengthInfo = "Minimum link length: {0:F1}m";
  /// <summary>Info string in the editor for maximum link length setting.</summary>
  protected static Message<float> MaximumLinkLengthInfo = "Maximum link length: {0:F1}m";
  /// <summary>Info string in the editor for maximum allowed angle at the source.</summary>
  protected static Message<float> SourceJointFreedomInfo = "Source angle limit: {0}deg";
  /// <summary>Info string in the editor for maximum allowed angle at the target.</summary>
  protected static Message<float> TargetJointFreedomInfo = "Target angle limit: {0}deg";
  /// <summary>Title of the module to present in the editor details window.</summary>
  protected static Message ModuleTitle = "KAS Joint";
  #endregion

  #region ILinkJoint CFG properties
  /// <inheritdoc/>
  public float cfgLinkBreakForce { get { return linkBreakForce; } }
  /// <inheritdoc/>
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
  /// <summary>Config setting. See <see cref="cfgLinkBreakForce"/>.</summary>
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
  public float linkBreakForce = 0;
  /// <summary>Config setting. See <see cref="cfgLinkBreakTorque"/>.</summary>
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
  public float linkBreakTorque = 0;
  /// <summary>Config setting. See <see cref="cfgSourceLinkAngleLimit"/>.</summary>
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
  public int sourceLinkAngleLimit = 0;
  /// <summary>Config setting. See <see cref="cfgTargetLinkAngleLimit"/>.</summary>
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
  public int targetLinkAngleLimit = 0;
  /// <summary>Config setting. See <see cref="cfgMinLinkLength"/>.</summary>
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
  public float minLinkLength = 0;
  /// <summary>Config setting. See <see cref="cfgMaxLinkLength"/>.</summary>
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
  public float maxLinkLength = 0;
  #endregion

  #region Inheritable properties
  /// <summary>Source of the link. It's populated in <see cref="CreateJoint"/>.</summary>
  /// <remarks>
  /// When loading vessel the joint is restored in the "physics" method <see cref="OnPartUnpack"/>.
  /// Before it happen the source will be <c>null</c>.
  /// </remarks>
  protected ILinkSource linkSource { get; private set; }
  /// <summary>Target of the link. It's populated in <see cref="CreateJoint"/>.</summary>
  /// <remarks>
  /// When loading vessel the joint is restored in the "physics" method <see cref="OnPartUnpack"/>.
  /// Before it happen the target will be <c>null</c>.
  /// </remarks>
  protected ILinkTarget linkTarget { get; private set; }
  /// <summary>Length at the moment of creating joint.</summary>
  /// <remarks>Elastic joints may allow length deviation. Use thi svalue as the base.</remarks>
  protected float originalLength { get; private set; }
  /// <summary>Tells if there is joint created.</summary>
  protected bool isLinked { get; private set; }
  /// <summary>Joint that was created by KSP core to connect two parts.</summary>
  /// <remarks>
  /// Once physics starts on part the KSP core creates a joint, and assigns it to
  /// <see cref="Part.attachJoint"/>. This module resets the joint to <c>null</c> to prevent KSP
  /// logic on it but <i>does not</i> change joint component on the part. Descendants must take care
  /// of the stock joint either by delegating relevant events to it or by destroying altogether.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_part.html#aa5a1e018fa5b47c5723aa0879e23c647">
  /// KSP: Part.attachJoint</seealso>
  protected PartJoint stockJoint { get; private set; }
  /// <summary>State of the joint as it was set by the game's core.</summary>
  /// <remarks>It only has valid value if <see cref="isLinked"/> is <c>true</c>.</remarks>
  /// <seealso cref="SetupUnbreakableJoint"/>
  protected JointState defaultJointState { get; private set; }
  #endregion

  bool isRestored;

  #region IJointEventsListener implemetation
  /// <inheritdoc/>
  public virtual void OnJointBreak(float breakForce) {
    Debug.LogFormat("Joint on {0} broken by physics with force={1}", part.name, breakForce);
    DropJoint();
    part.OnPartJointBreak(breakForce);
  }
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public virtual void CreateJoint(ILinkSource source, ILinkTarget target) {
    DropJoint();
    linkSource = source;
    linkTarget = target;
    if (part.attachJoint != null && part.attachJoint.Target == target.part) {
      stockJoint = part.attachJoint;
      part.attachJoint = null;
      defaultJointState = new JointState(stockJoint.Joint);
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
    sb.AppendLine(SourceJointFreedomInfo.Format(sourceLinkAngleLimit));
    sb.AppendLine(TargetJointFreedomInfo.Format(targetLinkAngleLimit));
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
          AsyncCall.CallOnEndOfFrame(this, x => {
            // Ensure part's state hasn't been changed by the other modules.
            if (part.parent == oldParent) {
              Debug.LogWarningFormat(
                  "Detach part {0} from the parent since joint limits are not met: {1}",
                  part.name, limitError);
              source.BreakCurrentLink(LinkActorType.Physics);
            } else {
              Debug.LogWarningFormat("Skip detaching {0} since it's already detached", part.name);
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
  /// This method must not be called prior to the joint initalization. It's safe to call it from
  /// <see cref="ILinkJoint"/> implementations, but all other cases must ensure the default joint
  /// state has been captured.
  /// </remarks>
  /// <param name="joint">Joint to set forces for.</param>
  /// <param name="forceFromConfig">
  /// Break force from the config. If it's <c>0</c> then force will be the same as for the stock
  /// joints.
  /// </param>
  /// <param name="torqueFromConfig">
  /// Break torque from the config. If it's <c>0</c> then torque will be the same as for the stock
  /// joints.
  /// </param>
  /// <seealso cref="defaultJointState"/>
  protected void SetBreakForces(
      ConfigurableJoint joint, float forceFromConfig, float torqueFromConfig) {
    joint.breakForce =
        Mathf.Approximately(forceFromConfig, 0) ? defaultJointState.breakForce : forceFromConfig;
    joint.breakTorque =
        Mathf.Approximately(torqueFromConfig, 0) ? defaultJointState.breakTorque : torqueFromConfig;
  }

  /// <summary>Makes a regular joint unbreakable and locked.</summary>
  /// <remarks>
  /// Normal use-case for setting joint to unbreakable is switching to time warp mode. When time
  /// warp mode is enabled it's undesirable to have joint moving or broken. Moreover, in this mode
  /// performance reqirements become more improtant. 
  /// <list type="bullet">
  /// <item>All motions are set to <c>Locked</c>.</item>
  /// <item>Breaking forces are set to infinite.</item>
  /// <item>Joint preprocessing is set to <c>true</c> to improve performance.</item>
  /// </list>
  /// <para>
  /// Unless your code sets up the joint from the scratch it makes sense to save joint state via
  /// <see cref="JointState.SaveState"/> before making it unbreakable. Or, if your code uses stock
  /// settings, <see cref="defaultJointState"/> can be used to restore the default state.
  /// </para>
  /// </remarks>
  /// <param name="joint">Joint to configure.</param>
  /// <seealso cref="AdjustJoint"/>
  /// <seealso cref="JointState"/>
  protected void SetupUnbreakableJoint(ConfigurableJoint joint) {
    //FIXME
    Debug.LogWarning("Set unbreakable");
    // Make joint absolutly rigid. 
    joint.angularXMotion = ConfigurableJointMotion.Locked;
    joint.angularYMotion = ConfigurableJointMotion.Locked;
    joint.angularZMotion = ConfigurableJointMotion.Locked;
    joint.xMotion = ConfigurableJointMotion.Locked;
    joint.yMotion = ConfigurableJointMotion.Locked;
    joint.zMotion = ConfigurableJointMotion.Locked;
    // Since no movement is allowed the preprocessing will give a performance boost.
    joint.enablePreprocessing = true; 
    SetBreakForces(joint, Mathf.Infinity, Mathf.Infinity);
  }

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
  #endregion
}

}  // namespace
