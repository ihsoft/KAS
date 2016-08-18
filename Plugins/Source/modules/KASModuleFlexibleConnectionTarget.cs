// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using KASAPIv1;
using KSPDev.GUIUtils;
using UnityEngine;

namespace KAS {

/// <summary>A target module that allows connecting two parts with a spring joint.</summary>
/// <remarks>It's a counterpart to a link source. Part's configuration defines which source this
/// target will satisfy.</remarks>
//FIXME(ihsoft): Turn into unversal module and пet menu text from config.
public sealed class KASModuleFlexibleConnectionTarget : KASModuleLinkTargetBase, IActivateOnDecouple {

  //FIXME
  public void DecoupleAction(string nodeName, bool weDecouple) {
    Debug.LogWarningFormat("TARGET: ** DecoupleAction: {0} (id={3}, weDecouple={1}, linkState={2}",
                           nodeName, weDecouple, linkState, part.flightID);
  }


  /// <inheritdoc/>
  protected override void OnStateChange(LinkState oldState) {
    base.OnStateChange(oldState);
    Events["MakeLinkContextMenuAction"].active = (linkState == LinkState.AcceptingLinks);
    Events["BreakLinkContextMenuAction"].active = (linkState == LinkState.Linked);
    PartContextMenu.InvalidateContextMenu(part);
  }

  [KSPEvent(guiName = "Make flexible link", guiActive = true, guiActiveUnfocused = true)]
  public void MakeLinkContextMenuAction() {
    KASEvents.OnLinkAccepted.Fire(this);
  }

  [KSPEvent(guiName = "Break link", guiActive = true, guiActiveUnfocused = true)]
  public void BreakLinkContextMenuAction() {
    linkSource.BreakCurrentLink();
  }
}

}  // namespace
