// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using KSPDev.Processing;
using KASAPIv1;

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
public class KASModuleLinkSourceBase : PartModule, ILinkSource, ILinkStateEventListener,
                                       IActivateOnDecouple {
  #region ILinkSource config properties implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public string cfgLinkType { get { return type; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public string cfgAttachNodeName { get { return attachNodeName; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public bool cfgAllowSameVesselTarget { get { return allowSameVesselTarget; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public string cfgTubeRendererName { get { return linkRendererName; } }
//  /// <inheritdoc/>
//  /// <para>Implements <see cref="ILinkSource"/>.</para>
//  public float cfgMaxLength { get { return maxLength; } }
//  /// <inheritdoc/>
//  /// <para>Implements <see cref="ILinkSource"/>.</para>
//  public float cfgMaxAngle { get { return maxAngle; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public bool cfgAllowOtherVesselTarget { get { return allowOtherVesselTarget; } }
  #endregion

  #region ILinkSource properties implementation
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
      persistedLinkState = value;
      OnStateChange(oldState);
    }
  }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual bool isLocked {
    get { return linkState == LinkState.Locked; }
    set { linkState = value ? LinkState.Locked : LinkState.Available; }
  }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public Transform nodeTransform {
    get {
      if (_nodeTransform == null) {
        _nodeTransform = KASAPI.AttachNodesUtils.GetOrCreateNodeTransform(part, attachNodeName);
      }
      return _nodeTransform;
    }
  }
  Transform _nodeTransform;
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public AttachNode attachNode {
    get {
      if (_attachNode == null) {
        var node = KASAPI.AttachNodesUtils.GetAttachNode(part, attachNodeName);
        if (node == null) {
          throw new InvalidOperationException(string.Format(
              "Cannot find attach nodes in the part: {0} (node={1})", part.name, attachNodeName));
        }
        if (node.nodeType != AttachNode.NodeType.Stack) {
          throw new InvalidOperationException(string.Format(
              "Attach node must be of type 'stack': {0} (type={1})", part.name, node.nodeType));
        }
        _attachNode = node;
      }
      return _attachNode;
    }
  }
  AttachNode _attachNode;
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public GUILinkMode guiLinkMode { get; private set; }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public ILinkTubeRenderer linkRenderer {
    get {
      if (_tubeRenderer == null && cfgTubeRendererName != "") {
        _tubeRenderer = part.FindModulesImplementing<ILinkTubeRenderer>()
            .FirstOrDefault(x => x.cfgRendererName == cfgTubeRendererName);
      }
      return _tubeRenderer;
    }
  }
  ILinkTubeRenderer _tubeRenderer;
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Persistent fields
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public string type = "";
  [KSPField]
  public string linkRendererName = "";
  [KSPField]
  public string attachNodeName = "";
  [KSPField]
  public bool allowSameVesselTarget;
  [KSPField]
  public bool allowOtherVesselTarget;
  #endregion

  /// <summary>State machine that controls event reaction in different states.</summary>
  /// <remarks>Primary usage of the machine is managing subscriptions to the different game events. 
  /// It's highly discouraged to use it for firing events or taking actions. Initial state can be
  /// setup under different circumstances, and the associated events and actions may get triggered
  /// at the inappropriate moment.</remarks>
  protected SimpleStateMachine<LinkState> linkStateMachine { get; private set; }

  /// <summary>Joint module that manages source &lt;=&gt; target physical connection.</summary>
  protected ILinkJoint linkJoint { get; private set; }

  #region Localizable GUI strings
  protected static string CannotLinkPartToItselfMsg = "Cannot link part to itself";
  protected static string IncompatibleTargetLinkTypeMsg = "Incompatible target link type";
  protected static string CannotLinkToTheSameVesselMsg = "Cannot link to the same vessel";
  protected static string CannotLinkToTheSamePartMsg = "Cannot link to the same part";
  protected static string SourceIsNotAvailableForLinkMsg = "Source is not available for link";
  protected static string TargetDoesntAcceptLinksMsg = "Target doesn't accept links";
  protected static string LengthLimitReachedMsg = "Link length limit reached: {0:F2}m > {1:F2}m";
  protected static string TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0}deg > {1:F0}deg";
  protected static string SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0}deg > {1:F0}deg";
  //FIXME
  protected static string LinkingStatusTextMsg = "Link length {0:F2}m, mass {1:F0}kg";
  #endregion

  #region PartModule overrides
  /// <summary>Initializes the object.</summary>
  /// <remarks>Defines link state tranistion matrix.
  /// <para>Overridden from <see cref="PartModule"/>.</para>
  /// </remarks>
  public override void OnAwake() {
    base.OnAwake();
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
//    linkStateMachine.OnDebugStateChange = (from, to) =>
//        Debug.LogWarningFormat("SOURCE: Part {0} (id={1}), module {2}: {3}=>{4}",
//                               part.name, part.craftID, moduleName, from, to);
  }

  /// <summary>Initalizes module state on scene start.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    linkJoint = part.FindModuleImplementing<ILinkJoint>();
    if (persistedLinkState == LinkState.Linked && attachNode.attachedPart != null) {
      var target = FindLinkedTarget();
      if (target != null) {
        //FIXME
        Debug.LogWarning("fire on link load");
        OnLinkLoaded(target);
        
        //FIXME drop
        Debug.LogWarning("OnStart");
      }
    }

    //FIXME
    Debug.LogWarningFormat("START: Attached part id={0}, part={1}",
                           attachNode.attachedPartId, attachNode.attachedPart);

    linkStateMachine.Start(persistedLinkState);
    linkState = linkState;  // Trigger state updates.
  }

  public virtual void OnPartPack() {
    Debug.LogWarning("OnPartPack");
  }

  // Set an insane force that will be overriden anyways. This is how we detect it's time to
  // adjust the joint.
  const float TempBreakingForceForDetection = 777000;

  public virtual void OnPartUnpack() {
    //FIXME: check joint
//    Debug.LogWarning("OnPartUnpack");
    if (linkState == LinkState.Linked && linkTarget == null) {
      // Restore the link on part load.
      var target = FindLinkedTarget();
      if (target != null) {
        linkTarget = target;
        StartCoroutine(WaitAndFireOnSetupJoint());
      } else {
        Debug.LogErrorFormat("Cannot restore link between parts: {0} (id={1}) and {2} (id={3})",
                             part.name, part.flightID,
                             attachNode.attachedPart.name, attachNode.attachedPart.flightID);
        linkState = LinkState.Available;
      }
    }
  }
  #endregion

  #region ILinkSource implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual bool StartLinking(GUILinkMode mode) {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linking)) {
      Debug.LogWarningFormat("Cannot start linking mode in state: {0}", linkState);
      return false;
    }
    if (mode == GUILinkMode.Eva && !FlightGlobals.ActiveVessel.isEVA) {
      Debug.LogWarning("Cannot start EVA linking mode since active vessel is not EVA");
      return false;
    }
    linkState = LinkState.Linking;
    StartLinkGUIMode(mode);
    return true;
  }
  
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual void CancelLinking() {
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Available)) {
      Debug.LogWarningFormat("Cannot stop linking mode in state: {0}", linkState);
      return;
    }
    StopLinkGUIMode();
    linkState = LinkState.Available;
  }

  //FIXME: correct description, it does make physical connection
  /// <summary>Creates a logical link between the parts.</summary>
  /// <remarks>Callers should take care of the physical connection between the game
  /// objects.</remarks>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  /// <returns><c>true</c> if link has been successfully established. Errors, if any, are reported
  /// to the logs only.</returns>
  public virtual bool LinkToTarget(ILinkTarget target) {
    if (!CheckCanLinkTo(target)) {
      return false;
    }
    ConnectParts(target);
    LinkParts(target);
    StopLinkGUIMode();  // FIXME: palce first or add a comment why not
    return true;
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual void BreakCurrentLink(bool moveFocusOnTarget = false) {
    if (linkState != LinkState.Linked) {
      Debug.LogWarningFormat(
          "Cannot break connection: part {0} is not connected to anything", part.name);
      return;
    }
    var target = linkTarget;
    UnlinkParts();
    var targetVessel = DisconnectTargetPart(target);
    if (moveFocusOnTarget && FlightGlobals.ActiveVessel == vessel) {
      FlightGlobals.ForceSetActiveVessel(targetVessel);
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkSource"/>.</para>
  public virtual bool CheckCanLinkTo(
      ILinkTarget target, bool reportToGUI = false, bool reportToLog = true) {
    string errorMsg =
        CheckBasicLinkConditions(target) ?? CheckJointLimits(target.nodeTransform);
    if (errorMsg != null) {
      if (reportToGUI || reportToLog) {
        Debug.LogWarningFormat("Cannot link {0} (type={1}) and {2} (type={3}): {4}",
                               part.name, cfgLinkType, target.part.name, target.cfgLinkType, errorMsg);
      }
      if (reportToGUI) {
        ScreenMessages.PostScreenMessage(errorMsg, 5f, ScreenMessageStyle.UPPER_CENTER);
      }
    }
    return errorMsg == null;
  }
  #endregion

  #region ILinkStateEventListener implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkStateEventListener"/>.</para>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    // Lock this source if another source on the part made the link.
    if (!isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkStateEventListener"/>.</para>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    // Unlock this source if link with another source one the part has broke.
    if (isLocked && !ReferenceEquals(info.source, this)) {
      isLocked = false;
    }
  }
  #endregion

  #region IActivateOnDecouple implementation
  //FIXME: doc
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    //FIXME
    Debug.LogWarningFormat("SOURCE: ** DecoupleAction: {0} (id={3}, weDecouple={1}, linkState={2}",
                           nodeName, weDecouple, linkState, part.flightID);
    if (linkState == LinkState.Linked) {
      Debug.LogWarningFormat("Connection between {0} and {1} has broken externally!",
                             part.name, linkTarget.part.name);
      UnlinkParts(isBrokenExternally: true);
    }
    //FIXME: fire forced unlink event
    //FIXME: unlink before diconnecting
  }
  #endregion

  // FIXME: Handle KIS actions.
  // FIXME: Handle part destroyed action.
  // FIXME: Handle part staged action.

  #region New inheritable methods
  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  /// <param name="breakForce">Actual force that has been applied.</param>
  /// FIXME: check if can be deprecated in favor of DecoupleAction
  protected virtual void OnJointBreak(float breakForce) {
    Debug.LogWarningFormat("Connection between {0} and {1} has broken with force {2}",
                           part.name, linkTarget.part.name, breakForce);
    //UnlinkParts(isBrokenExternally: true);
  }

  /// <summary>Destroys the object.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  protected virtual void OnDestroy() {
    linkStateMachine.Stop();
  }

  /// <summary>Triggers when state has been assigned with a value.</summary>
  /// <remarks>This method triggers even when new state doesn't differ from the old one. When it's
  /// important to catch the transition check for <paramref name="oldState"/>.</remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
    if (linkRenderer != null && oldState != linkState) {
      if (linkState == LinkState.Linked) {
        linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
      } else if (oldState == LinkState.Linked) {
        linkRenderer.StopRenderer();
      }
    }
  }

  /// <summary>Triggers when vessel has loaded and physics is about to start.</summary>
  /// <remarks>This event is called when all normal loading stuff is already done. I.e. for the
  /// docked links it means the part's joint is restored to default. Override the event to adjust
  /// joint settings, e.g. to set the proper linear and rotating force limits.
  /// <para>Note, that since this event is a part of loading process normal state change events are
  /// not expected to trigger. Descendants must not expect the link/unlink events to fire during the
  /// loading.</para>
  /// </remarks>
  /// <param name="target">Linked part target module.</param>
  /// FIXME: nope, unpack is not good for restore. use onload
  protected virtual void OnLinkLoaded(ILinkTarget target) {
    if (linkRenderer != null) {
      linkRenderer.StartRenderer(nodeTransform, target.nodeTransform);
    }
  }

  //FIXME: figure out how to know who's the host of the joint
  /// <summary>Triggers when a physical joint between the two parts is created.</summary>
  protected virtual void OnSetupJoint() {
    if (linkJoint != null) {
      linkJoint.SetupJoint(this, linkTarget);
    }
  }

  /// <summary>Triggers when physical joint between the two parts is destroyed.</summary>
  protected virtual void OnCleanupJoint() {
    if (linkJoint != null) {
      linkJoint.CleanupJoint();
    }
  }

  /// <summary>Initiates GUI mode, and starts displaying linking process.</summary>
  /// <remarks>Displays current mode via <see cref="KASModuleTubeRenderer"/> if one assigned to the
  /// part. Mode <see cref="GUILinkMode.API"/> is not reflected in GUI.</remarks>
  /// <param name="mode">Mode to start with.</param>
  /// FIXME: return bool
  protected virtual void StartLinkGUIMode(GUILinkMode mode) {
    guiLinkMode = mode;
    KASEvents.OnStartLinking.Fire(this);
  }

  /// <summary>Stops any pending GUI mode that displays linking process.</summary>
  protected virtual void StopLinkGUIMode() {
    KASEvents.OnStopLinking.Fire(this);
    guiLinkMode = GUILinkMode.None;
  }

  /// <summary>Joins this part and the target into one vessel.</summary>
  /// <param name="target">Target link module.</param>
  protected virtual bool ConnectParts(ILinkTarget target) {
    //FIXME: implement
    Debug.LogWarning("ConnectParts");
    if (part.vessel == target.part.vessel) {
      //FIXME
      Debug.LogWarning("Already docked skipping");
      return false;
    }
    //FIXME: move to CheckCanLink
    Debug.LogWarningFormat("Docking {0} to {1}", part.vessel, target.part.vessel);
    if (attachNode.attachedPart != null || target.attachNode.attachedPart != null) {
      throw new InvalidOperationException(string.Format(
          "Both attach nodes must be free: {0} (attached={1}) => {2} (attached={3})",
          part.name, attachNode.attachedPart, target.part.name, target.attachNode.attachedPart));
    }
    // FIXME: fromn here move to KIS/KAS common
    GameEvents.onActiveJointNeedUpdate.Fire(part.vessel);
    GameEvents.onActiveJointNeedUpdate.Fire(target.part.vessel);
    attachNode.attachedPart = target.part;
    target.attachNode.attachedPart = part;
    //FIXME: looks unneeded
    part.attachMode = AttachModes.STACK;
    part.Couple(target.part);
    StartCoroutine(WaitAndFireOnSetupJoint());
    return true;
  }

  /// <summary>Separates connected parts into two different vessels.</summary>
  /// <param name="target">Target to disconnect from.</param>
  /// <returns>Vessel created from the target part or <c>null</c> if no decoupling happen.</returns>
  protected virtual Vessel DisconnectTargetPart(ILinkTarget target) {
    if (part.parent == target.part) {
      //FIXME
      Debug.LogWarning("Detach src from target");
      part.decouple();
      //FIXME: restore source vessel info
    } else if (target.part.parent == part) {
      //FIXME
      Debug.LogWarning("Detach trg from source");
      target.part.decouple();
      //FIXME: restore target vessel info
    } else {
      Debug.LogWarningFormat("Source {0} (id={1}) is not linked with target {2} (id={3})",
                             part.name, part.flightID, target.part.name, target.part.flightID);
      return null;
    }
    OnCleanupJoint();
    return target.part.vessel;
  }

  /// <summary>Logically links source and target.</summary>
  /// <remarks>No actual connection is created in the game.</remarks>
  /// <param name="target">Target to link with.</param>
  protected virtual void LinkParts(ILinkTarget target) {
    var linkInfo = new KASEvents.LinkEvent(this, target);
    linkTarget = target;
    linkTarget.linkSource = this;
    linkState = LinkState.Linked;
    KASEvents.OnLinkCreated.Fire(linkInfo);
    SendMessage(KASEvents.LinkCreatedEventName, linkInfo, SendMessageOptions.DontRequireReceiver);
  }

  /// <summary>Logically unlinks source and the current target.</summary>
  /// <remarks>No actual connection is destroyed in the game.</remarks>
  /// <param name="isBrokenExternally">If <c>true</c> then link has been broken not by the source.
  /// It's usually physics engine (breaking force exceeded) or a parts manipulation mod (like KIS).
  /// </param>
  protected virtual void UnlinkParts(bool isBrokenExternally = false) {
    var linkInfo = new KASEvents.LinkEvent(this, linkTarget);
    linkTarget.linkSource = null;
    linkTarget = null;
    linkState = LinkState.Available;
    KASEvents.OnLinkBroken.Fire(linkInfo);
    SendMessage(KASEvents.LinkBrokenEventName, linkInfo, SendMessageOptions.DontRequireReceiver);
  }
  #endregion

  #region New utility methods
  /// <summary>Checks if basic source and target states allows linking.</summary>
  /// <param name="target">Target of the pipe to check link with.</param>
  /// <returns>An error message if link cannot be established or <c>null</c> otherwise.</returns>
  protected string CheckBasicLinkConditions(ILinkTarget target) {
    if (part == target.part) {
      return CannotLinkPartToItselfMsg;
    }
    if (cfgLinkType != target.cfgLinkType) {
      return IncompatibleTargetLinkTypeMsg;
    }
    if (!allowSameVesselTarget && part.vessel == target.part.vessel) {
      return CannotLinkToTheSameVesselMsg;
    }
    if (part == target.part) {
      return CannotLinkToTheSamePartMsg;
    }
    if (!linkStateMachine.CheckCanSwitchTo(LinkState.Linked)) {
      return SourceIsNotAvailableForLinkMsg;
    }
    if (target.linkState != LinkState.AcceptingLinks) {
      return TargetDoesntAcceptLinksMsg;
    }
    return null;
  }

  protected string CheckJointLimits(Transform targetTransform) {
    if (linkJoint == null) {
      return null;
    }
    return
        linkJoint.CheckLengthLimit(this, targetTransform)
        ?? linkJoint.CheckAngleLimitAtSource(this, targetTransform)
        ?? linkJoint.CheckAngleLimitAtTarget(this, targetTransform);
  }
  #endregion

  #region KASEvents listeners
  /// <summary>Sets rejecting state when some other source has started connection mode.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.Available"/>.
  /// <para>Event handler for <see cref="KASEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started connecting mode.</param>
  void OnStartLinking(ILinkSource source) {
    linkState = LinkState.RejectingLinks;
  }

  /// <summary>Restores available state when connection mode is over.</summary>
  /// <remarks>It's only listened in state <see cref="LinkState.RejectingLinks"/>.  
  /// <para>Event handler for <see cref="KASEvents.OnStopLinking"/>.</para>
  /// </remarks>
  /// <param name="source">Source module that started the mode.</param>
  void OnStopLinking(ILinkSource source) {
    linkState = LinkState.Available;
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
  #endregion 

  /// <summary>Finds this source link target.</summary>
  /// <returns>Target or <c>null</c> if nothing found.</returns>
  ILinkTarget FindLinkedTarget() {
    return attachNode.attachedPart.FindModulesImplementing<ILinkTarget>()
        .FirstOrDefault(x => x.linkState == LinkState.Linked && x.cfgLinkType == cfgLinkType);
  }

  /// <summary>
  /// Detects moment when KSP core initializes the part' joint, and fires KAS event. 
  /// </summary>
  /// <returns><c>null</c> until the condition is met.</returns>
  IEnumerator WaitAndFireOnSetupJoint() {
    // Set an arbitary breaking force. The moment of KSP joint initialization is when this value
    // get overwritten. It's not important what exact value is set for this purpose since all this
    // stuff happens before physics start.
    part.attachJoint.Joint.breakForce = 123456;
    while (Mathf.Approximately(part.attachJoint.Joint.breakForce, 123456)) {
      //FIXME
      Debug.LogWarning("Waiting...");
      yield return null;
    }
    //FIXME
    Debug.LogWarning("Setup joint");
    OnSetupJoint();
  }
}

}  // namespace
