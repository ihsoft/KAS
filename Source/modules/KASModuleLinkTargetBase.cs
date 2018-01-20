// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
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
// TODO(ihsoft): Add code samples.
// Next localization ID: #kasLOC_03004.
public class KASModuleLinkTargetBase :
    // KSP parents.
    AbstractLinkPeer, IModuleInfo,
    // KAS parents.
    ILinkTarget, ILinkStateEventListener,
    // Syntax sugar parents.
    IsPartDeathListener, IKSPDevModuleInfo {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected static readonly Message<string> AcceptsLinkTypeInfo = new Message<string>(
      "#kasLOC_03000",
      defaultTemplate: "Accepts link type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message ModuleTitleInfo = new Message(
      "#kasLOC_03001",
      defaultTemplate: "KAS Joint Target",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region ILinkTarget properties implementation
  /// <inheritdoc/>
  public virtual ILinkSource linkSource {
    get { return otherPeer as ILinkSource; }
    set { otherPeer = value; }
  }
  #endregion

  #region Part's config fields
  /// <summary>
  /// Tells if compatible targets should highlight themselves when linking mode started.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public bool highlightCompatibleTargets = true;

  /// <summary>Defines highlight color for the compatible targets.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Color highlightColor = Color.cyan;
  #endregion

  #region Context menu events/actions
  /// <summary>
  /// Context menu item to have the EVA carried connector attached to the target part.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveUncommand = true,
            externalToEVAOnly = true, active = false)]
  [LocalizableItem(
      tag = "#kasLOC_03002",
      defaultTemplate = "Attach connector",
      description = "Context menu item to have the EVA carried connector attached to the target"
      + " part.")]
  public void LinkWithCarriableConnectorEvent() {
    var kerbalTarget = FindEvaTargetWithConnector();
    if (kerbalTarget != null) {
      var connectorSource = kerbalTarget.linkSource;
      if (connectorSource.CheckCanLinkTo(this, reportToGUI: true, checkStates: false)) {
        connectorSource.BreakCurrentLink(LinkActorType.Player, moveFocusOnTarget: true);
        if (connectorSource.CheckCanLinkTo(this, reportToGUI: true)
            && connectorSource.StartLinking(GUILinkMode.API, LinkActorType.Player)) {
          if (!connectorSource.LinkToTarget(this)) {
            connectorSource.CancelLinking();
          }
        }
      }
      if (!ReferenceEquals(connectorSource.linkTarget, this)) {
        UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
      }
    }
  }

  /// <summary>Context menu item to break the currently established link.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true, guiActiveUncommand = true, active = false)]
  [LocalizableItem(
      tag = "#kasLOC_03003",
      defaultTemplate = "Detach connector",
      description = "Context menu item to break the currently established link.")]
  public void BreakLinkEvent() {
    if (linkSource != null) {
      linkSource.BreakCurrentLink(LinkActorType.Player,
                                  moveFocusOnTarget: FlightGlobals.ActiveVessel == vessel);
    }
  }
  #endregion

  #region AbstractLinkPeer overrides
  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition +=
        (start, end) => HostedDebugLog.Fine(this, "Target state changed: {0} => {1}", start, end);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.AcceptingLinks, LinkState.RejectingLinks, LinkState.NodeIsBlocked});
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
    linkStateMachine.SetTransitionConstraint(
        LinkState.RejectingLinks,
        new[] {LinkState.Available, LinkState.Locked});

    linkStateMachine.AddStateHandlers(
        LinkState.Available,
        enterHandler: x => {
          KASEvents.OnStartLinking.Add(OnStartConnecting);
          GameEvents.onPartActionUICreate.Add(OnPartGUIStart);
        },
        leaveHandler: x => {
          KASEvents.OnStartLinking.Remove(OnStartConnecting);
          GameEvents.onPartActionUICreate.Remove(OnPartGUIStart);
          PartModuleUtils.SetupEvent(this, LinkWithCarriableConnectorEvent, e => e.active = false);
        });
    linkStateMachine.AddStateHandlers(
        LinkState.AcceptingLinks,
        enterHandler: x => {
          SetEligiblePartHighlighting(true);
          KASEvents.OnStopLinking.Add(OnStopConnecting);
        },
        leaveHandler: x => {
          SetEligiblePartHighlighting(false);
          KASEvents.OnStopLinking.Remove(OnStopConnecting);
        });
    linkStateMachine.AddStateHandlers(
        LinkState.RejectingLinks,
        enterHandler: x => KASEvents.OnStopLinking.Add(OnStopConnecting),
        leaveHandler: x => KASEvents.OnStopLinking.Remove(OnStopConnecting));
    linkStateMachine.AddStateHandlers(
        LinkState.Linked,
        enterHandler: x => PartModuleUtils.SetupEvent(this, BreakLinkEvent, e => e.active = true),
        leaveHandler: x => PartModuleUtils.SetupEvent(this, BreakLinkEvent, e => e.active = false));
  }

  /// <inheritdoc/>
  protected override void OnPeerChange(ILinkPeer oldPeer) {
    base.OnPeerChange(oldPeer);
    linkState = linkSource != null ? LinkState.Linked : LinkState.Available;

    // Trigger events on the part.
    var oldSource = oldPeer as ILinkSource;
    if (linkStateMachine.currentState != null && oldSource != linkSource) {
      var linkInfo = new KASEvents.LinkEvent(linkSource ?? oldSource, this);
      if (linkSource != null) {
        part.FindModulesImplementing<ILinkStateEventListener>()
            .ForEach(x => x.OnKASLinkCreatedEvent(linkInfo));
      } else {
        part.FindModulesImplementing<ILinkStateEventListener>()
            .ForEach(x => x.OnKASLinkBrokenEvent(linkInfo));
      }
    }
  }

  /// <inheritdoc/>
  protected override void CheckAttachNode() {
    // The source is responsible to handle the link, which may be done at the end of frame. So put
    // our check at the end of the frame queue.
    AsyncCall.CallOnEndOfFrame(this, () => {
      if (!isLinked && attachNode != null && attachNode.attachedPart != null) {
        linkState = LinkState.NodeIsBlocked;
      }
      if (linkState == LinkState.NodeIsBlocked
          && (attachNode == null || attachNode.attachedPart == null)) {
        linkState = LinkState.Available;
      }
    });
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
    sb.Append(AcceptsLinkTypeInfo.Format(linkType));
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

  #region ILinkEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    // Lock this target if another target on the part has accepted the link.
    if (!isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    // Unlock this target if link with another target on the part has broke.
    if (isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = false;
    }
  }
  #endregion

  #region KASEvents listeners
  /// <summary>Reacts on source link mode change.</summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="source"></param>
  protected virtual void OnStartConnecting(ILinkSource source) {
    linkState = CheckCanLinkWith(source) ? LinkState.AcceptingLinks : LinkState.RejectingLinks;
  }

  /// <summary>Reacts on source link mode change.</summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="connectionSource"></param>
  protected virtual void OnStopConnecting(ILinkSource connectionSource) {
    linkState = LinkState.Available;
  }
  #endregion

  #region New inheritable methods
  /// <summary>Verifies that part can link with the source.</summary>
  /// <param name="source">Source to check against.</param>
  /// <returns>
  /// <c>true</c> if link is <i>technically</i> possible. It's not guaranteed that the link will
  /// succeed.
  /// </returns>
  protected virtual bool CheckCanLinkWith(ILinkSource source) {
    // Cannot attach to itself or incompatible link type.
    if (part == source.part || cfgLinkType != source.cfgLinkType) {
      return false;
    }
    // Check if same vessel part links are enabled. 
    if (source.part.vessel == vessel
        && (source.cfgLinkMode == LinkMode.TiePartsOnSameVessel
            || source.cfgLinkMode == LinkMode.TieAnyParts)) {
      return true;
    }
    // Check if different vessel part links are enabled. 
    if (source.part.vessel != vessel
        && (source.cfgLinkMode == LinkMode.TiePartsOnDifferentVessels
            || source.cfgLinkMode == LinkMode.TieAnyParts)) {
      return true;
    }
    // Link is not allowed.
    return false;
  }
  #endregion

  #region Local untility methods
  /// <summary>Updates the GUI items when a part's context menu is opened.</summary>
  /// <param name="menuOwnerPart">The part for which the UI is created.</param>
  void OnPartGUIStart(Part menuOwnerPart) {
    if (menuOwnerPart == part) {
      PartModuleUtils.SetupEvent(this, LinkWithCarriableConnectorEvent,
                                 x => x.active = FindEvaTargetWithConnector() != null);
    }
  }

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
