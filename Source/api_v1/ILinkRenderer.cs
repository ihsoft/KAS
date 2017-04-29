// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
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
  /// <value>
  /// Unique name of the randerer that is used by the other modules to find this renderer.
  /// </value>
  string cfgRendererName { get; }

  /// <value>Temporally sets another color to the link meshes.</value>
  /// <remarks>
  /// Set it to <c>null</c> to reset the override and get back to the original color.
  /// </remarks>
  /// TODO(ihsoft): Deprecate it.
  Color? colorOverride { get; set; }

  /// <value>Temporally sets another shader to the link meshes.</value>
  /// <remarks>Set it to <c>null</c> to reset the override and get back to the original shader.
  /// </remarks>
  /// TODO(ihsoft): Deprecate it.
  string shaderNameOverride { get; set; }

  /// <value>Tells if the link interacts with the rigid bodies.</value>
  /// <remarks>
  /// Setting this property to <c>false</c> turns the link colliders into triggers. I.e. the link
  /// won't have a physical impact but the collision events will be sent to the parent game object.
  /// </remarks>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Collider.html">
  /// Unity3D: Collider</seealso>
  /// TODO(ihsoft): Deprecate it in favor of collidersEnabled.
  bool isPhysicalCollider { get; set; }
  
  // TODO(ihsoft): Add method(s) for drawing a hollow representation.

  /// <value>Tells if the renderer is started and active.</value>
  /// <seealso cref="StartRenderer"/>
  bool isStarted { get; }

  /// <value>
  /// Base position/direction of the connection point at the beginning of the link. The source
  /// joint models will be aligned against this transform.
  /// </value>
  /// <seealso cref="StartRenderer"/>
  Transform sourceTransform { get; }

  /// <value>
  /// Base position/direction of the connection point at the end of the link. The target
  /// joint models will be aligned against this transform.
  /// </value>
  /// <seealso cref="StartRenderer"/>
  Transform targetTransform { get; }

  /// <value>
  /// Defines how significantly the link has stretched or shrinked comparing to it's "normal" state.
  /// </value>
  /// <remarks>
  /// A value below <c>1.0</c> means the link has shrinked. Otherwise, it's stretched. 
  /// <para>
  /// This ratio only affects the visual representation. For the renderers that don't care about
  /// stretching it's ok to always return <c>1.0</c> from the getter and ignore calls to the setter.
  /// </para>
  /// </remarks>
  float stretchRatio { get; set; }

  /// <value>Starts rendering a link between the points.</value>
  /// <remarks>
  /// This method only indicates that the link is to be drawn between the specified points. The
  /// renderer is allowed to draw meshes even when not started. E.g. if there are constants parts of
  /// the link like the joint pivots.
  /// <para>
  /// The ends of the link are not required to be located at the surface of the owning parts. It's
  /// up to the renderer to decide how to draw the joint.
  /// </para>
  /// <para>
  /// It's OK to call this method multiple times with different or the same source/target arguments:
  /// the renderer must accept the values and update accordingly. However, this operation is rated
  /// as performance expensive, so the callers are discouraged to invoke this method too frequently
  /// (e.g. on every frame update).
  /// </para>
  /// </remarks>
  /// <param name="source">Source node.</param>
  /// <param name="target">Target node.</param>
  // TODO(ihsoft): Migrate to ILinkSource & ILinkTarget.
  void StartRenderer(Transform source, Transform target);

  /// <summary>Cancells rendering the link.</summary>
  /// <remarks>
  /// THe stopped renderers are not required to not render anything. The stopped state only tells
  /// that the source and the target position provided to the <see cref="StartRenderer"/> method
  /// must not be respresented as connected anymore. A specific renderer implementation is free to
  /// choose how to represent this situation.
  /// <para>
  /// It's OK to call this method multiple time. If the renderer is already stopped the call must be
  /// treated as NO-OP with a little or no performance cost.
  /// </para>
  /// </remarks>
  void StopRenderer();

  /// <summary>Called when a link representation update is required.</summary>
  /// <remarks>
  /// The performance cost of this method is rated as moderate. The callers should consider
  /// optimization techniques to avoid calling this method on the every frame update.
  /// <para>
  /// A specific renderer implementation may introduce own optimization algorithm when the call
  /// becomes too heavy and slow.
  /// </para>
  /// </remarks>
  void UpdateLink();

  /// <summary>Verifies that there are no obstacles beween the points.</summary>
  /// <remarks>The renderer is not required to be started for this method to call.</remarks>
  /// <param name="source">Source node.</param>
  /// <param name="target">Target node.</param>
  /// <returns>
  /// <c>null</c> if nothing collides with the link. Otherwise, a short user friendly message.
  /// </returns>
  /// TODO(ihsoft): Deprecate it in favor of the hollow model callback.
  string CheckColliderHits(Transform source, Transform target);
}

}  // namespace
