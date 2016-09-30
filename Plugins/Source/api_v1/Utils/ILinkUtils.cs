// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

// Name of the namespace denotes the API version.
namespace KASAPIv1 {

//FIXME: give doc with samples
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

  /// <summary>Finds link source given a part.</summary>
  /// <remarks>
  /// Only one source on the part can be linked. This method goes over all sources on the source
  /// part, and returns the one that is linked, and the link is valid.
  /// <para>
  /// It's discouraged to implement this logic in own code since linking approach may change in the
  /// future versions.
  /// </para>
  /// </remarks>
  /// <param name="sourcePart">Part to check source on.</param>
  /// <returns>Source or <c>null</c> if no valid source was found.</returns>
  ILinkSource FindLinkSourceFromPart(Part sourcePart);
}

}  // namespace
