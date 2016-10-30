// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KASAPIv1;
using System;
using System.Linq;
using UnityEngine;

namespace KASImpl {

class LinkUtilsImpl : ILinkUtils {
  /// <inheritdoc/>
  public ILinkTarget FindLinkTargetFromSource(ILinkSource source) {
    if (source != null && source.attachNode != null && source.attachNode.attachedPart != null) {
      return source.attachNode.attachedPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(x => x.attachNode != null && x.attachNode.attachedPart == source.part);
    }
    return null;
  }

  /// <inheritdoc/>
  public ILinkSource FindLinkSourceFromTarget(ILinkTarget target) {
    if (target != null && target.attachNode != null && target.attachNode.attachedPart != null) {
      return target.attachNode.attachedPart.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(x => x.attachNode != null && x.attachNode.attachedPart == target.part);
    }
    return null;
  }

  /// <inheritdoc/>
  public DockedVesselInfo CoupleParts(AttachNode sourceNode, AttachNode targetNode) {
    var srcPart = sourceNode.owner;
    var srcVessel = srcPart.vessel;
    var trgPart = targetNode.owner;
    var trgVessel = trgPart.vessel;

    var vesselInfo = new DockedVesselInfo();
    vesselInfo.name = srcVessel.vesselName;
    vesselInfo.vesselType = srcVessel.vesselType;
    vesselInfo.rootPartUId = srcVessel.rootPart.flightID;

    GameEvents.onActiveJointNeedUpdate.Fire(srcVessel);
    GameEvents.onActiveJointNeedUpdate.Fire(trgVessel);
    sourceNode.attachedPart = trgPart;
    targetNode.attachedPart = srcPart;
    srcPart.attachMode = AttachModes.STACK;  // All KAS links are expected to be STACK.
    srcPart.Couple(trgPart);
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
    GameEvents.onVesselWasModified.Fire(sourceNode.owner.vessel);

    return vesselInfo;
  }

  /// <inheritdoc/>
  public Vessel DecoupleParts(Part part1, Part part2) {
    Vessel inactiveVessel;
    if (part1.parent == part2) {
      part1.decouple();
      inactiveVessel = part2.vessel;
    } if (part2.parent == part1) {
      part2.decouple();
      inactiveVessel = part1.vessel;
    } else {
      Debug.LogWarningFormat("Cannot decouple since parts belong to different vessels: {0} != {1}",
                             part1.vessel, part2.vessel);
      return null;
    }
    return inactiveVessel;
  }
}

}  // namespace
