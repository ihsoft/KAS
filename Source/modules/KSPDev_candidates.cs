// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

using System;
using System.Linq;

namespace KSPDev.GUIUtils {

/// <summary>
/// Localized message formatting class for a numeric value that represents a <i>speed</i>. The
/// resulted message may have a unit specification.
/// </summary>
/// <remarks>
/// <para>
/// Use it as a generic parameter when creating a <see cref="LocalizableMessage"/> descendants.
/// </para>
/// <para>
/// The class uses the unit name localizations from the stock module
/// <c>KSP.UI.Screens.Flight.SpeedDisplay</c>. In case of this module is deprecated or the tags are
/// changed, the default English values will be used for the unit names.
/// </para>
/// </remarks>
/// <include file="SpecialDocTags.xml" path="Tags/MessageTypeWithArg/*"/>
/// <include file="SpecialDocTags.xml" path="Tags/MessageArgumentType/*"/>
public sealed class SpeedType {
  /// <summary>Localization tag for the "m/s" units.</summary>
  public const string MetersPerSecondLocTag = "#autoLOC_180095";
  
  /// <summary>Localization tag for the "km/s" units.</summary>
  public const string KilometerPerSecondLocTag = "#autoLOC_180103";

  /// <summary>Localization tag for the "Mm/s" (megameter per second) units.</summary>
  public const string MegameterPerSecondLocTag = "#autoLOC_180098";

  /// <summary>Localized suffix for the "m/s" units. Scale <c>1.0</c>.</summary>
  public static readonly Message meterPerSecond = new Message(
      MetersPerSecondLocTag, defaultTemplate: "m/s",
      description: "Meter per second unit for a speed value");

  /// <summary>Localized suffix for the "km/s" untis. Scale <c>1000.0</c></summary>
  public static readonly Message kilometerPerSecond = new Message(
      KilometerPerSecondLocTag, defaultTemplate: "km/s",
      description: "Kilometer per second unit for a speed value");

  /// <summary>Localized suffix for the "Mm/s" untis. Scale <c>1000000.0</c>.</summary>
  public static readonly Message megametrPerSecond = new Message(
      MegameterPerSecondLocTag, defaultTemplate: "Mm/s",
      description: "Megameter per second unit for a speed value");

  /// <summary>A wrapped numeric value.</summary>
  /// <remarks>This is the original non-rounded and unscaled value.</remarks>
  public readonly double value;

  /// <summary>Constructs an object from a numeric value.</summary>
  /// <param name="value">The numeric value in meters.</param>
  /// <seealso cref="Format"/>
  public SpeedType(double value) {
    this.value = value;
  }

  /// <summary>Converts a numeric value into a type object.</summary>
  /// <param name="value">The numeric value to convert.</param>
  /// <returns>An object.</returns>
  public static implicit operator SpeedType(double value) {
    return new SpeedType(value);
  }

  /// <summary>Converts a type object into a numeric value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator double(SpeedType obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string with a unit specification.</summary>
  /// <remarks>
  /// The method tries to keep the resulted string meaningful and as short as possible. For this
  /// reason the big values may be scaled down and/or rounded.
  /// </remarks>
  /// <param name="value">The unscaled numeric value to format.</param>
  /// <param name="scale">
  /// The fixed scale to apply to the value before formatting. The formatting method can uderstand
  /// only few scales:
  /// <list type="bullet">
  /// <item>Megameters per second: scale=<c>1.0e+6</c>.</item>
  /// <item>Kilometers per second: scale=<c>1.0e+3</c>.</item>
  /// <item>Meters per second: scale=<c>1.0</c>. <i>It's the base speed unit in the game.</i></item>
  /// </list>
  /// <para>
  /// The unknown scales will be rounded <i>up</i> to the closest known scale. If this parameter
  /// is omitted, then the best scale for the value will be choosen automatically.
  /// </para>
  /// </param>
  /// <param name="format">
  /// The specific numeric number format to use. If this parameter is specified, then the method
  /// doesn't try to guess the right scale. Instead, it uses either the provided
  /// <paramref name="scale"/>, or <c>1.0</c> if nothing is provided. If the format is not
  /// specified, then it's choosen basing on the scale.
  /// </param>
  /// <returns>A formatted and localized string</returns>
  public static string Format(double value, double? scale = null, string format = null) {
    // Detect the scale, and scale the value.
    string units;
    double scaledValue;
    if (format != null && !scale.HasValue) {
      scale = 1.0;  // No scale detection.
    }
    if (!scale.HasValue) {
      // Auto detect the best scale.
      if (value < 1000.0) {
        scale = 1.0; 
      } else if (value < 1000000.0) {
        scale = 1000.0; 
      } else {
        scale = 1000000.0;
      }
    }
    if (scale <= 1.0) {
      scaledValue = value;
      units = meterPerSecond;
    } else if (scale <= 1000.0) {
      scaledValue = value / 1000.0;
      units = kilometerPerSecond;
    } else {
      scaledValue = value / 1000000.0;
      units = megametrPerSecond;
    }
    if (format != null) {
      return scaledValue.ToString(format) + units;
    }
    return CompactNumberType.Format(scaledValue) + units;
  }

  /// <summary>Returns a string formatted as a human friendly distance specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

}  // namespace
