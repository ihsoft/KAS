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

// FIXME: docs
public sealed class KASModuleInteractiveJointSource : KASModuleLinkSourceBase {

  #region Localizable strings
  static Message<float> CanBeConnectedMsg = "Click to establish a link (length {0:F2} m)";
  static Message LinkingInProgressMsg = "Select a compatible socket or press ESC";
  #endregion

  //FIXME: use single message object
  ScreenMessage linkingMessage;
  ScreenMessage canLinkStatusMessage;
  ScreenMessage cannotLinkStatusMessage;

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

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public string plugSndPath = "KAS/Sounds/plugdocked";
  [KSPField]
  public string unplugSndPath = "KAS/Sounds/unplugdocked";
  [KSPField]
  public string brokeSndPath = "KAS/Sounds/broke";
  [KSPField]
  public string startLinkMenu = "Start a link";
  [KSPField]
  public string breakLinkMenu = "Break the link";
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    Events["StartLinkContextMenuAction"].guiName = startLinkMenu;
    Events["BreakLinkContextMenuAction"].guiName = breakLinkMenu;
  }
  
  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (linkState == LinkState.Linking && guiLinkMode == GUILinkMode.Interactive) {
      UpdateLinkingState();
      if (Input.GetKeyDown(KeyCode.Escape)) {
        CancelLinking();
      }
    }
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    // Infinity duration doesn't mean the message will be shown forever. It must be refreshed in the
    // Update method.
    linkingMessage =
        new ScreenMessage(LinkingInProgressMsg, Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    canLinkStatusMessage =
        new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    cannotLinkStatusMessage =
        new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
  }
  #endregion

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
    ScreenMessages.PostScreenMessage(linkingMessage);
    InputLockManager.SetControlLock(
        ControlTypes.All & ~ControlTypes.CAMERACONTROLS, TotalControlLock);
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
    ScreenMessages.RemoveMessage(linkingMessage);
    ScreenMessages.RemoveMessage(canLinkStatusMessage);
    ScreenMessages.RemoveMessage(cannotLinkStatusMessage);
    InputLockManager.RemoveControlLock(TotalControlLock);
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
    Events["StartLinkContextMenuAction"].active = linkState == LinkState.Available;
    Events["BreakLinkContextMenuAction"].active = linkState == LinkState.Linked;
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

  //FIXME: disallow non-eva control
  #region Action handlers
  [KSPEvent(guiName = "Start a link", guiActive = true, guiActiveUnfocused = true)]
  public void StartLinkContextMenuAction() {
    StartLinking(GUILinkMode.Interactive);
  }

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
              linkRenderer.CheckColliderHits(nodeTransform, targetCandidate.nodeTransform),
              linkJoint.CheckAngleLimitAtSource(this, targetCandidate.nodeTransform),
              linkJoint.CheckAngleLimitAtTarget(this, targetCandidate.nodeTransform),
              linkJoint.CheckLengthLimit(this, targetCandidate.nodeTransform)
          }.Where(x => x != null).ToArray();
          if (linkStatusErrors.Length == 0) {
            targetCandidateIsGood = true;
            canLinkStatusMessage.message = CanBeConnectedMsg.Format(
                Vector3.Distance(nodeTransform.position, targetCandidate.nodeTransform.position));
          } else {
            cannotLinkStatusMessage.message = ScreenMessaging.SetColorToRichText(
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
    //FIXME: check it in right way so what all the modifiers are honored
    if (targetCandidateIsGood && Input.GetKeyDown(KeyCode.Mouse0)) {
      AsyncCall.CallOnEndOfFrame(this, x => LinkToTarget(targetCandidate));
    }

    // Update linking messages (they need to be refreshed to not go out by a timeout).
    if (targetCandidateIsGood) {
      ScreenMessages.PostScreenMessage(canLinkStatusMessage);
      ScreenMessages.RemoveMessage(cannotLinkStatusMessage);
      ScreenMessages.RemoveMessage(linkingMessage);
    } else {
      ScreenMessages.RemoveMessage(canLinkStatusMessage);
      if (targetCandidate != null) {
        // There is a target but it's not good for a link. Refresh an error message.
        ScreenMessages.PostScreenMessage(cannotLinkStatusMessage);
        ScreenMessages.RemoveMessage(linkingMessage);
      } else {
        // No target is found. Show status message and hide errors if any.
        ScreenMessages.PostScreenMessage(linkingMessage);
        ScreenMessages.RemoveMessage(cannotLinkStatusMessage);
      }
    }
  }
  #endregion
}

}  // namespace
