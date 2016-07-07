// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using UnityEngine;
using KAS_API;
using KSPDev.Processing;

namespace KAS {

/// <summary>Base link source module. Does all the job on making two parts linked.</summary>
/// <remarks>This module deals with main logic of linking two parts together. The other party of the
/// link must be aware of the linking porcess. The targets must implement <see cref="ILinkTarget"/>.
/// <para>External callers must access methods and properties declared in base classes or interfaces
/// only. Members and methods that are not part of these declarations are not intended for the
/// public use <b>regardless</b> to their visibility level.</para>
/// <para>Decendand classes may use any members and methods but good practice is restricting the
/// usage to the interfaces and virtuals only.</para>
/// </remarks>
public class KASModuleLinkSourceBase : PartModule, ILinkSource, ILinkEventListener {
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public string linkType { get { return type; } }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public ILinkTarget linkTarget { get; private set; }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public LinkState linkState {
    get { return linkStateMachine.currentState; }
    protected set {
      var oldState = linkStateMachine.currentState;
      linkStateMachine.currentState = value;
      OnStateChange(oldState);
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual bool isLocked {
    get { return linkState == LinkState.Locked; }
    set { linkState = value ? LinkState.Locked : LinkState.Available; }
  }

  /// <summary>State machine that controls event reaction in different states.</summary>
  /// <remarks>Primary usage of the machine is managing subscriptions to the different game events. 
  /// It's highly discouraged to use it for firing events or taking actions. Initial state can be
  /// setup under different circumstances, and the associated events and actions may get triggered
  /// at the inappropriate moment.</remarks>
  protected SimpleStateMachine<LinkState> linkStateMachine;

  /// <summary>
  /// 
  /// </summary>
  [KSPField(isPersistant = true)]
  protected string tgtStrutPartID = "";
  /// <summary>
  /// 
  /// </summary>
  [KSPField(isPersistant = true)]
  protected string tgtStrutVesselID = "";

  /// <summary>
  /// 
  /// </summary>
  [KSPField]
  protected string type = string.Empty;

  // Localizable GUI strings.
  protected static string CannotLinkPartToItselfMsg = "Cannot link part to itself";
  protected static string IncompatibleTargetLinkTypeMsg = "Incompatible target link type";
  protected static string CannotLinkToTheSameVesselMsg = "Cannot link to the same vessel";
  protected static string CannotLinkToTheSamePartMsg = "Cannot link to the same part";
  protected static string SourceIsNotAvailableForLinkMsg = "Source is not available for link";
  protected static string TargetDoesntAcceptLinksMsg = "Target doesn't accept links";

  /// <summary>Initializes the object.</summary>
  /// <remarks>Defines link state tranistion matrix.
  /// <para>Overridden from <see cref="PartModule"/>.</para>
  /// </remarks>
  public override void OnAwake() {
    linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.Linking, LinkState.RejectingLinks});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Linking,
        new[] {LinkState.Available, LinkState.Linked});
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
        enterHandler: x => KASEvents.OnStartLinking.Add(OnStartLinking),
        leaveHandler: x => KASEvents.OnStartLinking.Remove(OnStartLinking));
    linkStateMachine.AddStateHandlers(
        LinkState.RejectingLinks,
        enterHandler: x => KASEvents.OnStopLinking.Add(OnStopLinking),
        leaveHandler: x => KASEvents.OnStopLinking.Remove(OnStopLinking));
    linkStateMachine.AddStateHandlers(
        LinkState.Linking,
        enterHandler: x => KASEvents.OnLinkAccepted.Add(OnLinkActionAccepted),
        leaveHandler: x => KASEvents.OnLinkAccepted.Remove(OnLinkActionAccepted));

