// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.PartUtils;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

/// <summary>Flexible link joint designed specifically for towing vessels.</summary>
/// <remarks>
/// Key different from a regular flexible joint is increased towing stability.
/// <list type="bullet">
/// <item>Link is locked at the vessel being towed to make towing more predictable.</item>
/// <item>
/// Active steering mode allows using towed vessel control mode to compensate relative shifts.
/// </item>
/// </list>
/// </remarks>
// Next localization ID: #kasLOC_05020.
public sealed class KASJointTowBar : KASJointTwoEndsSphere,
    // KSPDev sugar interfaces.
    IsPhysicalObject,
    // KSPDev interfaces.
    IHasContextMenu {

  #region Localizable strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<AngleType> LockingStatusMsg = new Message<AngleType>(
      "#kasLOC_05000",
      defaultTemplate: "Tow bar is locking: diff <<1>>",
      description: "Message to display when a tow bar is not locked but the locking process has"
      + " been started."
      + " The <<1>> argument shows the current locking error and is formatted as an angle type.",
      example: "Tow bar is locking: diff 1.5°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LockedStatusMsg = new Message(
      "#kasLOC_05001",
      defaultTemplate: "Tow bar is LOCKED!",
      description: "Message to display when a tow bar locking process successfully ends with"
      + " locking.");

  #region SteeringStatus enum values
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringStatusMsg_Disabled = new Message(
      "#kasLOC_05002",
      defaultTemplate: "Disabled",
      description: "A string in the context menu that tells that the active steering mode is not"
      + " enabled.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringStatusMsg_Active = new Message(
      "#kasLOC_05003",
      defaultTemplate: "Active",
      description: "A string in the context menu that tells that the active steering mode is ready"
      + " and working.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringStatusMsg_CurrentVesselIsTarget = new Message(
      "#kasLOC_05004",
      defaultTemplate: "Target is active vessel",
      description: "A string in the context menu that tells that the active steering mode cannot"
      + " work due to the bar's target vessel is currently under player's control.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringStatusMsg_TargetIsNotControllable = new Message(
      "#kasLOC_05005",
      defaultTemplate: "Target is uncontrollable",
      description: "A string in the context menu that tells that the active steering mode cannot"
      + " work due to the linked vessel is remotely controlled.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringStatusMsg_NotLocked = new Message(
      "#kasLOC_05006",
      defaultTemplate: "Not locked",
      description: "A string in the context menu that tells that the active steering mode is"
      + " activated but cannot start working due to the constraints.");
  #endregion

  /// <summary>Translates <see cref="SteeringStatus"/> enum into a localized message.</summary>
  static readonly MessageLookup<SteeringStatus> SteeringStatusMsgLookup =
      new MessageLookup<SteeringStatus>(new Dictionary<SteeringStatus, Message>() {
          {SteeringStatus.Disabled, SteeringStatusMsg_Disabled},
          {SteeringStatus.Active, SteeringStatusMsg_Active},
          {SteeringStatus.CurrentVesselIsTarget, SteeringStatusMsg_CurrentVesselIsTarget},
          {SteeringStatus.TargetIsNotControllable, SteeringStatusMsg_TargetIsNotControllable},
          {SteeringStatus.NotLocked, SteeringStatusMsg_NotLocked},
      });

  #region LockMode enum values
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LockStatusMsg_Disabled = new Message(
      "#kasLOC_05007",
      defaultTemplate: "Disabled",
      description: "A string in the context menu that tells that the bar joints are unlocked.");
  
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LockStatusMsg_Locked = new Message(
      "#kasLOC_05008",
      defaultTemplate: "Locked",
      description: "A string in the context menu that tells that the bar joints are locked.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LockStatusMsg_Locking = new Message(
      "#kasLOC_05009",
      defaultTemplate: "Locking",
      description: "A string in the context menu that tells that the bar joints are unlocked but"
      + " the part is trying to lock them.");
  #endregion
  
  /// <summary>Translates <see cref="LockMode"/> enum into a localized message.</summary>
  static readonly MessageLookup<LockMode> LockStatusMsgLookup =
      new MessageLookup<LockMode>(new Dictionary<LockMode, Message>() {
          {LockMode.Disabled, LockStatusMsg_Disabled},
          {LockMode.Locked, LockStatusMsg_Locked},
          {LockMode.Locking, LockStatusMsg_Locking},
      });
  
  /// <summary>Status screen message to be displayed during the locking process.</summary>
  ScreenMessage lockStatusScreenMessage;

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringNormalToggle = new Message(
      "#kasLOC_05018",
      defaultTemplate: "Normal",
      description: "A string in the context menu that tells if the steering commands are sent to"
      + " the linked vessel in the exact form as they've emitted for the source vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message SteeringInvertToggle = new Message(
      "#kasLOC_05019",
      defaultTemplate: "Inverted",
      description: "A string in the context menu that tells if the steering commands are sent to"
      + " the linked vessel in the inverted form relative to the source vessel.");
  #endregion

  #region Part's config fields
  /// <summary>The link angle at the source part that produces the maximum steering.</summary>
  /// <remarks>
  /// This is a denominator that tells at which steering angle the steering power on the towed
  /// vessel is at <c>1.0</c>. E.g. if this settings is <c>25</c> degrees and the angle at the
  /// source is <c>10</c> degrees, then the steering power will be <c>10/25=0.4</c>. In the
  /// contrast, if the angle at the source is <c>40</c>, then the towed vessel will be steering at
  /// power <c>1.6</c>, bringing it back to line more quick but with less course stability. In
  /// general, this setting defines the maximum comfort speed of towing: the lower values are good
  /// for the higher speed towing.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float maxSteeringAngle = 1.0f;  // We don't want it be zero.
  #endregion

  #region Persistent fields
  /// <summary>Tells if the active steering mode is enabled.</summary>
  /// <remarks>
  /// If the mode is enabled it doesn't mean it's active. There are the conditions that affect when
  /// the mode can actually start affecting the target vessel.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public bool persistedActiveSteeringEnabled;

  /// <summary>Current locking mode of the tow bar.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public LockMode persistedLockingMode = LockMode.Disabled;
  #endregion

  #region The context menu fields
  /// <summary>Status field to display current lock state.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField]
  [LocalizableItem(
      tag = "#kasLOC_05010",
      defaultTemplate = "Lock status",
      description = "A context menu item that displays the current status of the bar locking.")]
  public string lockStatus = "";

  /// <summary>Status field to display current steering status.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField]
  [LocalizableItem(
      tag = "#kasLOC_05011",
      defaultTemplate = "Steering status",
      description = "A context menu item that displays the current steering status.")]
  public string steeringStatus = "";

  /// <summary>Defines responsiveness of the towed vessel to the steering.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  [KSPField(guiFormat = "0.0", isPersistant = true),
   UI_FloatRange(controlEnabled = true, scene = UI_Scene.All,
                 stepIncrement = 0.01f, maxValue = 2f, minValue = 0.1f)]
  [LocalizableItem(
      tag = "#kasLOC_05012",
      defaultTemplate = "Steering sensitivity",
      description = "A context menu item that displays and allows changing the strength of the"
      + " steering commands, that the tow bar sends to the linked vessel.")]
  public float steeringSensitivity = 1.0f;

  /// <summary>Inverts steering angle calculated in active steering mode.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/UIConfigSetting/*"/>
  /// FIXME: localization for the invert modes
  [KSPField(guiName = "Steering: Direction", isPersistant = true),
   UI_Toggle(disabledText = "Normal", enabledText = "Inverted", scene = UI_Scene.All)]
  [LocalizableItem(
      tag = "#kasLOC_05013",
      defaultTemplate = "Steering: Direction",
      description = "A context menu item that displays and allows changing the direction of the"
      + " steering commands.")]
  public bool steeringInvert;
  #endregion

  #region Internal properties
  /// <summary>Current locking mode.</summary>
  /// <remarks>It's declared public only to make the KSP peristense working.</remarks>
  public enum LockMode {
    /// <summary>Not requested.</summary>
    Disabled,
    /// <summary>Requested but angular difference is too much to activate.</summary>
    Locking,
    /// <summary>Target joint is locked on Z axis (a normal vector to the surface).</summary>
    Locked,
  }

  /// <summary>Status helper. Only used to present GUI status.</summary>
  enum SteeringStatus {
    /// <summary>Not requested.</summary>
    Disabled,
    /// <summary>Requested and currently active.</summary>
    Active,
    /// <summary>Requested but inactive due to active vessel is the same as link's target.</summary>
    CurrentVesselIsTarget,
    /// <summary>Requested but inactive due to target vessel has no active control module.</summary>
    TargetIsNotControllable,
    /// <summary>Requested but inactive due to link has not yet locked.</summary>
    NotLocked,
  }

  /// <summary>
  /// The maximum angle between the port normal and the link vector to consdier the locking process
  /// is done.
  /// </summary>
  /// <remarks>Once the angle decreases down to this value, the towbar locks down.</remarks>

  /// <summary>
  /// Minumal angle between port normal and the link vector to continue apply steering commands.
  /// </summary>
  /// <remarks>The angles below this value don't affect the towed vessel.</remarks>
  const float ZeroSteeringAngle = 0.05f;
  #endregion

  #region KASJointTwoEndsSphere overrides
  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    lockStatusScreenMessage = new ScreenMessage(
        "", ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_LEFT);
    if (HighLogic.LoadedSceneIsFlight) {
      // Trigger updates with the loaded value.
      SetActiveSteeringState(persistedActiveSteeringEnabled);
    }
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void LocalizeModule() {
    base.LocalizeModule();

    var toggle = Fields["steeringInvert"].uiControlFlight as UI_Toggle;
    if (toggle != null) {
      toggle.disabledText = SteeringNormalToggle;
      toggle.enabledText = SteeringInvertToggle;
    }
  }

  /// <inheritdoc/>
  public override bool CreateJoint(ILinkSource source, ILinkTarget target) {
    if (!base.CreateJoint(source, target)) {
      return false;
    }
    SetLockingMode(persistedLockingMode);
    SetActiveSteeringState(persistedActiveSteeringEnabled);
    return true;
  }

  /// <inheritdoc/>
  public override void DropJoint() {
    SetLockingMode(LockMode.Disabled);
    steeringInvert = false;
    steeringSensitivity = 1.0f;
    base.DropJoint();
    UpdateContextMenu();
  }
  #endregion

  #region GUI menu action handlers
  /// <summary>Starts mode to lock target joint.</summary>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_05014",
      defaultTemplate = "Start locking",
      description = "A context menu event that starts the locking process on a linked vessel.")]
  public void StartLockLockingAction() {
    SetLockingMode(LockMode.Locking);
  }

  /// <summary>Unlocks target joint and disables active steering.</summary>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_05015",
      defaultTemplate = "Unlock joint",
      description = "A context menu event that disables the locking of the tow bar joints and turns"
      + " off the active steering mode.")]
  public void UnlockAction() {
    SetLockingMode(LockMode.Disabled);
  }

  /// <summary>Enables active steering of the towed vessel.</summary>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_05016",
      defaultTemplate = "Enable active steering",
      description = "A context menu event that enables the active steering mode.")]
  public void ActiveSteeringAction() {
    SetActiveSteeringState(true);
  }

  /// <summary>Disables active steering of the towed vessel.</summary>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_05017",
      defaultTemplate = "Disable active steering",
      description = "A context menu event that disables the active steering mode.")]
  public void DeactiveSteeringAction() {
    SetActiveSteeringState(false);
  }
  #endregion

  #region IsPhysicalObject implementation
  /// <inheritdoc/>
  public void FixedUpdate() {
    if (!isLinked) {
      return;
    }
    if (persistedLockingMode == LockMode.Locking) {
      var yaw = GetYawAngle(linkTarget.nodeTransform, linkSource.nodeTransform);
      var absYaw = Mathf.Abs(yaw);
      if (absYaw < trgJoint.angularZLimit.limit) {
        trgJoint.angularZLimit = new SoftJointLimit() {
          limit = absYaw,
        };
        // Either Unity or KSP applies a cap to the minimum allowed limit. It makes it impossible to
        // iteratively reduce the limit down to zero. So check when we hit the cap and just activate
        // locked mode assuming the limit cap was chosen wise.
        // TODO(ihsoft): Track last known yaw and lock when it starts increasing.
        if (!Mathf.Approximately(absYaw, trgJoint.angularZLimit.limit) || absYaw < LockJointAngle) {
          yaw = 0f;
          SetLockingMode(LockMode.Locked);
        }
      }
      ShowLockingProgress(angleDiff: yaw);
    }
    if (persistedActiveSteeringEnabled) {
      UpdateActiveSteering();
    }
  }
  #endregion

  #region IHasContextMenu implemenation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    Fields["lockStatus"].guiActive = isLinked;
    Fields["steeringStatus"].guiActive = isLinked;
    Fields["steeringInvert"].guiActive = isLinked && persistedActiveSteeringEnabled;
    Fields["steeringSensitivity"].guiActive = isLinked && persistedActiveSteeringEnabled;
    
    PartModuleUtils.SetupEvent(
        this, StartLockLockingAction,
        e => e.active = isLinked && persistedLockingMode == LockMode.Disabled);
    PartModuleUtils.SetupEvent(
        this, UnlockAction,
        e => e.active = isLinked && persistedLockingMode != LockMode.Disabled);
    PartModuleUtils.SetupEvent(
        this, DeactiveSteeringAction,
        e => e.active = isLinked && persistedActiveSteeringEnabled);
    PartModuleUtils.SetupEvent(
        this, ActiveSteeringAction,
        e => e.active = isLinked && !persistedActiveSteeringEnabled);

    lockStatus = LockStatusMsgLookup.Lookup(persistedLockingMode);
    steeringStatus = SteeringStatusMsgLookup.Lookup(
        persistedActiveSteeringEnabled ? SteeringStatus.Active : SteeringStatus.Disabled);
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// Sets current locking state. Updates UI, vessel, and joints states as needed.
  /// </summary>
  /// <remarks>
  /// This method may be called from a cleanup routines, so make it safe to execute in incomplete
  /// states.
  /// </remarks>
  /// <param name="mode"></param>
  void SetLockingMode(LockMode mode) {
    persistedLockingMode = mode;

    if (isLinked && trgJoint != null && (mode == LockMode.Locked || mode == LockMode.Disabled)) {
      // Restore joint state that could be affected during locking.
      var angularLimit = trgJoint.angularZLimit;
      angularLimit.limit = targetLinkAngleLimit;
      trgJoint.angularZLimit = angularLimit;
      trgJoint.angularZMotion = mode == LockMode.Locked
          ? ConfigurableJointMotion.Locked
          : ConfigurableJointMotion.Limited;
    }
    if (mode == LockMode.Disabled) {
      ShowLockingProgress(hideMessages: true);
      SetActiveSteeringState(false);  // No active steering in unlocked mode.
    }
    UpdateContextMenu();
  }

  /// <summary>
  /// Enables or disables active steering mode. Updates UI and vessel state as needed.
  /// </summary>
  /// <remarks>
  /// This method may be called from a cleanup routines, so make it safe to execute in incomplete
  /// states.
  /// </remarks>
  /// <param name="state"></param>
  void SetActiveSteeringState(bool state) {
    persistedActiveSteeringEnabled = state;
    if (isLinked && linkTarget != null && !persistedActiveSteeringEnabled) {
      linkTarget.part.vessel.ctrlState.wheelSteer = 0;
    }
    UpdateContextMenu();
  }

  /// <summary>
  /// Gets yaw angle between the tranformations. Angle is calculated on the plane defined by the
  /// <paramref name="refTransform"/> up direction.
  /// </summary>
  /// <param name="refTransform">Transformation to use as base of the calculation.</param>
  /// <param name="targetTransform">Transformation of the target.</param>
  /// <returns></returns>
  float GetYawAngle(Transform refTransform, Transform targetTransform) {
    var linkVector = targetTransform.position - refTransform.position;
    var partLinkVector = refTransform.InverseTransformDirection(linkVector);
    var eulerAngle = Quaternion.LookRotation(partLinkVector).eulerAngles.y;
    eulerAngle = eulerAngle > 180 ? eulerAngle - 360 : eulerAngle;
    return eulerAngle;
  }

  /// <summary>
  /// Updates towed vessel input with steering commands to align it with the towing vessel. Steering
  /// angles are obtained from the link angle at the source part.
  /// </summary>
  void UpdateActiveSteering() {
    if (linkTarget.part.vessel == FlightGlobals.ActiveVessel) {
      steeringStatus = SteeringStatusMsgLookup.Lookup(SteeringStatus.CurrentVesselIsTarget);
    } else if (!linkTarget.part.vessel.IsControllable) {
      steeringStatus = SteeringStatusMsgLookup.Lookup(SteeringStatus.TargetIsNotControllable);
    } else if (persistedLockingMode != LockMode.Locked) {
      steeringStatus = SteeringStatusMsgLookup.Lookup(SteeringStatus.NotLocked);
    } else if (persistedActiveSteeringEnabled) {
      steeringStatus = SteeringStatusMsgLookup.Lookup(SteeringStatus.Active);
      var srcJointYaw = GetYawAngle(linkSource.nodeTransform, linkTarget.nodeTransform);
      if (steeringInvert) {
        srcJointYaw = -srcJointYaw;
      }
      linkTarget.part.vessel.ctrlState.wheelSteer = Mathf.Abs(srcJointYaw) > ZeroSteeringAngle
          ? Mathf.Clamp(steeringSensitivity * srcJointYaw / maxSteeringAngle, -1.0f, 1.0f)
          : 0;
    }
  }

  /// <summary>\Displays current locking state in the upper left corner of the screen.</summary>
  /// <param name="angleDiff">
  /// Value of the difference between port normal and link vector projected on the surface plane.
  /// If value is close to zero then locking state is assumed to be LOCKED.
  /// </param>
  /// <param name="hideMessages">Specifies if any progress display should be hidden.</param>
  void ShowLockingProgress(float angleDiff = 0f, bool hideMessages = false) {
    if (lockStatusScreenMessage != null) {
      if (hideMessages) {
        ScreenMessages.RemoveMessage(lockStatusScreenMessage);
      } else if (Mathf.Approximately(angleDiff, 0)) {
        ScreenMessages.RemoveMessage(lockStatusScreenMessage);
        ScreenMessages.PostScreenMessage(LockedStatusMsg, ScreenMessaging.DefaultMessageTimeout,
                                         ScreenMessageStyle.UPPER_LEFT);
      } else {
        lockStatusScreenMessage.message = LockingStatusMsg.Format(angleDiff);
        ScreenMessages.PostScreenMessage(lockStatusScreenMessage);
      }
    }
  }
  #endregion
}

}  // namespace
