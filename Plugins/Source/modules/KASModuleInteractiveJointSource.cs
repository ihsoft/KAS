// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using System.Text;
using System.Collections;
using UnityEngine;
using KSPDev.GUIUtils;
using KASAPIv1;

namespace KAS {

// FIXME: docs
public sealed class KASModuleInteractiveJointSource : KASModuleLinkSourceBase, IModuleInfo {
  ScreenMessage linkingMessage;
  ScreenMessage canLinkStatusMessage;
  ScreenMessage cannotLinkStatusMessage;
  const string CanBeConnectedMsg = "Click to establish a link (length {0:F2} m)";
  const string LinkingInProgressMsg = "Select a compatible socket or press ESC";

  /// <summary>Color of pipe in the linking mode when link can be established.</summary>
  readonly static Color GoodLinkColor = new Color(0, 1, 0, 0.5f);
  /// <summary>Color of pipe in the linking mode when link cannot be established.</summary>
  readonly static Color BadLinkColor = new Color(1, 0, 0, 0.5f);
  /// <summary>A lock that restricts anything but camera positioning.</summary>
  const string TotalControlLock = "KASInteractiveJointUberLock";
  /// <summary>Shader that reders pipe during linking.</summary>
  const string InteractiveShaderName = "Transparent/Diffuse";  

  //FIXME: move to the parent
  const string ModuleTitle = "KAS Joint Source";

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
    // FIXME: deal with the attach node. No node from the part should be used.
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
    //FIXME: Create dummy modules when required ones missing. And don't fail.
    if (linkJoint == null) {
      Debug.LogErrorFormat("Dynamic KAS part {0} misses joint module. It won't work properly",
                           part.name);
    }
    if (linkRenderer == null) {
      Debug.LogErrorFormat("Dynamic KAS part {0} misses renderer module. It won't work properly",
                           part.name);
    }
    // Infinity duration doesn't mean the message will be shown forever. It must be refreshed in the
    // Update method.
    linkingMessage =
        new ScreenMessage(LinkingInProgressMsg, Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    canLinkStatusMessage =
        new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    //FIXME: add color highlight
    cannotLinkStatusMessage =
        new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
  }


  void SetupUnlinkedState() {
    RotatePipeToDefaultDirection();
    //FIXME
    //linkRenderer.StartRenderer(nodeTransform, idleLinkTargetTransform);
  }

  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  /// <returns>HTML formatted text to show the in GUI.</returns>
  /// <para>Overridden from <see cref="PartModule"/>.</para>
  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.Append("Requires socket type: ");
    sb.AppendLine(type);
    return sb.ToString();
  }
  #endregion

  #region KASModuleLinkSourceBase overrides
  /// <inheritdoc/>
  public override bool StartLinking(GUILinkMode mode) {
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
    //FIXME
    Debug.LogWarning("Start GUI");
    linkRenderer.shaderNameOverride = InteractiveShaderName;
    linkRenderer.colorOverride = BadLinkColor;
    linkRenderer.isPhysicalCollider = false;
  }


