// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using UnityEngine;
using KSPDev.GUIUtils;
using KSPDev.ProcessingUtils;
using KASAPIv1;

namespace KAS {

/// <summary>Module that allows connecting parts by mouse via GUI.</summary>
/// <remarks>
/// When player starts linking mode he must either complete it by clicking on a compatible target
/// part or abort the mode altogether.
/// <para>
/// EVA kerbal movement is locked when linking mode is active, so both source and target parts
/// must be in the range from the kerbal.
/// </para>
/// </remarks>
public sealed class KASModuleInteractiveJointSource : KASModuleLinkSourceBase {

  #region Localizable strings
  /// <summary>Message to display when a compatible target part is hevred over.</summary>
  static Message<float> CanBeConnectedMsg = "Click to establish a link (length {0:F2} m)";
  /// <summary>
  /// Message to dsiplay as a help string when interactive linking mode is started.
  /// </summary>
  static Message LinkingInProgressMsg = "Select a compatible socket or press ESC";
  #endregion

  #region Local members
  /// <summary>Color of pipe in the linking mode when link can be established.</summary>
  readonly static Color GoodLinkColor = new Color(0, 1, 0, 0.5f);
  /// <summary>Color of pipe in the linking mode when link cannot be established.</summary>
  readonly static Color BadLinkColor = new Color(1, 0, 0, 0.5f);
  /// <summary>A lock that restricts anything but camera positioning.</summary>
  const string TotalControlLock = "KASInteractiveJointUberLock";
  /// <summary>Shader that reders pipe during linking.</summary>
  const string InteractiveShaderName = "Transparent/Diffuse";  
  /// <summary>Compativle target under mouse cursor.</summary>
  ILinkTarget targetCandidate;
  /// <summary>Tells if connection with teh candidate will be sucessfull.</summary>
  bool targetCandidateIsGood;
  /// <summary>Last known hovered part. Used to trigger detection of the target candidate.</summary>
  Part lastHoveredPart;
  /// <summary>Displayed during interactive linking.</summary>
  ScreenMessage statusScreenMessage;
  #endregion

  #region Event names. Keep them in sync with the event names!
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  /// <seealso cref="StartLinkContextMenuAction"/>
  const string StartLinkMenuActionName = "StartLinkContextMenuAction";  
  /// <summary>Name of the relevant event. It must match name of the method.</summary>
  /// <seealso cref="BreakLinkContextMenuAction"/>
  const string BreakLinkMenuActionName = "BreakLinkContextMenuAction";
  #endregion

  #region Part's config fields
  /// <summary>Config setting. Audio sample to play when parts are docked by the player.</summary>
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
  public string plugSndPath = "KAS/Sounds/plugdocked";
  /// <summary>Config setting. Audio sample to play when parts are undocked by the player.</summary>
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
  public string unplugSndPath = "KAS/Sounds/unplugdocked";
  /// <summary>Config setting. Audio sample to play when parts are undocked by physics.</summary>
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
  public string brokeSndPath = "KAS/Sounds/broke";
  /// <summary>Config setting. Name of the menu item to start linking mode.</summary>
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
  public string startLinkMenu = "Start a link";
  /// <summary>Config setting. Name of the menu item to break currently established link.</summary>
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
  public string breakLinkMenu = "Break the link";
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (linkState == LinkState.Linking && guiLinkMode == GUILinkMode.Interactive) {
      UpdateLinkingState();
      if (Input.GetKeyUp(KeyCode.Escape)) {
        AsyncCall.CallOnEndOfFrame(this, x => CancelLinking());
      }
    }
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    // Infinity duration doesn't mean the message will be shown forever. It must be refreshed in the
    // Update method.
    statusScreenMessage = new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
  }
  #endregion

  /// <summary>Variable to store auto save state before starting interactive mode.</summary>
  bool canAutoSaveState;

  #region KASModuleLinkSourceBase overrides
  /// <inheritdoc/>
  public override bool StartLinking(GUILinkMode mode) {
    // Don't allow EVA linking mode.
    if (mode != GUILinkMode.Interactive && mode != GUILinkMode.API) {
      return false;
    }
    return base.StartLinking(mode);
  }

  /// <inheritdoc/>
  protected override void StartLinkGUIMode(GUILinkMode mode) {
    base.StartLinkGUIMode(mode);
    InputLockManager.SetControlLock(
        ControlTypes.All & ~ControlTypes.CAMERACONTROLS, TotalControlLock);
    canAutoSaveState = HighLogic.CurrentGame.Parameters.Flight.CanAutoSave;
    HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;
    linkRenderer.shaderNameOverride = InteractiveShaderName;
    linkRenderer.colorOverride = BadLinkColor;
    linkRenderer.isPhysicalCollider = false;
  }

