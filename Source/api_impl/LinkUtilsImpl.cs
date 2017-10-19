// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.LogUtils;
using System;
using System.Linq;

namespace KASImpl {

class LinkUtilsImpl : ILinkUtils {
  /// <inheritdoc/>
  public ILinkTarget FindLinkTargetFromSource(ILinkSource source) {
    if (source.linkTargetPartId > 0) {
      var targetPart = FlightGlobals.FindPartByID(source.linkTargetPartId);
      return targetPart.FindModulesImplementing<ILinkTarget>().FirstOrDefault(
          t => t.linkState == LinkState.Linked && t.linkSourcePartId == source.part.flightID);
    }
    return null;
  }

  /// <inheritdoc/>
  public ILinkSource FindLinkSourceFromTarget(ILinkTarget target) {
    if (target.linkSourcePartId > 0) {
      var sourcePart = FlightGlobals.FindPartByID(target.linkSourcePartId);
      return sourcePart.FindModulesImplementing<ILinkSource>().FirstOrDefault(
          s => s.linkState == LinkState.Linked && s.linkTargetPartId == target.part.flightID);
    }
    return null;
  }

  /// <inheritdoc/>
  public DockedVesselInfo CoupleParts(AttachNode sourceNode, AttachNode targetNode,
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
    var srcPart = sourceNode.owner;
    var srcVessel = srcPart.vessel;
    var tgtPart = targetNode.owner;
    var tgtVessel = tgtPart.vessel;
    DebugEx.Fine("Couple {0} to {1}", srcPart, tgtPart);

    var vesselInfo = new DockedVesselInfo();
    vesselInfo.name = srcVessel.vesselName;
    vesselInfo.vesselType = srcVessel.vesselType;
    vesselInfo.rootPartUId = srcVessel.rootPart.flightID;

    sourceNode.attachedPart = tgtPart;
    sourceNode.attachedPartId = tgtPart.flightID;
    targetNode.attachedPart = srcPart;
    targetNode.attachedPartId = srcPart.flightID;
    srcPart.attachMode = AttachModes.STACK;  // All KAS links are expected to be STACK.
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

    return vesselInfo;
  }

  /// <inheritdoc/>
  public Part DecoupleParts(Part part1, Part part2) {
    Part decoupledPart;
    if (part1.parent == part2) {
      DebugEx.Fine("Decouple {0} from {1}", part1, part2);
      part1.decouple();
      decoupledPart = part1;
    } else if (part2.parent == part1) {
      DebugEx.Fine("Decouple {0} from {1}", part2, part1);
      part2.decouple();
      decoupledPart = part2;
    } else {
      DebugEx.Warning("Cannot decouple {0} <=> {1} - not coupled!", part1, part2);
      return null;
    }
    part1.vessel.CycleAllAutoStrut();
    part2.vessel.CycleAllAutoStrut();
    return decoupledPart;
  }
}

}  // namespace
