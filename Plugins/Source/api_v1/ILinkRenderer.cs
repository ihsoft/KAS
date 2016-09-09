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

  /// <summary>Tells if renderer is started and active.</summary>
  bool isStarted { get; }
//  Transform sourceTransform { get; set; }
//  Transform targetTransform { get; set; }

  /// <summary>Starts rendering link between the points.</summary>
  /// <remarks>This method only indicates that the link is to be drawn between the specified points.
  /// The renderer is allowed to draw meshes even when not started. E.g. if there are constants
  /// parts of the link like joint pivots.
  /// <para>It's OK to call this method multiple times with different or same source/target
  /// arguments. Renderer must accept the values and update accordingly. Though, this operation is
  /// rated as performance expensive, so callers are discouraged to invoke this method too
  /// frequently (e.g. from on every frame update).</para>
  /// </remarks>
  /// <param name="source">Source node.</param>
  /// <param name="target">Target node.</param>
  void StartRenderer(Transform source, Transform target);

  /// <summary>Cancells rendering the link.</summary>
  /// <remarks>Stopped renderers are not required to not render anything. Stopped state only affects
  /// the link started by <see cref="StartRenderer"/>.
  /// <para>It's OK to call this method multiple time. If renderer is already stopped the call must
  /// be treated as NO-OP with a little or no performance cost.</para></remarks>
  void StopRenderer();

  /// <summary>Called when link representation update is required.</summary>
  /// <remarks>Performance cost of this method is rated as moderate. Callers should consider
  /// optimization techniques to avoid calling this method on every frame update.
  /// <para>The interface implementation may implement own optimization algorithm when call becomes
  /// too heavy and slow.</para>
  /// </remarks>
  void UpdateLink();

  /// <summary>Verifies that there are no obstacles beween the points.</summary>
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
