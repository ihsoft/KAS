// Kerbal Attachment System API
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASAPIv1 {

/// <summary>
/// Interface for a module that takes care of rendering a link and, optionally, manages its
/// colliders.
/// </summary>
/// <remarks>
/// The link can be rendered between any two transformations. The renderer is responsible to adjust
/// the representation if the connecting points have moved (<see cref="UpdateLink"/>).
/// </remarks>
public interface ILinkRenderer {
  /// <summary>
  /// Unique name of the randerer that is used by the other modules to find this renderer.
  /// </summary>
  /// <value>Arbitrary string. Can be empty.</value>
  string cfgRendererName { get; }

  /// <summary>Temporally sets another color to the link meshes.</summary>
  /// <value>Color or <c>null</c> if the original mesh color should be used.</value>
  // TODO(ihsoft): Deprecate it in favor of the holo model support feature.
  Color? colorOverride { get; set; }

  /// <summary>Temporally sets another shader to the link meshes.</summary>
  /// <value>
  /// Name of the shader or <c>null</c> if the original mesh shader should be used.
  /// </value>
  // TODO(ihsoft): Deprecate it.
  string shaderNameOverride { get; set; }

  /// <summary>Tells if the link colliders should be active.</summary>
  /// <value>The current state of the collider(s).</value>
  /// <remarks>
  /// Setting this property to <c>false</c> disables the link colliders, if there were any. Setting
  /// this oroperty to <c>true</c> doesn't make the link physlcal, it only enables the colliders
  /// that were already on the link.
  /// </remarks>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Collider.html">
  /// Unity3D: Collider</seealso>
  // TODO(ihsoft): Deprecate it in favor of collidersEnabled.
  bool isPhysicalCollider { get; set; }
  
  // TODO(ihsoft): Add method(s) for drawing a hollo representation.
  // void DrawHolo(Vector3 startPos, Vector3 endPos, Color color, string shader)

  /// <summary>Tells if the renderer is started and active.</summary>
  /// <value>The start state.</value>
  /// <seealso cref="StartRenderer"/>
  bool isStarted { get; }

  /// <summary>
  /// Base position/direction of the connection point at the beginning of the link. The source
  /// joint models will be aligned against this transform.
  /// </summary>
  /// <value>The source game object's transform.</value>
  /// <remarks>The value is undefined if the renderer is not started.</remarks>
  /// <seealso cref="StartRenderer"/>
  Transform sourceTransform { get; }

  /// <summary>
  /// Base position/direction of the connection point at the end of the link. The target
  /// joint models will be aligned against this transform.
  /// </summary>
  /// <value>The target game object's transform.</value>
  /// <remarks>The value is undefined if the renderer is not started.</remarks>
  /// <seealso cref="StartRenderer"/>
  Transform targetTransform { get; }

  /// <summary>Starts rendering a link between the objects.</summary>
  /// <remarks>
  /// <para>
  /// This method only indicates that the link is to be drawn between the specified points. The
  /// renderer is allowed to draw meshes even when not started. E.g. if there are constant parts of
  /// the link like the joint pivots.
  /// </para>
  /// <para>
  /// The ends of the link are not required to be located at the surface of the owning parts. It's
  /// up to the renderer to decide how to draw the link.
  /// </para>
  /// <para>
  /// It's OK to call this method multiple times with different or the same source/target arguments:
  /// the renderer must accept the values and update accordingly. However, this operation is rated
  /// as performance expensive, so the callers are discouraged to invoke this method too frequently
  /// (e.g. on every frame update).
  /// </para>
  /// </remarks>
  /// <param name="source">The source node.</param>
  /// <param name="target">The target node.</param>
  void StartRenderer(Transform source, Transform target);

  /// <summary>Cancels rendering the link.</summary>
  /// <remarks>
  /// The stopped renderer is not required to not render anything. The stopped state only tells
  /// that the source and the target positions provided to the <see cref="StartRenderer"/> method
  /// must not be respresented as connected anymore. A specific renderer implementation is free to
  /// choose how to represent this mode.
  /// <para>
  /// It's OK to call this method multiple time. If the renderer is already stopped the call must be
  /// treated as NO-OP with a little or no performance cost.
  /// </para>
  /// </remarks>
  void StopRenderer();

  /// <summary>Called when a link representation update is required.</summary>
  /// <remarks>
  /// It's called on every frame update if the link is started. The performance cost of this method
  /// is rated as moderate. The callers should consider optimization techniques to avoid calling
  /// this method on the every frame update.
  /// <para>
  /// A specific renderer implementation may introduce own optimization algorithm when the call
  /// becomes too heavy and slow.
  /// </para>
  /// </remarks>
  void UpdateLink();

  /// <summary>Verifies that there are no obstacles beween the points.</summary>
  /// <remarks>The renderer is not required to be started for this method to call.</remarks>
  /// <param name="source">The source node.</param>
  /// <param name="target">The target node.</param>
  /// <returns>
  /// An empty array if no hits were detected, or a list of user friendly errors otherwise.
  /// </returns>
  // TODO(ihsoft): Deprecate it in favor of the hollo model callback.
  string[] CheckColliderHits(Transform source, Transform target);

  /// <summary>Returns a mesh, created by the renderer.</summary>
  /// <remarks>
  /// It depends on the implementation which meshes a specific renderer creates. The caller must be
  /// aware of which renderer it uses and don't request unknown meshes.
  /// </remarks>
  /// <param name="meshName">The name of the mesh. It's not required to be the object name!</param>
  /// <returns>The object or <c>null</c> if the named mesh is not created.</returns>
  /// <exception cref="ArgumentException">If the mesh cannot be retrieved.</exception>
  Transform GetMeshByName(string meshName);
}

}  // namespace
