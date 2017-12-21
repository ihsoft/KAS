// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using KSPDev.ProcessingUtils;

namespace Examples {

public class ILinkSourceExample1  {

  #region StateModel
  // Sets up a sample state machine for the source states.
  public static void SetupSourceStateModel() {
    var linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
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
        enterHandler: x => Debug.Log("Source is available"),
        leaveHandler: x => Debug.Log("Source is NOT available"));
  }
  #endregion

  #region ConnectParts
  // Connects two parts assuming the source and the target parts own exactly one link module. 
  public static bool ConnectParts(Part srcPart, Part tgtPart) {
    var source = srcPart.FindModuleImplementing<ILinkSource>();
    var target = tgtPart.FindModuleImplementing<ILinkTarget>();
    if (source == null || target == null || source.cfgLinkType != target.cfgLinkType) {
      Debug.LogError("Source and target cannot link");
      return false;
    }
    // GUILinkMode.API mode tells the implementation to not execute any user facing effects on the
    // link. See GUILinkMode for more details. 
    if (!source.StartLinking(GUILinkMode.API, LinkActorType.API) || !source.LinkToTarget(target)) {
      // Here we can only fail due to the constraints. E.g. the link mode is not supported, or the
      // joint module doesn't give the green light.
      Debug.LogError("Linking failed");
      source.CancelLinking();
      return false;
    }
    Debug.LogFormat("Established link with part: id={0}, mode={1}",
                    source.linkTargetPartId, source.cfgLinkMode);
    return true;
  }
  #endregion

  #region ConnectPartsWithCheck
  // Connects two parts assuming the source and the target parts own exactly one link module.
  // Does not attempt the link if it's obstructed to avoid a GUI error message.
  public static bool ConnectPartsWithCheck(Part srcPart, Part tgtPart) {
    var source = srcPart.FindModuleImplementing<ILinkSource>();
    var target = tgtPart.FindModuleImplementing<ILinkTarget>();
    if (source == null || target == null || source.cfgLinkType != target.cfgLinkType) {
      Debug.LogError("Source and target cannot link");
      return false;
    }
    if (!source.CheckCanLinkTo(target, reportToLog: false)) {
      Debug.Log("Link is obstructed. Silently cancel the action");
      return false;
    }
    if (!source.StartLinking(GUILinkMode.API, LinkActorType.API) || !source.LinkToTarget(target)) {
      Debug.LogError("Linking failed");
      source.CancelLinking();
      return false;
    }
    return true;
  }
  #endregion

  #region ConnectNodes
  // Connects two parts using the link type and the attach node names to find the right link's
  // source and target.
  public static bool ConnectNodes(string linkType,
                                  Part srcPart, string srcNodeName,
                                  Part tgtPart, string tgtNodeName) {
    var source = srcPart.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(s => s.cfgAttachNodeName == srcNodeName && s.cfgLinkType == linkType);
    var target = tgtPart.FindModulesImplementing<ILinkTarget>()
        .FirstOrDefault(t => t.cfgAttachNodeName == tgtNodeName && t.cfgLinkType == linkType);
    if (source == null || target == null) {
      Debug.LogError("Cannot link the nodes");
      return false;
    }
    // GUILinkMode.API mode tells the implementation to not execute any user facing effects on the
    // link. See GUILinkMode for more details. 
    if (!source.StartLinking(GUILinkMode.API, LinkActorType.API) || !source.LinkToTarget(target)) {
      Debug.LogError("Linking failed");
      source.CancelLinking();
      return false;
    }
    return true;
  }
  #endregion

  #region DisconnectParts
  // Disconnects the source part from its target. Only once source can be connected on the part.
  // And it can be connected to the exactly one target.
  public static void DisconnectParts(Part srcPart) {
    var source = srcPart.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(s => s.linkTarget != null);
    if (source == null) {
      Debug.LogWarningFormat("Part is not connected to anything");
      return;
    }
    // LinkActorType.API tells the implementation to not execute any user facing effects on the
    // link. See LinkActorType for more details.
    source.BreakCurrentLink(LinkActorType.API);
  }
  #endregion

  #region FindTargetFromSource
  // Returns the linked part of the source, if any. It assumes there is exactly one source module
  // on the source part.
  public static Part FindTargetFromSource(Part srcPart) {
    var source = srcPart.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(s => s.linkTarget != null);
    if (source == null) {
      Debug.Log("Source is not connected");
      return null;
    }
    return source.linkTarget.part;
  }
  #endregion