  bool targetCandidateIsGood;
  ILinkTarget targetCandidate;
  Part lastHoveredPart;

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
          var linkStatusError =
              linkJoint.CheckAngleLimitAtSource(this, targetCandidate.nodeTransform)
              ?? linkJoint.CheckAngleLimitAtTarget(this, targetCandidate.nodeTransform)
              ?? linkJoint.CheckLengthLimit(this, targetCandidate.nodeTransform)
              ?? linkRenderer.CheckColliderHits(nodeTransform, targetCandidate.nodeTransform);
          if (linkStatusError == null) {
            targetCandidateIsGood = true;
            canLinkStatusMessage.message = string.Format(
                CanBeConnectedMsg,
                Vector3.Distance(nodeTransform.position, targetCandidate.nodeTransform.position));
          } else {
            cannotLinkStatusMessage.message = linkStatusError;
          }
        }
      }
      // Show the possible link or indicate the error.
      if (targetCandidate != null && targetCandidateIsGood) {
        linkRenderer.colorOverride = GoodLinkColor;
        linkRenderer.StartRenderer(nodeTransform, targetCandidate.nodeTransform);
      } else {
        linkRenderer.colorOverride = BadLinkColor;
        linkRenderer.StopRenderer();
      }
    }

    // Handle link action (mouse click).
    //FIXME: check it in right way so what all the modifiers are honored
    if (targetCandidateIsGood && Input.GetKeyDown(KeyCode.Mouse0)) {
      StartCoroutine(WaitAndLink(targetCandidate));
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

  IEnumerator WaitAndLink(ILinkTarget target) {
    Debug.LogWarning("Link requested! Waiting...");
    yield return new WaitForEndOfFrame();
    //FIXME
    Debug.LogWarningFormat("** Linking to target with state: {0}", target.linkState);
    LinkToTarget(target);
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
  }

  /// <inheritdoc/>
  protected override void OnStateChange(LinkState oldState) {
    base.OnStateChange(oldState);
    Events["StartLinkContextMenuAction"].active = linkState == LinkState.Available;
    Events["BreakLinkContextMenuAction"].active = linkState == LinkState.Linked;
    //FIXME: figure out if still needed
    PartContextMenu.InvalidateContextMenu(part);

    // Adjust renderer state.
    if (linkState == LinkState.Linked && !linkRenderer.isStarted) {
      //FIXME
      Debug.LogWarning("** START linked mode");
      linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    }
    if (linkState != LinkState.Linked && linkRenderer.isStarted) {
      linkRenderer.StopRenderer();
    }
  }

  /// <inheritdoc/>
  protected override bool ConnectParts(ILinkTarget target) {
    var res = base.ConnectParts(target);
    if (res) {
      UISoundPlayer.instance.Play(plugSndPath);
    }
    return res;
  }

  /// <inheritdoc/>
  /// <para>Overrides <see cref="KASModuleLinkSourceBase"/>.</para>
  protected override Vessel DisconnectTargetPart(ILinkTarget target) {
    var res = base.DisconnectTargetPart(target);
    if (res != null) {
      UISoundPlayer.instance.Play(unplugSndPath);
    }
    return res;
  }

  /// <inheritdoc/>
  protected override void UnlinkParts(bool isBrokenExternally = false) {
    base.UnlinkParts(isBrokenExternally);
    if (isBrokenExternally) {
      UISoundPlayer.instance.Play(brokeSndPath);
    }
  }
  #endregion

  #region IModuleInfo implementation
  /// <summary>Returns module title to show in the editor part's details panel.</summary>
  /// <returns>Title of the module.</returns>
  public string GetModuleTitle() {
    return ModuleTitle;
  }

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  public Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  public string GetPrimaryField() {
    return null;
  }
  #endregion

  #region Action handlers
  //FIXME: disallow non-eva control
  [KSPEvent(guiName = "Start a link", guiActive = true, guiActiveUnfocused = true)]
  public void StartLinkContextMenuAction() {
    StartLinking(GUILinkMode.Interactive);
  }

  //FIXME: disallow non-eva control
  [KSPEvent(guiName = "Break the link", guiActive = true, guiActiveUnfocused = true)]
  public void BreakLinkContextMenuAction() {
    BreakCurrentLink();
    // FIXME: ovveride BreakCurrentLink 
    SetupUnlinkedState();
  }
  #endregion

  //FIXME: depreacte in favor of update
  void RotatePipeToDefaultDirection() {
    Debug.LogWarning("Rotate to the limit");
    //FIXME: adjust for anchors
//    idleLinkTargetTransform.localPosition =
//        idlePipeDirection.normalized * linkJoint.cfgMinLinkLength;
//    idleLinkTargetTransform.localRotation.SetLookRotation(-idlePipeDirection);

    //FIXME
    //linkRenderer.startSocketTransfrom.localRotation = Quaternion.LookRotation(idlePipeDirection);

    // In editor OnUpdate() events are not fired. Kick the renderer update via interface. 
    if (HighLogic.LoadedSceneIsEditor) {
      linkRenderer.UpdateLink();
    }
  }

  string ExtractPositionName(string cfgDirectionString) {
    var lastCommaPos = cfgDirectionString.LastIndexOf(',');
    return lastCommaPos != -1
        ? cfgDirectionString.Substring(lastCommaPos + 1)
        : cfgDirectionString;
  }

  Vector3 ExtractDirectionVector(string cfgDirectionString) {
    var lastCommaPos = cfgDirectionString.LastIndexOf(',');
    if (lastCommaPos == -1) {
      Debug.LogWarningFormat("Cannot extract direction from string: {0}", cfgDirectionString);
      return Vector3.forward;
    }
    return ConfigNode.ParseVector3(cfgDirectionString.Substring(0, lastCommaPos));
  }
}

}  // namespace
