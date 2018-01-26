// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

// Name of the namespace denotes the API version.
namespace KASAPIv1 {

/// <summary>Various tools to deal with KAS links.</summary>
public interface ILinkUtils {
  /// <summary>Finds the other peer of the link.</summary>
  /// <remarks>
  /// The links are always 1-to-1, i.e. one source peer can be linked to exactly one target peer.
  /// It's discouraged to implement this logic in the own code since the linking approach may change
  /// in the future versions.
  /// </remarks>
  /// <param name="srcPeer">THe peer to find a target for.</param>
  /// <returns>The peer or <c>null</c> if no valid target was found.</returns>
  ILinkPeer FindLinkPeer(ILinkPeer srcPeer);

  /// <summary>Couples two parts together given they belong to the different vessels.</summary>
  /// <remarks>
  /// <para>
  /// Once the coupling is done, one of the vessels will be destroyed. It will become a part of the
  /// other vessel. The new merged vessel will become active. Which vessel will be destroyed is
  /// determined by the <paramref name="toDominantVessel"/> parameter.
  /// </para>
  /// <para>
  /// This coupling requires the both attach nodes to be provided, and creates a "stack" nodes
  /// coupling.
  /// </para>
  /// </remarks>
  /// <para><i>IMPORTANT</i>. The attach nodes must have a valid <c>owner</c> set.</para>
  /// <param name="sourceNode">
  /// The attach node at the source part that defines the source vessel. It must not be <c>null</c>. 
  /// </param>
  /// <param name="targetNode">
  /// The attach node at the target part that defines the target vessel. It must not be <c>null</c>.
  /// </param>
  /// <param name="toDominantVessel">
  /// If <c>false</c>, then the source vessel will get coupled with the target. As a result, the
  /// source vessel will be destroyed. If <c>true</c>, then the method will find the <i>least</i>
  /// significant vessel of the two, and couple it with the <i>most</i> significant one. The least
  /// signficant vessel will be destroyed.
  /// </param>
  /// <returns>The part that attached as a child into the new hierarchy.</returns>
  /// <seealso cref="ILinkVesselInfo"/>
  /// <seealso cref="IAttachNodesUtils"/>
  Part CoupleParts(AttachNode sourceNode, AttachNode targetNode,
                   bool toDominantVessel = false);

  /// <summary>Decouples the connected parts and breaks down one vessel into two.</summary>
  /// <remarks>
  /// If the part, being decoupled, has the <c>DockedVesselInfo</c> provided, then additionally to
  /// the decoupling, the method will also restore the old vessel properties. Including the root
  /// part.
  /// </remarks>
  /// <param name="part1">
  /// The first part of the connection. It must be a direct parent or a child of the
  /// <paramref name="part2"/>.
  /// </param>
  /// <param name="part2">
  /// The second part of the connection. It must be a direct parent or a child of the
  /// <paramref name="part1"/>.
  /// </param>
  /// <param name="vesselInfo1">
  /// The optional info of the vessel that owned the <paramref name="part1"/> on coupling.
  /// </param>
  /// <param name="vesselInfo2">
  /// The optional info of the vessel that owned the <paramref name="part2"/> on coupling.
  /// </param>
  /// <returns>The child part that has decoupled from the owner vessel.</returns>
  Part DecoupleParts(Part part1, Part part2,
                     DockedVesselInfo vesselInfo1 = null, DockedVesselInfo vesselInfo2 = null);
}

}  // namespace