    //FIXME
    linkStateMachine.OnDebugStateChange = (from, to) =>
        Debug.LogWarningFormat("SOURCE: Part {0} (id={1}), module {2}: {3}=>{4}",
                               part.name, part.craftID, moduleName, from, to);
  }

  /// <summary>Destroys the object.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  protected virtual void OnDestroy() {
    linkStateMachine.Stop();
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual void StartLinking(GUILinkMode mode) {
    linkState = LinkState.Linking;
    KASEvents.OnStartLinking.Fire(this);
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual void CancelLinking() {
    linkState = LinkState.Available;
    KASEvents.OnStopLinking.Fire(this);
  }

  /// <summary>Creates a logical link between the parts.</summary>
  /// <remarks>Callers should take care of the physical connection between the game
  /// objects.</remarks>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual bool LinkToTarget(ILinkTarget target) {
    //FIXME
    Debug.LogWarningFormat("LinkTargetToTarget: {0}", target);
    if (!CheckCanLinkTo(target)) {
      return false;
    }
    ConnectParts(target);
    LinkParts(target);
    return true;
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual void BreakCurrentLink(bool moveFocusOnTarget = false) {
    //FIXME
    Debug.LogWarning("BreakCurrentLink");
    if (linkState != LinkState.Linked) {
      Debug.LogWarningFormat(
          "Cannot break connection: part {0} is not connected to anything", part.name);
      return;
    }
    DisconnectParts();
    //FIXME: set active vessel as needed
    UnlinkParts();
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual bool CheckCanLinkTo(ILinkTarget linkTarget,
                                     bool reportToGUI = false, bool reportToLog = true) {
    string errorMsg = null;
    if (part == linkTarget.part) {
      errorMsg = CannotLinkPartToItselfMsg;
    } else if (linkType != linkTarget.linkType) {
      errorMsg = IncompatibleTargetLinkTypeMsg;
    } else if (!allowSameVessel && part.vessel == linkTarget.part.vessel) {
      errorMsg = CannotLinkToTheSameVesselMsg;
    } else if (part == linkTarget.part) {
      errorMsg = CannotLinkToTheSamePartMsg;
    } else if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linked)) {
      errorMsg = SourceIsNotAvailableForLinkMsg;
    } else if (linkTarget.linkState != LinkState.AcceptingLinks) {
      errorMsg = TargetDoesntAcceptLinksMsg;
    }
    if (errorMsg != null) {
      if (reportToGUI || reportToLog) {
        Debug.LogWarningFormat("Cannot link {0} (type={1}) and {2} (type={3}): {4}",
                               part.name, linkType,
                               linkTarget.part.name, linkTarget.linkType,
                               errorMsg);
      }
      if (reportToGUI) {
        ScreenMessages.PostScreenMessage(errorMsg, 5f, ScreenMessageStyle.UPPER_CENTER);
      }
      return false;
    }
    return true;
  }

  /// <summary>Initalizes moulde state on part start.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnLoad(ConfigNode node) {
    Debug.LogWarning("*** SOURCE: ON LOAD");
  }

  /// <summary>Initalizes module state on scene start.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnStart(PartModule.StartState state) {
    Debug.LogWarning("*** SOURCE: ON START");
    linkStateMachine.Start(LinkState.Available);
    linkState = linkState;  // Trigger state updates.
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkEventListener"/>.</para>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkInfo info) {
    // Lock this source if another source on the part made the link.
    if (!isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkEventListener"/>.</para>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkInfo info) {
    // Unlock this source if link with another source one the part has broke.
    if (isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = false;
    }
  }

  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  /// <param name="breakForce">Actual force that has been applied.</param>
  protected virtual void OnJointBreak(float breakForce) {
    Debug.LogWarningFormat("Connection between {0} and {1} has broken with force {2}",
                           part.name, linkTarget.part.name, breakForce);
    UnlinkParts();
  }

  /// <summary>Triggers when state has being assigned with a value.</summary>
  /// <remarks>This method triggers even when new state doesn't differ from the old one. When it's
  /// important to catch the transition check for <paramref name="oldState"/>.</remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
  }

  /// <summary>Joins this part and the target into one vessel.</summary>
  /// <param name="target">Target link module.</param>
  void ConnectParts(ILinkTarget target) {
    //FIXME: implement
    Debug.LogWarning("ConnectParts");
  }

  /// <summary>Separates connected parts into two different vessels.</summary>
  void DisconnectParts() {
    //FIXME: implement
    Debug.LogWarning("DisconnectParts");
  }

  /// <summary>Logically links source and target.</summary>
  /// <remarks>No actual connection is created in the game.</remarks>
  /// <param name="target">Target to link with.</param>
  void LinkParts(ILinkTarget target) {
    var linkInfo = new KASEvents.LinkInfo(this, target);
    linkTarget = target;
    linkTarget.linkSource = this;
    linkState = LinkState.Linked;
    KASEvents.OnLinkCreated.Fire(linkInfo);
    SendMessage("OnKASLinkCreatedEvent", linkInfo, SendMessageOptions.DontRequireReceiver);
  }

  /// <summary>Logically unlinks source and the current target.</summary>
  /// <remarks>No actual connection is destroyed in the game.</remarks>
  void UnlinkParts() {
    var linkInfo = new KASEvents.LinkInfo(this, linkTarget);
    linkTarget.linkSource = null;
    linkTarget = null;
    linkState = LinkState.Available;
    KASEvents.OnLinkBroken.Fire(linkInfo);
    SendMessage("OnKASLinkBrokenEvent", linkInfo, SendMessageOptions.DontRequireReceiver);
  }

  /// <summary>Sets rejecting mode when some other source has started connection mode.</summary>
  /// <remarks>Event handler for <see cref="KASEvents.OnStartLinking"/>.</remarks>
  /// <param name="source">Source module that started connecting mode.</param>
  void OnStartLinking(ILinkSource source) {
    if (!ReferenceEquals(this, source)) {
      linkState = LinkState.RejectingLinks;
    }
  }

  /// <summary>Restores available mode when other's source connection mode is over.</summary>
  /// <remarks>Event handler for <see cref="KASEvents.OnStopLinking"/>.</remarks>
  /// <param name="source">Source module that started the mode.</param>
  void OnStopLinking(ILinkSource source) {
    if (!ReferenceEquals(this, source)) {
      linkState = LinkState.Available;
    }
  }

  /// <summary>Establishes a link if target has accepted connection from this source.</summary>
  /// <remarks>Any problems that prevent from a successful creation will be logged to the user. The
  /// accepting party must ensure the link can be done.</remarks>
  /// <param name="target">Target that has accepted connetion.</param>
  void OnLinkActionAccepted(ILinkTarget target) {
    if (CheckCanLinkTo(target, reportToGUI: true)) {
      LinkToTarget(target);
    }
  }
}

}  // namespace
