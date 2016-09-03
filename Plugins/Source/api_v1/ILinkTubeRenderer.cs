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

/// <summary>Defines how link collisions should be checked.</summary>
public enum LinkCollider {
  /// <summary>No collisions check.</summary>
  None,
  /// <summary>Check collisions basing on the mesh. It's performance expensive.</summary>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/MeshCollider.html"/>
  Mesh,
  /// <summary>Simple collider which fits the primitive type. It's performance optimized.</summary>
  Shape,
  /// <summary>Simple collider which wraps all mesh vertexes. It's performance optimized.</summary>
  Bounds,
  /// <summary>Create a simple capsule collider which is performance optimized.</summary>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/CapsuleCollider.html"/>
  //FIXME: drop
  Capsule
}

public interface ILinkPipeRenderer {
  string cfgRendererName { get; }
  LinkJointType cfgSourceJointType { get; }
  float cfgSourceJointOffset { get; }
  LinkJointType cfgTargetJointType { get; }
  float cfgTargetJointOffset { get; }
  float cfgPipeTextureSamplesPerMeter { get; }
  string cfgPipeTexturePath { get; }
  string cfgShaderName { get; }
  LinkTextureRescaleMode cfgPipeRescaleMode { get; }
  float cfgPipeDiameter { get; }
  float cfgSphereDiameter { get; }
  Color cfgColor { get; set; }

  Color? colorOverride { get; set; }
  string shaderNameOverride { get; set; }
  bool isPhysicalCollider { get; set; }
  Transform startSocketTransfrom { get; }
  Transform endSocketTransfrom { get; }

  void StartRenderer(Transform source, Transform target);
  void StopRenderer();
  void UpdateLink();
  // Checks for sphere hit.
  string CheckColliderHits(Transform source, Transform target);
}

}  // namespace
