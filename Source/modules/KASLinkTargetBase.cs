// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
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
// Next localization ID: #kasLOC_03002.
public class KASLinkTargetBase :
    // KSP parents.
    AbstractLinkPeer, IModuleInfo,
    // KAS parents.
    ILinkTarget,
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

  #region AbstractLinkPeer overrides
  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition += (start, end) => HostedDebugLog.Fine(
        this, "Target state changed at {0}: {1} => {2}", attachNodeName, start, end);
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
        enterHandler: x => KASAPI.KasEvents.OnStartLinking.Add(OnStartConnecting),
        leaveHandler: x => KASAPI.KasEvents.OnStartLinking.Remove(OnStartConnecting));
    linkStateMachine.AddStateHandlers(
        LinkState.AcceptingLinks,
        enterHandler: x => KASAPI.KasEvents.OnStopLinking.Add(OnStopConnecting),
        leaveHandler: x => KASAPI.KasEvents.OnStopLinking.Remove(OnStopConnecting));
    linkStateMachine.AddStateHandlers(
        LinkState.RejectingLinks,
        enterHandler: x => KASAPI.KasEvents.OnStopLinking.Add(OnStopConnecting),
        leaveHandler: x => KASAPI.KasEvents.OnStopLinking.Remove(OnStopConnecting));
    linkStateMachine.AddStateHandlers(
        LinkState.AcceptingLinks,
        enterHandler: x => SetEligiblePartHighlighting(true),
        leaveHandler: x => SetEligiblePartHighlighting(false),
        callOnShutdown: false);
  }

  /// <inheritdoc/>
  protected override void OnPeerChange(ILinkPeer oldPeer) {
    base.OnPeerChange(oldPeer);
    linkState = linkSource != null ? LinkState.Linked : LinkState.Available;

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
        isNodeBlocked = true;
      } else if (linkState == LinkState.NodeIsBlocked && parsedAttachNode.attachedPart == null) {
        isNodeBlocked = false;
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
  protected virtual void OnStartConnecting(ILinkSource source) {
    linkState = CheckCanLinkWith(source) ? LinkState.AcceptingLinks : LinkState.RejectingLinks;
  }

  /// <summary>Cancels  the linking mode on this module.</summary>
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
