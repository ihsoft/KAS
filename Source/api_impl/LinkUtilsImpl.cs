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
          t => t.isLinked && t.linkSourcePartId == source.part.flightID);
    }
    return null;
  }

  /// <inheritdoc/>
  public ILinkSource FindLinkSourceFromTarget(ILinkTarget target) {
    if (target.linkSourcePartId > 0) {
      var sourcePart = FlightGlobals.FindPartByID(target.linkSourcePartId);
      return sourcePart.FindModulesImplementing<ILinkSource>().FirstOrDefault(
          s => s.isLinked && s.linkTargetPartId == target.part.flightID);
    }
    return null;
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
    var srcPart = sourceNode.owner;
    var srcVessel = srcPart.vessel;
    var tgtPart = targetNode.owner;
    var tgtVessel = tgtPart.vessel;
    DebugEx.Fine("Couple {0} to {1}",
                 KASAPI.AttachNodesUtils.DumpAttachNode(sourceNode),
                 KASAPI.AttachNodesUtils.DumpAttachNode(targetNode));

    UpdateVesselInfoOnPart(srcPart);
    UpdateVesselInfoOnPart(tgtPart);

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

    return srcPart;
  }

  /// <inheritdoc/>
  public Part DecoupleParts(Part part1, Part part2) {
    Part partToDecouple;
    if (part1.parent == part2) {
      DebugEx.Fine("Decouple {0} from {1}", part1, part2);
      partToDecouple = part1;
    } else if (part2.parent == part1) {
      DebugEx.Fine("Decouple {0} from {1}", part2, part1);
      partToDecouple = part2;
    } else {
      DebugEx.Warning("Cannot decouple {0} <=> {1} - not coupled!", part1, part2);
      return null;
    }

    var parentVesselInfoModule = partToDecouple.parent.FindModuleImplementing<ILinkVesselInfo>();
    if (parentVesselInfoModule != null) {
      parentVesselInfoModule.vesselInfo = null;
    }
    var childVesselInfoModule = partToDecouple.FindModuleImplementing<ILinkVesselInfo>();
    if (childVesselInfoModule != null && childVesselInfoModule.vesselInfo != null) {
      // Simulate the IActivateOnDecouple behaviour since Undock() doesn't do it.
      var srcAttachNode = partToDecouple.FindAttachNodeByPart(partToDecouple.parent);
      if (srcAttachNode != null) {
        srcAttachNode.attachedPart = null;
        partToDecouple.FindModulesImplementing<IActivateOnDecouple>()
            .ForEach(m => m.DecoupleAction(srcAttachNode.id, true));
      }
      var tgtAttachNode = partToDecouple.parent.FindAttachNodeByPart(partToDecouple);
      if (tgtAttachNode != null) {
        tgtAttachNode.attachedPart = null;
        partToDecouple.parent.FindModulesImplementing<IActivateOnDecouple>()
            .ForEach(m => m.DecoupleAction(tgtAttachNode.id, false));
      }
      // Decouple and restore the name and hierarchy on the decoupled assembly.
      partToDecouple.Undock(childVesselInfoModule.vesselInfo);
      childVesselInfoModule.vesselInfo = null;
    } else {
      // Do simple decouple event which will screw the decoupled vessel root part.
      partToDecouple.decouple();
    }
    part1.vessel.CycleAllAutoStrut();
    part2.vessel.CycleAllAutoStrut();
    return partToDecouple;
  }

  #region Local utility methods
  /// <summary>Updates the vessel info on the part if it has the relevant module.</summary>
  /// <param name="part">The part to search for the module on.</param>
  void UpdateVesselInfoOnPart(Part part) {
    var vesselInfoModule = part.FindModuleImplementing<ILinkVesselInfo>();
    if (vesselInfoModule != null) {
      vesselInfoModule.vesselInfo = new DockedVesselInfo();
      vesselInfoModule.vesselInfo.name = part.vessel.vesselName;
      vesselInfoModule.vesselInfo.vesselType = part.vessel.vesselType;
      vesselInfoModule.vesselInfo.rootPartUId = part.vessel.rootPart.flightID;
    }
  }
  #endregion
}

}  // namespace
