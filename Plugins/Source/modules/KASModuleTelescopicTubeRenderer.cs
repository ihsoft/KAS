// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;
using KASAPIv1;

namespace KAS {

public class KASModuleTelescopicTubeRenderer : KASModuleTubeRenderer {
  #region ILinkTubeRenderer config propertiers implementation
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public float pistonLength = 0.2f;
//  [KSPField]
//  public float pistonOverlap = 0.03f;
  [KSPField]
  public int pistonsCount = 3;
  [KSPField]
  public float pistonWallThickness = 0.01f;
  #endregion

  protected GameObject[] pistons;

  #region ILinkTubeRenderer implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTubeRenderer"/>.</para>
  public override void StartRenderer(Transform source, Transform target) {
    base.StartRenderer(source, target);
    linkPipeMR.enabled = false;
    CreatePistons();
  }
  
  void CreatePistons() {
    pistons = new GameObject[pistonsCount];
    var startScale = pipeDiameter;
    for (var i = 0; i < pistonsCount; ++i) {
      var piston = CreatePrimitive(PrimitiveType.Cylinder, startScale, part.transform);
      piston.transform.localScale = new Vector3(startScale, startScale, pistonLength);
      startScale -= 2 * pistonWallThickness;
      RescaleTextureToLength(piston);
      pistons[i] = piston;
    }
    UpdatePistons();
  }

  void SetupCylinderPrimitive(GameObject obj, Vector3 pos, Vector3 dir) {
    obj.transform.position = pos + dir * (obj.transform.localScale.z / 2);
    obj.transform.LookAt(obj.transform.position + dir);
  }

  void UpdatePistons() {
    var fromPos = sourceJointNode.position;
    var toPos = targetJointNode.position;
    var linkDirection = (toPos - fromPos).normalized;
    var lookAtPos = toPos + linkDirection;

    // First piston has fixed position due to it's attached to the source.
    SetupCylinderPrimitive(pistons[0], fromPos, linkDirection);
    // Last piston has fixed position due to it's atatched to the target.
    SetupCylinderPrimitive(
        pistons[pistons.Length - 1], toPos - linkDirection * pistonLength, linkDirection);
    // Pistions between first and last monotonically fill the link.
    if (pistons.Length > 2) {
      var linkFillStart = fromPos + linkDirection * pistonLength;
      var linkFillEnd = targetJointNode.position - linkDirection * pistonLength;
      var linkStep = (linkFillEnd - linkFillStart) / (pistonsCount - 2);
      for (var i = 1; i < pistons.Length - 1; ++i) {
        var piston = pistons[i];
        piston.transform.position = linkFillStart + linkStep / 2;
        piston.transform.LookAt(lookAtPos);
        linkFillStart += linkStep;
      }
    }
  }

  void DeletePistons() {
    foreach (var piston in pistons) {
      piston.DestroyGameObject();
    }
    pistons = null;
  }

  /// <inheritdoc/>
  public override void StopRenderer() {
    base.StopRenderer();
    if (pistons != null) {
      DeletePistons();
    }
  }

  /// <inheritdoc/>
  public override void UpdateLink() {
    base.UpdateLink();
    if (pistons != null) {
      UpdatePistons();
    }
  }
  #endregion
}

}  // namespace
