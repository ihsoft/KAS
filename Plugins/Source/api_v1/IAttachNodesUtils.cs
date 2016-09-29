// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

// Name of the namespace denotes the API version.
namespace KASAPIv1 {

//FIXME: give doc with samples
public interface IAttachNodesUtils {
  /// <summary>Returns attach node by name.</summary>
  /// <remarks>
  /// This method ensures that the found node is properly initialized. As of KSP 1.1.3.1289 owner
  /// of the node can be <c>null</c>. The node returned by this methid is guaranteed to have all the
  /// members set as expected.
  /// </remarks>
  /// <param name="part">Part to look nodes in.</param>
  /// <param name="attachNodeName">name of the node.</param>
  /// <returns>Attch node or <c>null</c> if nothing is found.</returns>
  AttachNode GetAttachNode(Part part, string attachNodeName);

  /// <summary>Returns attach node transform. Creates it if it doesn't exist.</summary>
  /// <remarks>
  /// Attach node transform is located at the attach node position and looks in the same direction.
  /// The returned transform is a child of the owning part transform, so it could be used as an
  /// anchor in relational 3D calculations.
  /// </remarks>
  /// <param name="part">Part to lookup node in.</param>
  /// <param name="attachNodeName">Name of the node.</param>
  /// <returns>Transform that is a child of the part's transform.</returns>
  Transform GetOrCreateNodeTransform(Part part, string attachNodeName);

  /// <summary>Returns attach node transform. Creates it if it doesn't exist.</summary>
  /// <remarks>
  /// Attach node transform is located at the attach node position and looks in the same direction.
  /// The returned transform is a child of the owning part transform, so it could be used as an
  /// anchor in relational 3D calculations.
  /// </remarks>
  /// <param name="attachNode">Node to get/create transform for.</param>
  /// <returns>Transform that is a child of the owning part transform.</returns>
  Transform GetOrCreateNodeTransform(AttachNode attachNode);
}

}  // namespace
