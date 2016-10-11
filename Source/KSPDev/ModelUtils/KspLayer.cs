// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.ModelUtils {

/// <summary>Flags for the collision layers in KSP.</summary>
/// <remarks>
/// It's not a full set of the layers. More investigation is needed to reveal all of them.
/// </remarks>
[Flags]
public enum KspLayerMask {
  /// <summary>Just a default value that doesn't match any layer.</summary>
  NONE = 0,
  
  /// <summary>Layer for a regular part.</summary>
  PARTS = 1 << 0,
  
  /// <summary>Layer to set bounds of a celestial body.</summary>
  /// <remarks>
  /// A very rough boundary of a planet, moon or asteroid. Used for macro objects detection.
  /// </remarks>
  SERVICE_LAYER = 1 << 10, 

  /// <summary>"Zero" level of a static structure on the surface.</summary>
  /// <remarks>E.g. a launchpad.</remarks>
  SURFACE = 1 << 15,

  /// <summary>Layer of kerbonaut models.</summary>
  KERBALS = 1 << 17,

  /// <summary>A layer for FX.</summary>
  /// <remarks>E.g. <c>PadFXReceiver</c> on the Kerbins VAB launchpad.</remarks>
  FX = 1 << 30,
};

}  // namespace
