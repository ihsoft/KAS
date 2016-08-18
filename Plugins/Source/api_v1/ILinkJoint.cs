// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

namespace KASAPIv1 {

//FIXME: docs and samples
public interface ILinkJoint {
  /// <summary>Sets up a physical link between source and target.</summary>
  /// <remarks>If parts are docked then there is a <see cref="PartJoint"/> created by the KSP core.
  /// The implementation must either adjust it or drop it alltogether. Note, that in the either case
  /// pack/unpack events must be handled to keep the joint setup at the specified values.
  /// <para>For the docked parts source is always a child to the target. That said,
  /// <c>source.part.attachJoint</c> is the KSP managed joint which the implementation may want to
  /// adjust.</para>
  /// </remarks>
  /// <param name="source">Link source. This part owns the joint.</param>
  /// <param name="target">Link target.</param>
  void SetupJoint(ILinkSource source, ILinkTarget target);

  /// <summary>Destroys a physical link between source and target.</summary>
  void CleanupJoint();

  /// <summary>Checks if link length is within the limits.</summary>
  /// <param name="source">Source that probes the link.</param>
  /// <param name="targetTransform">Target of the link to check the length of.</param>
  /// <returns>An error message if link is over limit or <c>null</c> otherwise.</returns>
  /// FIXME: use ILinkTarget once kerbal gets it.
  string CheckLengthLimit(ILinkSource source, Transform targetTransform);

  /// <summary>Checks if link angle at the source joint is within the limits.</summary>
  /// <param name="source">Source that probes the link.</param>
  /// <param name="targetTransform">Target of the link to check the angle against.</param>
  /// <returns>An error message if angle is over limit or <c>null</c> otherwise.</returns>
  string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform);

  /// <summary>Checks if link angle at the target joint is within the limits.</summary>
  /// <param name="source">Source that probes the link.</param>
  /// <param name="targetTransform">Target of the link to check the angle against.</param>
  /// <returns>An error message if angle is over limit or <c>null</c> otherwise.</returns>
  string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform);
}

}  // namespace
