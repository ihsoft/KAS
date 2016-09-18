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

/// <summary>A source module that allows connecting two parts with a spring joint.</summary>
/// <remarks>This joint can damper strtuctral stress and allow greater bases to be connected.
/// Though, no structural reinforcement is offered. The components being connected should relay on
/// other mechanisms to ensure structural stability.</remarks>
public sealed class KASModuleFlexibleConnectionSource : KASModuleLinkSourceBase {
  ScreenMessage linkStatusMessage;
  ScreenMessage linkStatusMaxLengthMessage;
  ScreenMessage linkStatusMaxAngleMessage;
  const float LinkingMessageDuration = 4.0f;
  readonly static Color GoodLinkColor = new Color(0, 1, 0, 0.5f);
  readonly static Color BadLinkColor = new Color(1, 0, 0, 0.5f);

  ILinkRenderer evaRenderer {
    get {
      if (_evaRenderer == null && evaLinkRendererName != "") {
        _evaRenderer = part.FindModulesImplementing<ILinkRenderer>()
            .FirstOrDefault(x => x.cfgRendererName == evaLinkRendererName);
      }
      return _evaRenderer;
    }
  }
  ILinkRenderer _evaRenderer;

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
  public string evaLinkRendererName = "";
  #endregion

  #region ILinkSource overrides
  /// <summary>Creates logical and physical link between source and target.</summary>
  /// <remarks>Physical link is implemented via a spring join. The objects will be allowed to move
  /// relatively to each other, and only the psring force will be keeping them together.</remarks>
  /// <para>Overridden from <see cref="KASModuleLinkSourceBase"/>.</para>
  public override bool LinkToTarget(ILinkTarget target) {
    if (base.LinkToTarget(target)) {
      //FIXME: add a spring join
      Debug.LogWarning("here we actually dock/link!");
      return true;
    }
    return false;
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  /// <para>Overridden from <see cref="PartModule"/>.</para>
  public override void OnUpdate() {
    base.OnUpdate();
    if (linkState == LinkState.Linking && guiLinkMode == GUILinkMode.Eva) {
      UpdateEvaLinkingStats();
    }
  }

  /// <inheritdoc/>
  /// <para>Overridden from <see cref="PartModule"/>.</para>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    linkStatusMessage =
        new ScreenMessage("", LinkingMessageDuration, ScreenMessageStyle.UPPER_LEFT);
    linkStatusMaxLengthMessage =
        new ScreenMessage("", LinkingMessageDuration, ScreenMessageStyle.UPPER_LEFT);
    linkStatusMaxAngleMessage =
        new ScreenMessage("", LinkingMessageDuration, ScreenMessageStyle.UPPER_LEFT);
  }

  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  /// <returns>HTML formatted text to show the in GUI.</returns>
  /// <para>Overridden from <see cref="PartModule"/>.</para>
  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.Append("Compatibility: ");
    sb.AppendLine(type);
    if (cfgAllowOtherVesselTarget) {
      sb.AppendLine();
      sb.AppendLine("Can be linked to another vessel");
    }
    return sb.ToString();
  }
  #endregion

  #region KASModuleLinkSourceBase overrides 
  /// <inheritdoc/>
  /// <para>Overrides <see cref="KASModuleLinkSourceBase"/>.</para>
  protected override void StartLinkGUIMode(GUILinkMode mode) {
    base.StartLinkGUIMode(mode);
    if (mode == GUILinkMode.Eva) {
      if (evaRenderer != null) {
        evaRenderer.StartRenderer(nodeTransform, GetEvaNode());
      } else {
        Debug.LogWarningFormat("Cannot find EVA renderer for {0}", part.name);
      }
    }
  }

  /// <inheritdoc/>
  /// <para>Overrides <see cref="KASModuleLinkSourceBase"/>.</para>
  protected override void StopLinkGUIMode() {
    if (guiLinkMode == GUILinkMode.Eva) {
      if (evaRenderer != null) {
        evaRenderer.StopRenderer();
      }
      if (evaTransform != null) {
        evaTransform.gameObject.DestroyGameObject();
      }
    }
    ScreenMessages.RemoveMessage(linkStatusMessage);
    ScreenMessages.RemoveMessage(linkStatusMaxLengthMessage);
    ScreenMessages.RemoveMessage(linkStatusMaxAngleMessage);
    base.StopLinkGUIMode();
  }

  /// <inheritdoc/>
  /// <para>Overrides <see cref="KASModuleLinkSourceBase"/>.</para>
  protected override void OnStateChange(LinkState oldState) {
    base.OnStateChange(oldState);
    Events["StartLinkContextMenuAction"].active = linkState == LinkState.Available;
    Events["CancelLinkContextMenuAction"].active = linkState == LinkState.Linking;
    Events["BreakLinkContextMenuAction"].active = linkState == LinkState.Linked;
    //FIXME: figure out if still needed
    PartContextMenu.InvalidateContextMenu(part);
  }

  /// <inheritdoc/>
  /// <para>Overrides <see cref="KASModuleLinkSourceBase"/>.</para>
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
  /// <para>Overrides <see cref="KASModuleLinkSourceBase"/>.</para>
  protected override void UnlinkParts(bool isBrokenExternally = false) {
    base.UnlinkParts(isBrokenExternally);
    if (isBrokenExternally) {
      UISoundPlayer.instance.Play(brokeSndPath);
    }
  }
  #endregion
  
  /// <summary>Displays linking status in real time.</summary>
  /// <remarks>Shows errors when link cannot be done, and changes link renderer color.</remarks>
  void UpdateEvaLinkingStats() {
    // If linking in EVA mode verify and update link feasibility.
    if (evaRenderer != null) {
      //FIXME: update renderer!

      //FIXME: move to DisplayLinkingStatus(bool hide = false);
      var linkLength = Vector3.Distance(nodeTransform.position, evaTransform.position);
      //FIXME: localizeable string
      //float linkMass = 0;
      //linkStatusMessage.message = string.Format(LinkingStatusTextMsg, linkLength, linkMass);
      //ScreenMessages.PostScreenMessage(linkStatusMessage);

      // When part defines custom joint there may be constraints on the link.
      if (linkJoint != null) {
        var maxLengthMsg = linkJoint.CheckLengthLimit(this, evaTransform);
        if (maxLengthMsg != null) {
          ScreenMessages.PostScreenMessage(maxLengthMsg, linkStatusMaxLengthMessage);
        } else {
          ScreenMessages.RemoveMessage(linkStatusMaxLengthMessage);
        }
        var maxAngleMsg = linkJoint.CheckAngleLimitAtSource(this, evaTransform);
        if (maxAngleMsg != null) {
          ScreenMessages.PostScreenMessage(maxAngleMsg, linkStatusMaxAngleMessage);
        } else {
          ScreenMessages.RemoveMessage(linkStatusMaxAngleMessage);
        }
        evaRenderer.colorOverride = maxLengthMsg == null && maxAngleMsg == null
            ? GoodLinkColor : BadLinkColor;
      }
    }
  }

  // FIXME: move this stuff into EVA module
  Transform evaTransform;
  Vector3 evaStrutPos = new Vector3(0.05f, 0.059f, -0.21f);
  Vector3 evaStrutRot = new Vector3(190.0f, 0.0f, 0.0f);
  Transform GetEvaNode() {
    if (evaTransform != null) {
      evaTransform.gameObject.DestroyGameObject();
    }
    evaTransform = new GameObject("KASLinkEvaPeerNode").transform;
    evaTransform.position = FlightGlobals.ActiveVessel.transform.TransformPoint(evaStrutPos);
    evaTransform.rotation =
        FlightGlobals.ActiveVessel.transform.rotation * Quaternion.Euler(evaStrutRot);
    evaTransform.parent = FlightGlobals.ActiveVessel.transform;
    return evaTransform;
  }

  #region Action handlers
  [KSPEvent(guiName = "Start flexible link", guiActive = true, guiActiveUnfocused = true)]
  public void StartLinkContextMenuAction() {
    StartLinking(GUILinkMode.Eva);
  }

  [KSPEvent(guiName = "Cancel link mode", guiActive = true, guiActiveUnfocused = true)]
  public void CancelLinkContextMenuAction() {
    CancelLinking();
  }

  [KSPEvent(guiName = "Break link", guiActive = true, guiActiveUnfocused = true)]
  public void BreakLinkContextMenuAction() {
    BreakCurrentLink();
  }
  #endregion
}

}  // namespace
