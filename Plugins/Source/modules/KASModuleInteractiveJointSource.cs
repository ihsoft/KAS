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
  const string CanBeConnectedMsg = "Can be connected: link length {0:F2} m";
  const string LinkingInProgressMsg = "Select compatible socket or press ESC";

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
  public string pipeDirection0Menu = "";
  [KSPField]
  public string pipeDirection1Menu = "";
  [KSPField]
  public string pipeDirection2Menu = "";
  [KSPField]
  public string startLinkMenu = "Start a link";
  [KSPField]
  public string breakLinkMenu = "Break the link";
  [KSPField(isPersistant = true)]
  public Vector3 idlePipeDirection = Vector3.forward;
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    Debug.LogWarningFormat("*************** ONAWAKE! {0}", part.name);
    base.OnAwake();
    Events["PipeDirection0ContextMenuAction"].guiName = ExtractPositionName(pipeDirection0Menu);
    Events["PipeDirection1ContextMenuAction"].guiName = ExtractPositionName(pipeDirection1Menu);
    Events["PipeDirection2ContextMenuAction"].guiName = ExtractPositionName(pipeDirection2Menu);
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
    Debug.LogWarning("*************** ONSTART!");
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

    // FIXME: check for the link state
    if (linkState == LinkState.Available) {
      //SetupUnlinkedState();
    }
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
    linkRenderer.isPhysicalCollider = false;
  }

  /// <summary>Displays linking status in real time.</summary>
  void UpdateLinkingState() {
    ScreenMessages.PostScreenMessage(linkingMessage);

    // Figure out if link can be established and what is the target.
    var canLink = false;
    string linkStatusError = null;
    if (Mouse.HoveredPart != null) {
      var target = Mouse.HoveredPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(x => x.cfgLinkType == cfgLinkType
                          && x.linkState == LinkState.AcceptingLinks);
      if (target != null) {
        linkStatusError =
            linkJoint.CheckAngleLimitAtSource(this, target.nodeTransform)
            ?? linkJoint.CheckAngleLimitAtTarget(this, target.nodeTransform)
            ?? linkJoint.CheckLengthLimit(this, target.nodeTransform)
            ?? linkRenderer.CheckColliderHits(nodeTransform, target.nodeTransform);
        canLink = linkStatusError == null;
        if (canLink) {
          canLinkStatusMessage.message = string.Format(
              CanBeConnectedMsg,
              Vector3.Distance(nodeTransform.position, target.nodeTransform.position));

          //FIXME: make the code better
//          idleLinkTargetTransform.position = target.nodeTransform.position;
//          idleLinkTargetTransform.LookAt(nodeTransform);
          linkRenderer.endSocketTransfrom.position = target.nodeTransform.position;
          linkRenderer.endSocketTransfrom.LookAt(nodeTransform);
          
          //FIXME: check it in right way so what all the modifiers are honored
          if (Input.GetKeyDown(KeyCode.Mouse0)) {
            StartCoroutine(WaitAndLink(target));
          }
        }
      }
    }

    // Update linking messages and set good/bad color on the pipe.
    if (canLink) {
      linkRenderer.colorOverride = GoodLinkColor;
      ScreenMessages.PostScreenMessage(canLinkStatusMessage);
      ScreenMessages.RemoveMessage(cannotLinkStatusMessage);
    } else {
      linkRenderer.colorOverride = BadLinkColor;
      if (linkStatusError != null) {
        cannotLinkStatusMessage.message = linkStatusError;
        ScreenMessages.PostScreenMessage(cannotLinkStatusMessage);
      } else {
        ScreenMessages.RemoveMessage(cannotLinkStatusMessage);
        RotatePipeToDefaultDirection();
      }
      ScreenMessages.RemoveMessage(canLinkStatusMessage);
    }
  }

  IEnumerator WaitAndLink(ILinkTarget target) {
    Debug.LogWarning("Link requested! Waiting...");
    yield return new WaitForEndOfFrame();
    LinkToTarget(target);
  }

  /// <inheritdoc/>
  protected override void StopLinkGUIMode() {
    linkRenderer.shaderNameOverride = null;
    linkRenderer.colorOverride = null;
    linkRenderer.isPhysicalCollider = true;
    ScreenMessages.RemoveMessage(linkingMessage);
    ScreenMessages.RemoveMessage(canLinkStatusMessage);
    ScreenMessages.RemoveMessage(cannotLinkStatusMessage);
    InputLockManager.RemoveControlLock(TotalControlLock);
    base.StopLinkGUIMode();
  }

  /// <inheritdoc/>
  protected override void OnStateChange(LinkState oldState) {
    base.OnStateChange(oldState);
    Events["PipeDirection0ContextMenuAction"].active =
        ExtractPositionName(pipeDirection0Menu) != "" && linkState == LinkState.Available;
    Events["PipeDirection1ContextMenuAction"].active =
        ExtractPositionName(pipeDirection1Menu) != "" && linkState == LinkState.Available;
    Events["PipeDirection2ContextMenuAction"].active =
        ExtractPositionName(pipeDirection2Menu) != "" && linkState == LinkState.Available;
    Events["StartLinkContextMenuAction"].active = linkState == LinkState.Available;
    Events["BreakLinkContextMenuAction"].active = linkState == LinkState.Linked;
    //FIXME: figure out if still needed
    PartContextMenu.InvalidateContextMenu(part);
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
  //FIXME: allow editor access
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void PipeDirection0ContextMenuAction() {
    idlePipeDirection = ExtractDirectionVector(pipeDirection0Menu);
    RotatePipeToDefaultDirection();
  }

  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void PipeDirection1ContextMenuAction() {
    idlePipeDirection = ExtractDirectionVector(pipeDirection1Menu);
    RotatePipeToDefaultDirection();
  }

  [KSPEvent(guiName = "Pipe position 2", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void PipeDirection2ContextMenuAction() {
    idlePipeDirection = ExtractDirectionVector(pipeDirection2Menu);
    RotatePipeToDefaultDirection();
  }

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
    linkRenderer.startSocketTransfrom.localRotation = Quaternion.LookRotation(idlePipeDirection);

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
