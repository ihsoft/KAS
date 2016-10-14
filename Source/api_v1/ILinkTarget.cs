// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

namespace KASAPIv1 {

/// <summary>A generic target of a KAS link between two parts.</summary>
/// <remarks>Target is a sink for the link initiated by another part's <see cref="ILinkSource"/>.
/// The target logic is very limited and simple. It just remembers the source and does whatever GUI
/// adjustments are needed.</remarks>
/// TODO(ihsoft): Add state transtion diagram reference.
/// TODO(ihsoft): Add code samples.
public interface ILinkTarget {
  /// <summary>Part that owns the target.</summary>
  Part part { get; }

  /// <summary>A target link type identifier.</summary>
  /// <remarks>
  /// This type is used to match with compatible sources. Sources of different types will not be
  /// able to connect with the target. Type can be any string, including empty.
  /// </remarks>
  string cfgLinkType { get; }

  /// <summary>Name of the attach node to connect with.</summary>
  /// <remarks>
  /// Name is not required to be one of the KSP reserved ones (e.g. "top"). It can be any string.
  /// </remarks>
  string cfgAttachNodeName { get; }

  /// <summary>Attach node used for linking with the source part.</summary>
  /// <remarks>
  /// The node is required to exist only when target is linked to a compatible source. For not
  /// linked parts attach node may not actually exist in the target part.
  /// </remarks>
  /// <seealso cref="cfgAttachNodeName"/>
  AttachNode attachNode { get; }

  /// <summary>Transform that defines position and orientation of the attach node.</summary>
  /// <remarks>
  /// This transform must exist even when no actual attach node is created on the part.
  /// <list>
  /// <item>When connecting parts this transform will be used to create part's attach node.</item>
  /// <item>Renderer uses this transform to align meshes.</item>
  /// <item>Joint module uses node transform as source anchor for PhysX joint.</item>
  /// </list>
  /// </remarks>
  Transform nodeTransform { get; }

  /// <summary>Source that maintains the link or <c>null</c> if nothing is linked.</summary>
  /// <remarks>
  /// Setting of this property changes target state: a non-null value changes state to
  /// <see cref="LinkState.Linked"/>; <c>null</c> value changes state to
  /// <see cref="LinkState.Available"/>.
  /// <para>Setting same value to this property doesn't trigger state change events.</para>
  /// <para>
  /// Note, that not any state transition is possible. If transition is invalid then exception is
  /// thrown.
  /// </para>
  /// </remarks>
  /// <seealso cref="linkState"/>
  ILinkSource linkSource { get; set; }

  /// <summary>Current state of the target.</summary>
  /// <remarks>
  /// The state cannot be affected directly. Different methods change it to different values.
  /// Though, there is strict model of state tranistioning for the target.
  /// </remarks>
  /// TODO(ihsoft): Add state transtion diagram.
  LinkState linkState { get; }

  /// <summary>
  /// Defines if target must not accept any link requests because the part is already linked as
  /// source.
  /// </summary>
  /// <remarks>
  /// Setting of this property changes target state: <c>true</c> value changes state to
  /// <see cref="LinkState.Locked"/>; <c>false</c> value changes state to
  /// <see cref="LinkState.Available"/>.
  /// <para>Setting same value to this property doesn't trigger state change events.</para>
  /// <para>
  /// Note, that not any state transition is possible. If transition is invalid then exception is
  /// thrown.
  /// </para>
  /// </remarks>
  /// <seealso cref="linkState"/>
  bool isLocked { get; set; }
}

}  // namespace
