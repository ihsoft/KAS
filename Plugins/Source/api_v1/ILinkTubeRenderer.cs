// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

namespace KASAPIv1 {

public enum LinkJointType {
  None,
  Rounded,
  RoundedWithOffset,
}

public enum LinkTextureRescaleMode {
  None,
  Source,
  Target,
  Center
}

public enum LinkCollider {
  None,
  Mesh,
  Capsule
}

public interface ILinkTubeRenderer {
  string cfgRendererName { get; }
  LinkJointType cfgSourceJointType { get; }
  float cfgSourceJointOffset { get; }
  LinkJointType cfgTargetJointType { get; }
  float cfgTargetJointOffset { get; }
  float cfgPipeTextureSamplesPerMeter { get; }
  string cfgPipeTexturePath { get; }
  string cfgShaderName { get; }
  LinkTextureRescaleMode cfgPipeRescaleMode { get; }
  float cfgPipeScale { get; }
  float cfgSphereScale { get; }

  Color cfgColor { get; set; }
  Color? colorOverride { get; set; }
  string shaderNameOverride { get; set; }

  void StartRenderer(Transform source, Transform target);
  void StopRenderer();
  void UpdateLink();
  // Checks for sphere hit.
  string CheckColliderHits(Transform source, Transform target);
}

}  // namespace
