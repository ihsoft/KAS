// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASAPIv1 {

/// <summary>A generic target of a KAS link between two parts.</summary>
/// <remarks>
/// The target is a sink for a link initiated by the another part's <see cref="ILinkSource"/>.
/// The target logic is very limited and simple. It just remembers the source and does the GUI
/// adjustments as needed.
/// </remarks>
/// <example>See <see cref="ILinkSource"/> for the examples.</example>
// TODO(ihsoft): Add state transtion diagram reference.
public interface ILinkTarget {

  /// <summary>Part that owns the target.</summary>
  /// <value>Instance of the part.</value>
  Part part { get; }

  /// <summary>Target link type identifier.</summary>
  /// <value>Arbitrary string. Can be empty.</value>
  /// <remarks>
  /// This string is used by the source to find the compatible targets.
  /// </remarks>
  string cfgLinkType { get; }

  /// <summary>Name of the attach node to connect with.</summary>
  /// <value>Arbitrary string.</value>
  /// <remarks>
  /// A node with such name must not exist in the part's model. It will be created right before
  /// establishing a link, and will be destroyed after the link is broken.
  /// </remarks>
  /// <seealso cref="nodeTransform"/>
  string cfgAttachNodeName { get; }

  /// <summary>Attach node used for linking with the source part.</summary>
  /// <value>Fully initialized attach node. Can be <c>null</c>.</value>
  /// <remarks>
  /// The node is required to exist only when the target is linked to a source. For the not linked
  /// parts the attach node may not actually exist in the target part.
  /// </remarks>
  /// <seealso cref="cfgAttachNodeName"/>
  AttachNode attachNode { get; }

  /// <summary>Transform that defines the position and orientation of the attach node.</summary>
  /// <value>Game object transformation. It's never <c>null</c>.</value>
  /// <remarks>This transform must exist even when no actual attach node is created on the part:
  /// <list type="bullet">
  /// <item>
  /// When connecting the parts, this transform will be used to create a part's attach node.
  /// </item>
  /// <item>The renderer uses this transform to align the meshes.</item>
  /// <item>The joint module uses a node transform as a source anchor for the PhysX joint.</item>
  /// </list>
  /// </remarks>
  /// <seealso cref="attachNode"/>
  Transform nodeTransform { get; }

  /// <summary>Source that maintains the link.</summary>
  /// <value>Source or <c>null</c> if nothing is linked.</value>
  /// <remarks>
  /// <para>
  /// Setting of this property changes the target state:
  /// <list type="bullet">
  /// <item>A non-null value changes the state to <see cref="LinkState.Linked"/>.</item>
  /// <item><c>null</c> value changes the state to <see cref="LinkState.Available"/>.</item>
  /// </list>
  /// </para>
  /// <para>Assigning the same value to this property doesn't trigger a state change event.</para>
  /// <para>
  /// Note, that not any state transition is possible. If the transition is invalid then an
  /// exception is thrown.
  /// </para>
  /// <para>
  /// It's descouraged to assign this property from a code other than an implementation of
  /// <see cref="ILinkSource"/>.
  /// </para>
  /// </remarks>
  /// <seealso cref="linkState"/>
  ILinkSource linkSource { get; set; }

  /// <summary>ID of the linked source part.</summary>
  /// <value>Flight ID.</value>
  uint linkSourcePartId { get; }

  /// <summary>Current state of the target.</summary>
  /// <value>The current state.</value>
  /// <remarks>
  /// <para>
  /// The state cannot be affected directly. The state is changing in response to the actions that
  /// are implemented by the interface methods.
  /// </para>
  /// </remarks>
  // TODO(ihsoft): Add state transtion diagram.
  LinkState linkState { get; }

  /// <summary>Defines if target must not accept any link requests.</summary>
  /// <value>Locked state.</value>
  /// <remarks>
  /// <para>
  /// Setting of this property changes the target state:
  /// <list type="bullet">
  /// <item><c>true</c> value changes the state to <see cref="LinkState.Locked"/>.</item>
  /// <item><c>false</c> value changes the state to <see cref="LinkState.Available"/>.</item>
  /// </list>
  /// </para>
  /// <para>Assigning the same value to this property doesn't trigger a state change event.</para>
  /// <para>
  /// Note, that not any state transition is possible. If the transition is invalid then an
  /// exception is thrown.
  /// </para>
  /// </remarks>
  /// <seealso cref="linkState"/>
  bool isLocked { get; set; }
}

}  // namespace
