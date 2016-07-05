// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;

namespace KAS_API {

/// <summary>A generic target of a KAS link between two parts.</summary>
/// <remarks>Target is a sink for the link initiated by another part's <see cref="ILinkSource"/>.
/// The target logic is very limited and simple. It just remembers the source and does whatever GUI
/// adjustments are needed.</remarks>
/// FIXME(ihsoft): Add state transtion diagram reference.
/// FIXME(ihsoft): Add code samples.
public interface ILinkTarget {
  /// <summary>Part that owns the target.</summary>
  Part part { get; }

  /// <summary>A target link type identifier.</summary>
  /// <remarks>This type is used to match with compatible sources. Sources of different types will
  /// not be able to connect with the target. Type can be any string, including empty.</remarks>
  string linkType { get; }

  /// <summary>Source that maintains the link or <c>null</c> if nothing is linked.</summary>
  ILinkSource linkSource { get; set; }

  /// <summary>Current state of the target.</summary>
  /// <remarks>The state cannot be affected directly. Different methods change it to different
  /// values. Though, there is strict model of state tranistioning for the target.</remarks>
  /// FIXME(ihsoft): Add state transtion diagram.
  LinkState linkState { get; }

  /// <summary>Defines if target must not accept any link requests.</summary>
  /// <remarks>Setting of this property changes target state. Decendants can react on this action to
  /// do internal state adjustments (e.g. changing GUI items).</remarks>
  bool isLocked { get; set; }
}

}  // namespace
