// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.ModelUtils;
using KSPDev.Types;
using UnityEngine;

namespace KAS {

/// <summary>
/// Extension to the regular render that does't hide the target's joint node. Instead, the node is
/// "parked" at the main part model.
/// </summary>
/// <seealso cref="parkAtPart"/>
public class KASRendererPipeWithHead : KASRendererPipe {

  #region Object names for the procedural model construction
  /// <summary>
  /// Name of the object in the part's model to park the target node's model at.  
  /// </summary>
  protected string ParkAtPartObjectName {
    get { return ModelBasename + "-partAt"; }
  }
  #endregion

  #region Part's config settings loaded via ConfigAccessor
  /// <summary>Position/rotation of the head when the renderer is stopped.</summary>
  /// <remarks>The object is added at the part's model root.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentField/*"/>
  [PersistentField("parkAtPart", group = StdPersistentGroups.PartConfigLoadGroup)]
  public PosAndRot parkAtPart = new PosAndRot();
  #endregion

  #region KASModulePipeRenderer overrides
  /// <inheritdoc/>
  protected override void LoadPartModel() {
    base.LoadPartModel();
    ParkHead();
  }

  /// <inheritdoc/>
  protected override void DestroyPipeMesh() {
    base.DestroyPipeMesh();
    ParkHead();
  }
  #endregion

  #region Local utility methods
  /// <summary>Place the unlinked head on the part's model.</summary>
  void ParkHead() {
    if (targetJointConfig.type == PipeEndType.PrefabModel) {
      var headNode = MakePrefabNode(targetJointConfig);
      if (headNode != null) {
        var parkAt = partModelTransform.Find(ParkAtPartObjectName)
            ?? new GameObject(ParkAtPartObjectName).transform;
        Hierarchy.MoveToParent(parkAt, partModelTransform,
                               newPosition: parkAtPart.pos,
                               newRotation: parkAtPart.rot);
        AlignTransforms.SnapAlign(headNode.rootModel, headNode.pipeAttach, parkAt);
        headNode.rootModel.gameObject.SetActive(true);
        headNode.rootModel.parent = partModelTransform;
      }
    }
  }
  #endregion
}

}  // namespace
