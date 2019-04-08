// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using System;
using System.Linq;
using UnityEngine;
using KSPDev.ProcessingUtils;

namespace Examples {

public class ILinkTargetExample  {

  #region StateModel
  // Sets up a sample state machine for the target states.
  public static void SetupTargetStateModel() {
    var linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.AcceptingLinks});
    linkStateMachine.SetTransitionConstraint(
        LinkState.AcceptingLinks,
        new[] {LinkState.Available, LinkState.Linked, LinkState.Locked});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Linked,
        new[] {LinkState.Available});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Locked,
        new[] {LinkState.Available});

    linkStateMachine.AddStateHandlers(
        LinkState.Available,
        enterHandler: x => Debug.Log("Target is available"),
        leaveHandler: x => Debug.Log("Target is NOT available"));
  }
  #endregion

  #region FindSourceFromTarget
  // Returns the linked part of the target, if any. It assumes there is exactly one target module
  // on the part.
  public static Part FindSourceFromTarget(Part tgtPart) {
    var source = tgtPart.FindModulesImplementing<ILinkTarget>()
        .FirstOrDefault(s => s.linkSource != null);
    if (source == null) {
      Debug.Log("Target is not connected");
      return null;
    }
    return source.linkSource.part;
  }
  #endregion

  readonly Part part = null;

  #region HighlightLocked
  // Highlights the part in RED if it it could potentionally link with the linking source, but
  // it was explicitly disallowed (e.g. due to the state model).
  public bool isLocked {
    get { return _isLocked; }
    set {
      // This will never throw, but a real implementation would check a state model here.
      _isLocked = value;
      if (_isLocked) {
        part.SetHighlightColor(Color.red);
        part.SetHighlight(true, false);
      } else {
        part.SetHighlightDefault();
      }
    }
  }
  bool _isLocked;
  #endregion
}

};  // namespace
