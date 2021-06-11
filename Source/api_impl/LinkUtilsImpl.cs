// Kerbal Attachment System API
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.LogUtils;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KASImpl {

class LinkUtilsImpl : ILinkUtils {
  /// <inheritdoc/>
  public ILinkPeer FindLinkPeer(ILinkPeer srcPeer) {
    var host = srcPeer as PartModule;
    var tgtPart = FlightGlobals.FindPartByID(srcPeer.linkPartId);
    if (tgtPart == null) {
      HostedDebugLog.Warning(host, "Cannot find target part: partId=F{0}", srcPeer.linkPartId);
      return null;
    }

    // In normal case we can lookup by the node name.
    ILinkPeer tgtPeer = null;
    if (!string.IsNullOrEmpty(srcPeer.linkNodeName)) {
      tgtPeer = tgtPart.Modules
          .OfType<ILinkPeer>()
          .FirstOrDefault(m => m.linkState == LinkState.Linked
                               && m.linkPartId == srcPeer.part.flightID
                               && m.linkNodeName == srcPeer.cfgAttachNodeName
                               && m.cfgLinkType == srcPeer.cfgLinkType);
    }

    // Fallback case. Try guessing the target peer by less strict conditions.
    if (tgtPeer == null) {
      var candidates = tgtPart.Modules
          .OfType<ILinkPeer>()
          .Where(m => m.linkState == LinkState.Linked
                      && m.linkPartId == srcPeer.part.flightID
                      && m.cfgLinkType == srcPeer.cfgLinkType)
          .ToList();
      if (candidates.Count == 1) {
        tgtPeer = candidates[0];
        HostedDebugLog.Warning(host, "FALLBACK: Found a link: {0} => {1}", srcPeer, tgtPeer);
      }
    }

    if (tgtPeer == null) {
      HostedDebugLog.Warning(
          host,
          "Failed to find the link: targetPartId={0}, targetNode={1}",
          srcPeer.linkPartId, srcPeer.linkNodeName);
    }
    return tgtPeer;
  }

  /// <inheritdoc/>
  public Part CoupleParts(AttachNode sourceNode, AttachNode targetNode, bool toDominantVessel = false) {
    if (toDominantVessel) {
      var dominantVessel =
          Vessel.GetDominantVessel(sourceNode.owner.vessel, targetNode.owner.vessel);
      if (dominantVessel != targetNode.owner.vessel) {
        var tmp = sourceNode;
        sourceNode = targetNode;
        targetNode = tmp;
      }
    }
    DebugEx.Fine(
        "Couple {0} to {1}", KASAPI.AttachNodesUtils.NodeId(sourceNode), KASAPI.AttachNodesUtils.NodeId(targetNode));
    var srcPart = sourceNode.owner;
    var srcVessel = srcPart.vessel;
    KASAPI.AttachNodesUtils.AddNode(srcPart, sourceNode);
    var tgtPart = targetNode.owner;
    KASAPI.AttachNodesUtils.AddNode(tgtPart, targetNode);

    sourceNode.attachedPart = tgtPart;
    sourceNode.attachedPartId = tgtPart.flightID;
    targetNode.attachedPart = srcPart;
    targetNode.attachedPartId = srcPart.flightID;
    tgtPart.attachMode = AttachModes.STACK;

    // In KPS 1.11 the landed vessels are anchored to the surface with a constantly renewable fixed joint.
    // When it breaks (and it breaks on every fixed frame), the game recognizes this as a detachment of the connected
    // part from the vessel's root. Once coupled, the source's anchor makes no sense and interferes with the KAS logic.
    if (srcPart.vessel.IsAnchored) {
      DebugEx.Info("Reset RB anchor on vessel: {0}", srcPart.vessel.vesselName);
      srcPart.vessel.ResetRBAnchor();
      // The reset method only requests destroying. The joint still can emit a break event on the next physical frame.
      var rbAnchor = srcPart.vessel.rootPart.GetComponents<FixedJoint>().FirstOrDefault(j => j.connectedBody == null);
      if (rbAnchor != null) {
        Object.DestroyImmediate(rbAnchor);
      } else {
        DebugEx.Error("Cannot find the anchor joint on vessel: {0}", srcPart.vessel.vesselName);
      }
    }

    srcPart.Couple(tgtPart);
    // Depending on how active vessel has updated do either force active or make active. Note, that
    // active vessel can be EVA kerbal, in which case nothing needs to be adjusted.    
    // FYI: This logic was taken from ModuleDockingNode.DockToVessel.
    if (srcVessel == FlightGlobals.ActiveVessel) {
      FlightGlobals.ForceSetActiveVessel(sourceNode.owner.vessel);  // Use actual vessel.
      FlightInputHandler.SetNeutralControls();
    } else if (sourceNode.owner.vessel == FlightGlobals.ActiveVessel) {
      sourceNode.owner.vessel.MakeActive();
      FlightInputHandler.SetNeutralControls();
    }

    return srcPart;
  }

  /// <inheritdoc/>
  public Part DecoupleParts(Part part1, Part part2,
                            DockedVesselInfo vesselInfo1 = null,
                            DockedVesselInfo vesselInfo2 = null) {
    Part partToDecouple;
    DockedVesselInfo vesselInfo;
    if (part1.parent == part2) {
      DebugEx.Fine("Decouple {0} from {1}", part1, part2);
      partToDecouple = part1;
      vesselInfo = vesselInfo1;
    } else if (part2.parent == part1) {
      DebugEx.Fine("Decouple {0} from {1}", part2, part1);
      partToDecouple = part2;
      vesselInfo = vesselInfo2;
    } else {
      DebugEx.Warning("Cannot decouple {0} <=> {1} - not coupled!", part1, part2);
      return null;
    }
    var parentPart = partToDecouple.parent;

    if (partToDecouple.vessel != null && vesselInfo != null) {
      // Simulate the IActivateOnDecouple behaviour since Undock() doesn't do it.
      var srcAttachNode = partToDecouple.FindAttachNodeByPart(partToDecouple.parent);
      if (srcAttachNode != null) {
        srcAttachNode.attachedPart = null;
        partToDecouple.FindModulesImplementing<IActivateOnDecouple>()
            .ForEach(m => m.DecoupleAction(srcAttachNode.id, true));
      }
      if (partToDecouple.parent != null) {
        var tgtAttachNode = partToDecouple.parent.FindAttachNodeByPart(partToDecouple);
        if (tgtAttachNode != null) {
          tgtAttachNode.attachedPart = null;
          partToDecouple.parent.FindModulesImplementing<IActivateOnDecouple>()
              .ForEach(m => m.DecoupleAction(tgtAttachNode.id, false));
        }
      }
      // Decouple and restore the name and hierarchy on the decoupled assembly.
      var vesselInfoCfg = new ConfigNode();
      vesselInfo.Save(vesselInfoCfg);
      DebugEx.Fine("Restore vessel info:\n{0}", vesselInfoCfg);
      partToDecouple.Undock(vesselInfo);
    } else if (partToDecouple.vessel == null) {
      // During the EVA construction mode the parts can have no vessel.
      DebugEx.Fine("EVA construction mode detected. Skip decoupling.");
    } else {
      // Do simple decouple event which will screw the decoupled vessel root part.
      DebugEx.Warning("No vessel info found! Just decoupling");
      partToDecouple.decouple();
    }

    // KSP sometimes fails to restore the physic state of the decoupled parts. Fix it here. No guarantees, though.
    foreach (var rb in partToDecouple.GetComponentsInChildren<Rigidbody>()) {
      if (rb.isKinematic) {
        DebugEx.Warning("KSP ISSUE WORKAROUND: Make rigidbody physical: part={0}, rb={1}",
                        partToDecouple, DbgFormatter.TranformPath(rb.gameObject));
        rb.isKinematic = false;
        rb.velocity = parentPart.rb.velocity;
        rb.angularVelocity = parentPart.rb.angularVelocity;
      }
    }

    if (part1.vessel != null) {
      part1.vessel.CycleAllAutoStrut();
      if (vesselInfo1 != null) {
        part1.vessel.vesselName = vesselInfo1.name;
        part1.vessel.vesselType = vesselInfo1.vesselType;
      }
    }
    if (part2.vessel != null) {
      part2.vessel.CycleAllAutoStrut();
      if (vesselInfo2 != null) {
        part2.vessel.vesselName = vesselInfo2.name;
        part2.vessel.vesselType = vesselInfo2.vesselType;
      }
    }
    return partToDecouple;
  }
}

}  // namespace
