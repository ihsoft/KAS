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
  /// <seealso href="https://docs.unity3d.com/ScriptReference/MeshCollider.html">
  /// Unity3D: MeshCollider</seealso>
  Mesh,
  /// <summary>Simple collider which fits the primitive type. It's performance optimized.</summary>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/PrimitiveType.html">
  /// Unity3D: PrimitiveType</seealso>
  Shape,
  /// <summary>Simple collider which wraps all mesh vertexes. It's performance optimized.</summary>
  Bounds,
  /// <summary>Create a simple capsule collider which is performance optimized.</summary>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/CapsuleCollider.html">
  /// Unity3D: CapsuleCollider</seealso>
  //FIXME: drop
  Capsule
}

public interface ILinkRenderer {
  /// <summary>Renderer name from the config.</summary>
  string cfgRendererName { get; }

  /// <summary>Temporally sets another color to the link meshes.</summary>
  /// <remarks>Set it to <c>null</c> to reset the override and get back to the original color.
  /// </remarks>
  Color? colorOverride { get; set; }

  /// <summary>Temporally sets another shader to the link meshes.</summary>
  /// <remarks>Set it to <c>null</c> to reset the override and get back to the original shader.
  /// </remarks>
  string shaderNameOverride { get; set; }

  /// <summary>Tells if link interact with rigid objects with a collider.</summary>
  /// <remarks>Setting this property to <c>false</c> turns link colliders into triggers. I.e. the
  /// link won't have physical impact but collision events will be sent to the parent game object.
  /// </remarks>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Collider.html">Unity3D: Collider
  /// </seealso>
  bool isPhysicalCollider { get; set; }

  /// <summary>Starts rendering link between the points.</summary>
  /// <param name="source">Source node.</param>
  /// <param name="target">Target node.</param>
  void StartRenderer(Transform source, Transform target);

  /// <summary>Cancells rendering the link.</summary>
  void StopRenderer();

  /// <summary>Called when link representation update is required.</summary>
  void UpdateLink();

  /// <summary>Verifies that there are no osbtacles beween the points.</summary>
  /// <param name="source">Source node.</param>
  /// <param name="target">Target node.</param>
  /// <returns><c>null</c> if nothing collides with the link. Otherwise, a short user friendly
  /// message.</returns>
  string CheckColliderHits(Transform source, Transform target);
}

public interface ILinkPipeRendererModule : ILinkRenderer {
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
}

}  // namespace