  #region FindSourceByAttachNode
  // Returns a source module given an attach node name.
  public static ILinkSource FindSourceByAttachNode(Part srcPart, string srcNodeName) {
    return srcPart.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(s => s.cfgAttachNodeName == srcNodeName);
  }
  #endregion

  #region CheckIfConnected
  // Checks if the two parts are connected via a KAS link. 
  public static bool CheckIfConnected(Part srcPart, Part tgtPart) {
    return srcPart.FindModulesImplementing<ILinkSource>()
        .Any(s => s.linkTarget != null && s.linkTarget.part == tgtPart);
  }
  #endregion

  #region CheckIfSourceCanConnect
  // Checks if a source of the specified type on the part can possible establish a link.  
  public static bool CheckIfSourceCanConnect(Part srcPart, string linkType) {
    return srcPart.FindModulesImplementing<ILinkSource>()
        .Any(s => s.cfgLinkType == linkType && s.linkState == LinkState.Available);
  }
  #endregion

  readonly Part part = null;

  #region HighlightLocked
  // Highlights the part in RED if it could potentionally link with the linking source, but
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

  #region StartRenderer
  // Starts a renderer between the source and target.
  public static void StartRendrer(Part part, ILinkSource source, ILinkTarget target) {
    // It's a good idea to pre-cache this module in OnStart() method.
    var renderer = part.FindModulesImplementing<ILinkRenderer>()
        .FirstOrDefault(r => r.cfgRendererName == "MyRendererName");
    if (renderer == null) {
      Debug.LogError("Ops! No renderer found");
      return;
    }
    renderer.StartRenderer(source.nodeTransform, target.nodeTransform);
  }
  #endregion

  #region FindSourceAtAttachNode
  // Finds the KAS source at the specified part's attach node.
  public static ILinkSource FindSourceAtAttachNode(AttachNode an) {
    return an.owner.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(s => s.cfgAttachNodeName == an.id);
  }
  #endregion

  #region FindTargetAtAttachNode
  // Finds the KAS target connected to the specified part's attach node.
  public static ILinkTarget FindTargetAtAttachNode(AttachNode an) {
    var otherAn = an.FindOpposingNode();
    if (otherAn == null) {
      Debug.LogError("Attach node is not connected");
      return null;
    }
    return otherAn.owner.FindModulesImplementing<ILinkTarget>()
        .FirstOrDefault(t => t.cfgAttachNodeName == otherAn.id);
  }
  #endregion
}

public abstract class ILinkSourceExample_SampleImplementation : MonoBehaviour, ILinkSource {
  #region ILinkSourceExample_linkRenderer
  public ILinkRenderer linkRenderer { get; private set; }

  [KSPField]
  public string rendererName = "";

  void InitRenderer() {
    linkRenderer = part.FindModulesImplementing<ILinkRenderer>()
        .FirstOrDefault(r => r.cfgRendererName == rendererName);
  }
  #endregion

  public abstract bool StartLinking(GUILinkMode mode, LinkActorType actor);
  public abstract void CancelLinking();
  public abstract bool LinkToTarget(ILinkTarget target);
  public abstract void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false);
  public abstract bool CheckCanLinkTo(
      ILinkTarget target, bool reportToGUI = false, bool reportToLog = true);
  public Part part { get; private set; }
  public string cfgLinkType { get; private set; }
  public LinkMode cfgLinkMode { get; private set; }
  public string cfgAttachNodeName { get; private set; }
  public Transform nodeTransform { get; private set; }
  public Transform physicalAnchorTransform { get; private set; }
  public ILinkTarget linkTarget { get; private set; }
  public uint linkTargetPartId { get; private set; }
  public LinkState linkState { get; private set; }
  public bool isLocked { get; set; }
  public bool isLinked { get; private set; }
  public GUILinkMode guiLinkMode { get; private set; }
  public LinkActorType linkActor { get; private set; }
  public Vector3 targetPhysicalAnchor { get; private set; }
  public ILinkJoint linkJoint { get; private set; }
}

#region ILinkSourceExample_BreakFromPhysyicalMethod
public class ILinkSourceExample_BreakFromPhysyicalMethod : MonoBehaviour {
  public ILinkSource linkSource;

  // This method is called by Unity core during the physics update.
  IEnumerable OnJointBreak(float force) {
    Debug.LogWarningFormat("Link is broken with force: {0}", force);
    // Don't break the link from the physics methods! 
    yield return new WaitForEndOfFrame();
    // Now it's safe to change the physical objects.
    if (linkSource != null && linkSource.linkTarget != null) {
      linkSource.BreakCurrentLink(LinkActorType.Physics);
    }
  }
}
#endregion

};  // namespace
