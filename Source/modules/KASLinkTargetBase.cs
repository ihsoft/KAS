// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.GUIUtils;
using KSPDev.DebugUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Base link target module. Only controls target link state.</summary>
/// <remarks>
/// This module only deals with logic part of the linking. It remembers the source and notifies
/// other modules on the part about the link state. The actual work to make the link significant in
/// the game engine must be done by the link source, an implementation of <see cref="ILinkSource"/>.
/// <para>
/// External callers must access methods and properties declared in KSP base classes or interfaces
/// only. Members and methods that are not part of these declarations are not intended for the
/// public use <b>regardless</b> to their visibility level.
/// </para>
/// </remarks>
// Next localization ID: #kasLOC_03002.
public class KASLinkTargetBase :
    // KSP parents.
    AbstractLinkPeer, IModuleInfo,
    // KAS interfaces.
    ILinkTarget, IHasDebugAdjustables,
    // Syntax sugar parents.
    IsPartDeathListener, IKSPDevModuleInfo {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> AcceptsLinkTypeInfo = new Message<string>(
      "#kasLOC_03000",
      defaultTemplate: "Accepts link type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ModuleTitleInfo = new Message(
      "#kasLOC_03001",
      defaultTemplate: "KAS Joint Target",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region ILinkTarget properties implementation
  /// <inheritdoc/>
  public virtual ILinkSource linkSource {
    get { return otherPeer as ILinkSource; }
    set { SetOtherPeer(value); }
  }
  #endregion

  #region Part's config fields
  /// <summary>
  /// Tells if compatible targets should highlight themselves when linking mode started.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Highlight parts")]
  public bool highlightCompatibleTargets = true;

  /// <summary>Defines highlight color for the compatible targets.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Highlight color")]
  public Color highlightColor = Color.cyan;
  #endregion

  #region AbstractLinkPeer overrides
  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition += (start, end) => HostedDebugLog.Fine(
        this, "Target state changed: node={0}, state {1} => {2}", attachNodeName, start, end);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.AcceptingLinks, LinkState.NodeIsBlocked});
    linkStateMachine.SetTransitionConstraint(
        LinkState.NodeIsBlocked,
        new[] {LinkState.Available});
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
        enterHandler: x => KASAPI.KasEvents.OnStartLinking.Add(OnStartLinkingKASEvent),
        leaveHandler: x => KASAPI.KasEvents.OnStartLinking.Remove(OnStartLinkingKASEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.AcceptingLinks,
        enterHandler: x => KASAPI.KasEvents.OnStopLinking.Add(OnStopLinkingKASEvent),
        leaveHandler: x => KASAPI.KasEvents.OnStopLinking.Remove(OnStopLinkingKASEvent));
    linkStateMachine.AddStateHandlers(
        LinkState.AcceptingLinks,
        enterHandler: x => SetEligiblePartHighlighting(true),
        leaveHandler: x => SetEligiblePartHighlighting(false),
        callOnShutdown: false);
  }

  /// <inheritdoc/>
  protected override void OnPeerChange(ILinkPeer oldPeer) {
    base.OnPeerChange(oldPeer);
    SetLinkState(linkSource != null ? LinkState.Linked : LinkState.Available);

    // Trigger events on the part.
    var oldSource = oldPeer as ILinkSource;
    if (linkStateMachine.currentState != null && oldSource != linkSource) {
      var linkInfo = new KasLinkEventImpl(linkSource ?? oldSource, this);
      if (linkSource != null) {
        part.Modules.OfType<ILinkStateEventListener>().ToList()
            .ForEach(x => x.OnKASLinkedState(linkInfo, isLinked: true));
      } else {
        part.Modules.OfType<ILinkStateEventListener>().ToList()
            .ForEach(x => x.OnKASLinkedState(linkInfo, isLinked: false));
      }
    }
  }

  /// <inheritdoc/>
  protected override void CheckCoupleNode() {
    base.CheckCoupleNode();
    // The source is responsible to handle the link, which may be done at the end of frame. So put
    // our check at the end of the frame queue to go behind any delayed actions.
    AsyncCall.CallOnEndOfFrame(this, () => {
      if (linkState == LinkState.Available
          && parsedAttachNode != null && parsedAttachNode.attachedPart != null) {
        SetLinkState(LinkState.NodeIsBlocked);
      } else if (linkState == LinkState.NodeIsBlocked && parsedAttachNode.attachedPart == null) {
        SetLinkState(LinkState.Available);
      }
    });
  }
  #endregion

  #region IHasDebugAdjustables implementation
  ILinkSource dbgOldSource;
  float cableLength;

  /// <inheritdoc/>
  public override void OnBeforeDebugAdjustablesUpdate() {
    base.OnBeforeDebugAdjustablesUpdate();
    if (linkState != LinkState.Linked && linkState != LinkState.Available) {
      throw new InvalidOperationException("Cannot adjust value in link state: " + linkState);
    }
    dbgOldSource = linkSource;
    if (isLinked) {
      var cableJoint = linkSource.linkJoint as ILinkCableJoint;
      if (cableJoint != null) {
        cableLength = cableJoint.deployedCableLength;
      }
      linkSource.BreakCurrentLink(LinkActorType.Player);
    }
  }

  /// <inheritdoc/>
  public override void OnDebugAdjustablesUpdated() {
    base.OnDebugAdjustablesUpdated();
    AsyncCall.CallOnEndOfFrame(
        this,
        () => {
          InitModuleSettings();
          if (dbgOldSource != null && dbgOldSource.LinkToTarget(LinkActorType.Player, this)) {
            var cableJoint = linkSource.linkJoint as ILinkCableJoint;
            if (cableJoint != null) {
              cableJoint.SetCableLength(cableLength);
            }
          }
        },
        skipFrames: 2);  // The link's logic is asynchronous, give it 2 frames to settle.
  }
  #endregion

  #region IsPartDeathListener implemenation
  /// <inheritdoc/>
  public virtual void OnPartDie() {
    if (isLinked) {
      HostedDebugLog.Info(this, "Part has died. Drop the link to: {0}", linkSource);
      linkSource.BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    sb.AppendLine(AcceptsLinkTypeInfo.Format(
        !string.IsNullOrEmpty(linkTypeDisplayName)
        ? linkTypeDisplayName
        : linkType));
    return sb.ToString();
  }

  /// <inheritdoc/>
  public string GetModuleTitle() {
    return ModuleTitleInfo;
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

  #region KASEvents listeners
  /// <summary>
  /// Fires when this module can link, and there is a source that has actived the linking mode.
  /// </summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="source"></param>
  protected virtual void OnStartLinkingKASEvent(ILinkSource source) {
    if (CheckCanLinkWith(source)) {
      SetLinkState(LinkState.AcceptingLinks);
    }
  }

  /// <summary>Cancels  the linking mode on this module.</summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="connectionSource"></param>
  protected virtual void OnStopLinkingKASEvent(ILinkSource connectionSource) {
    if (!isLocked) {
      SetLinkState(LinkState.Available);
    }
  }
  #endregion

  #region New inheritable methods
  /// <summary>Verifies that part can link with the source.</summary>
  /// <remarks>
  /// It only checks if the source is <i>eligibile</i> to link with this target, not the actual
  /// conditions. The source is responsible to verify all the conditions before finiliszing the link.
  /// </remarks>
  /// <param name="source">Source to check against.</param>
  /// <returns>
  /// <c>true</c> if link is <i>technically</i> possible. It's not guaranteed that the link will
  /// succeed.
  /// </returns>
  protected virtual bool CheckCanLinkWith(ILinkSource source) {
    // Cannot attach to itself or incompatible link type.
    if (part != source.part && cfgLinkType == source.cfgLinkType) {
      return true;
    }
    // Link is not allowed.
    return false;
  }
  #endregion

  #region Local untility methods
  /// <summary>Finds a compatible source linked to the EVA kerbal.</summary>
  /// <returns>The source or <c>null</c> if nothing found.</returns>
  ILinkTarget FindEvaTargetWithConnector() {
    if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ActiveVessel.isEVA) {
      return null;
    }
    return FlightGlobals.ActiveVessel
        .FindPartModulesImplementing<ILinkTarget>()
        .FirstOrDefault(t => t.isLinked && t.cfgLinkType == cfgLinkType);
  }

  /// <summary>Sets the highlighter state on the part.</summary>
  /// <remarks>
  /// Does nothing if the <see cref="highlightCompatibleTargets"/> settings is set to <c>false</c>.
  /// </remarks>
  /// <param name="isHighlighted">The highlighting state.</param>
  /// <seealso cref="highlightCompatibleTargets"/>
  void SetEligiblePartHighlighting(bool isHighlighted) {
    if (highlightCompatibleTargets) {
      if (isHighlighted) {
        part.SetHighlightType(Part.HighlightType.AlwaysOn);
        part.SetHighlightColor(highlightColor);
        part.SetHighlight(true, false);
      } else {
        part.SetHighlightDefault();
      }
    }
  }
  #endregion
}

}  // namespace
