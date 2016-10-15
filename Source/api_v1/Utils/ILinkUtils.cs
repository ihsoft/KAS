// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

// Name of the namespace denotes the API version.
namespace KASAPIv1 {

/// <summary>Various tools to deal with KAS links.</summary>
public interface ILinkUtils {
  /// <summary>Finds linked target given the link source.</summary>
  /// <remarks>
  /// Any number of targets can be linked on the part but only one is linked with a particular
  /// source. This method goes over all targets on the target part, and returns the one that is
  /// linked with the provided source.
  /// <para>
  /// It's discouraged to implement this logic in own code since linking approach may change in the
  /// future versions.
  /// </para>
  /// </remarks>
  /// <param name="source">Source to get target for.</param>
  /// <returns>Target or <c>null</c> if no valid target was found.</returns>
  ILinkTarget FindLinkTargetFromSource(ILinkSource source);

  /// <summary>Finds linked source given the link target.</summary>
  /// <remarks>
  /// Only one source on the part can be linked. This method goes over all sources on the source
  /// part, and returns the one that is linked with the provided target.
  /// <para>
  /// It's discouraged to implement this logic in own code since linking approach may change in the
  /// future versions.
  /// </para>
  /// </remarks>
  /// <param name="target">target to get source for.</param>
  /// <returns>Source or <c>null</c> if no valid source was found.</returns>
  ILinkSource FindLinkSourceFromTarget(ILinkTarget target);

  /// <summary>Couples two parts together given they belong to different vessels.</summary>
  /// <remarks>
  /// Once coupling is done the source vessel will be destroyed, and become a part of the target
  /// vessel. The new target vessel will become active.
  /// </remarks>
  /// <para>Attach nodes must have valid <c>owner</c> set.</para>
  /// <param name="sourceNode">
  /// Attach node at the source part that defines the source vessel.
  /// </param>
  /// <param name="targetNode">
  /// Attach node at the target part that defines the target vessel.
  /// </param>
  /// <returns></returns>
  DockedVesselInfo CoupleParts(AttachNode sourceNode, AttachNode targetNode);
}

}  // namespace
