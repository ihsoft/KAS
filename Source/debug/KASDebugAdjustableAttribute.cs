// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.DebugUtils;

namespace KAS.Debug {

/// <summary>Debug adjustable class for KAS.</summary>
/// <remarks>
/// Annotate fields, properties and methods with this attribute to have them revealed in the KAS
/// part adjustment tool, a KAS built-in ability to tweak the parts in flight.
/// </remarks>
public class KASDebugAdjustableAttribute : DebugAdjustableAttribute {

  /// <summary>Debug controls group fro the KAS modules.</summary>
  public const string DebugGroup = "KAS";

  /// <summary>Creates an attribute that marks a KAS tweakable member.</summary>
  /// <param name="caption">The user friendly string to present in GUI.</param>
  public KASDebugAdjustableAttribute(string caption) : base(caption, DebugGroup) {
  }
}

}  // namespace
