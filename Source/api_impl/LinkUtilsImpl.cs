// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.LogUtils;
using System.Linq;

namespace KASImpl {

class LinkUtilsImpl : ILinkUtils {
  /// <inheritdoc/>
  public ILinkPeer FindLinkPeer(ILinkPeer srcPeer) {
    if (srcPeer.linkPartId == 0 || srcPeer.linkModuleIndex == -1) {
      DebugEx.Error("Bad target part definition [Part:(id=F{0}#Module:{1}]",
                    srcPeer.linkPartId, srcPeer.linkModuleIndex);
      return null;
    }
    var tgtPart = FlightGlobals.FindPartByID(srcPeer.linkPartId);
    if (tgtPart == null) {
      DebugEx.Error("Cannot find [Part:(id=F{0})]", srcPeer.linkPartId);
      return null;
    }
    if (srcPeer.linkModuleIndex >= tgtPart.Modules.Count) {
      DebugEx.Error("The target part {0} doesn't have a module at index {1}",
                    tgtPart, srcPeer.linkModuleIndex);
      return null;
    }
    var tgtPeer = tgtPart.Modules[srcPeer.linkModuleIndex] as ILinkPeer;
    if (tgtPeer == null) {
      DebugEx.Error("The target module {0} is not a link peer",
                    tgtPart.Modules[srcPeer.linkModuleIndex]);
      return null;
    }
    if (!tgtPeer.isLinked || tgtPeer.linkPartId != srcPeer.part.flightID
        || tgtPeer.linkModuleIndex != srcPeer.part.Modules.IndexOf(srcPeer as PartModule)) {
      DebugEx.Error("Source module {0} cannot be linked with the target module {1}",
                    srcPeer.part.Modules[tgtPeer.linkModuleIndex],
                    tgtPart.Modules[srcPeer.linkModuleIndex]);
      return null;
    }
    return tgtPeer;
  }

  /// <inheritdoc/>
  public Part CoupleParts(AttachNode sourceNode, AttachNode targetNode,
                          bool toDominantVessel = false) {
    if (toDominantVessel) {
      var dominantVessel =
          Vessel.GetDominantVessel(sourceNode.owner.vessel, targetNode.owner.vessel);
      if (dominantVessel != targetNode.owner.vessel) {
        var tmp = sourceNode;
        sourceNode = targetNode;
        targetNode = tmp;
      }
    }
    DebugEx.Fine("Couple {0} to {1}",
                 KASAPI.AttachNodesUtils.NodeId(sourceNode),
                 KASAPI.AttachNodesUtils.NodeId(targetNode));
    var srcPart = sourceNode.owner;
    var srcVessel = srcPart.vessel;
    KASAPI.AttachNodesUtils.AddNode(srcPart, sourceNode);
    var tgtPart = targetNode.owner;
    var tgtVessel = tgtPart.vessel;
    KASAPI.AttachNodesUtils.AddNode(tgtPart, targetNode);

    sourceNode.attachedPart = tgtPart;
    sourceNode.attachedPartId = tgtPart.flightID;
    targetNode.attachedPart = srcPart;
    targetNode.attachedPartId = srcPart.flightID;
    tgtPart.attachMode = AttachModes.STACK;
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

    if (vesselInfo != null) {
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
    } else {
      // Do simple decouple event which will screw the decoupled vessel root part.
      DebugEx.Warning("No vessel info found! Just decoupling");
      partToDecouple.decouple();
    }
    part1.vessel.CycleAllAutoStrut();
    part2.vessel.CycleAllAutoStrut();
    return partToDecouple;
  }
}

}  // namespace