  /// <inheritdoc/>
  protected override void StopLinkGUIMode() {
    linkRenderer.StopRenderer();
    linkRenderer.shaderNameOverride = null;
    linkRenderer.colorOverride = null;
    linkRenderer.isPhysicalCollider = true;
    ScreenMessages.RemoveMessage(statusScreenMessage);
    InputLockManager.RemoveControlLock(TotalControlLock);
    HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = canAutoSaveState;
    lastHoveredPart = null;
    base.StopLinkGUIMode();

    // Start renderer if link has been established.
    if (linkState == LinkState.Linked) {
      linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    }
  }

  /// <inheritdoc/>
  protected override void OnStateChange(LinkState oldState) {
    base.OnStateChange(oldState);
    Events[StartLinkMenuActionName].guiName = startLinkMenu;
    Events[BreakLinkMenuActionName].guiName = breakLinkMenu;
    Events[StartLinkMenuActionName].active = linkState == LinkState.Available;
    Events[BreakLinkMenuActionName].active = linkState == LinkState.Linked;
  }

  /// <inheritdoc/>
  public override void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkCreatedEvent(info);
    if (info.actor == LinkActorType.Player || info.actor == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(plugSndPath);
    }
  }

  /// <inheritdoc/>
  public override void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkBrokenEvent(info);
    if (info.actor == LinkActorType.Player) {
      UISoundPlayer.instance.Play(unplugSndPath);
    } else if (info.actor == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(brokeSndPath);
    }
  }
  #endregion

  // TODO(ihsoft): Disallow non-eva control.
  #region Action handlers
  /// <summary>Event handler. Initiates a link that must be completed by a mouse click.</summary>
  /// <seealso cref="StartLinkMenuActionName"/>
  [KSPEvent(guiName = "Start a link", guiActive = true, guiActiveUnfocused = true)]
  public void StartLinkContextMenuAction() {
    StartLinking(GUILinkMode.Interactive);
  }

  /// <summary>Event handler. Breaks current link between source and target.</summary>
  /// <seealso cref="BreakLinkMenuActionName"/>
  [KSPEvent(guiName = "Break the link", guiActive = true, guiActiveUnfocused = true)]
  public void BreakLinkContextMenuAction() {
    BreakCurrentLink(LinkActorType.Player);
  }
  #endregion

  #region Local utilities
  /// <summary>Displays linking status in real time.</summary>
  void UpdateLinkingState() {
    // Catch the hovered part, a possible target on it, and the link feasibility.
    if (Mouse.HoveredPart != lastHoveredPart) {
      lastHoveredPart = Mouse.HoveredPart;
      targetCandidateIsGood = false;
      if (lastHoveredPart == null ) {
        targetCandidate = null;
      } else {
        targetCandidate = lastHoveredPart.FindModulesImplementing<ILinkTarget>()
            .FirstOrDefault(x => x.cfgLinkType == cfgLinkType
                            && x.linkState == LinkState.AcceptingLinks);
        if (targetCandidate != null) {
          var linkStatusErrors = new[] {
              CheckBasicLinkConditions(targetCandidate),
              linkRenderer.CheckColliderHits(nodeTransform, targetCandidate.nodeTransform),
              linkJoint.CheckAngleLimitAtSource(this, targetCandidate.nodeTransform),
              linkJoint.CheckAngleLimitAtTarget(this, targetCandidate.nodeTransform),
              linkJoint.CheckLengthLimit(this, targetCandidate.nodeTransform)
          }.Where(x => x != null).ToArray();
          if (linkStatusErrors.Length == 0) {
            targetCandidateIsGood = true;
            statusScreenMessage.message = CanBeConnectedMsg.Format(
                Vector3.Distance(nodeTransform.position, targetCandidate.nodeTransform.position));
          } else {
            statusScreenMessage.message = ScreenMessaging.SetColorToRichText(
                String.Join("\n", linkStatusErrors), ScreenMessaging.ErrorColor);
          }
        }
      }
      // Show the possible link or indicate the error.
      if (targetCandidate != null) {
        linkRenderer.colorOverride = targetCandidateIsGood ? GoodLinkColor : BadLinkColor;
        linkRenderer.StartRenderer(nodeTransform, targetCandidate.nodeTransform);
      } else {
        linkRenderer.colorOverride = BadLinkColor;
        linkRenderer.StopRenderer();
      }
    }

    // Handle link action (mouse click).
    if (targetCandidateIsGood && Input.GetKeyDown(KeyCode.Mouse0)) {
      AsyncCall.CallOnEndOfFrame(this, x => LinkToTarget(targetCandidate));
    }

    // Update linking messages (it needs to be refreshed to not go out by timeout).
    if (targetCandidate == null) {
      statusScreenMessage.message = LinkingInProgressMsg;
    }
    ScreenMessages.PostScreenMessage(statusScreenMessage);
  }
  #endregion
}

}  // namespace
