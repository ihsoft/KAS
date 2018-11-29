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
    get { return "$parkAtPartTarget-" + part.Modules.IndexOf(this); }
  }
  #endregion

  #region Helper class for drawing a head model at the pipe's target emd.
  /// <summary>Helper class for drawing a pipe's end.</summary>
  protected class ParkedHead : ModelPipeEndNode {
    /// <summary>
    /// Transform at which the node's model should be parked when the renderer is stopped.
    /// </summary>
    public readonly Transform parkAt;

    /// <summary>Creates a new attach node.</summary>
    /// <param name="model">Model to use. It cannot be <c>null</c>.</param>
    /// <param name="parkAt">Object ot park the model at when the renderer is stopped.</param>
    public ParkedHead(Transform model, Transform parkAt) : base(model) {
      this.parkAt = parkAt;
    }

    /// <inheritdoc/>
    public override void AlignTo(Transform target) {
      if (target == null) {
        AlignTransforms.SnapAlign(model, pipeAttach, parkAt);
        model.gameObject.SetActive(true);
      } else {
        base.AlignTo(target);
      }
    }
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
  protected override Transform CreateJointEndModels(string modelName, JointConfig config) {
    var res = base.CreateJointEndModels(modelName, config);
    if (modelName.EndsWith("-targetNode")) {
      var partAtTransform = new GameObject(ParkAtPartObjectName).transform;
      Hierarchy.MoveToParent(partAtTransform, partModelTransform,
                             newPosition: parkAtPart.pos,
                             newRotation: parkAtPart.rot);
    }
    return res;
  }
  #endregion
}

}  // namespace
