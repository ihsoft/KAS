// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

using System;
using System.Linq;
using KSPDev.GUIUtils;

namespace KASAPIv1.GUIUtils {

/// <summary>Localized message formatting class for a part.</summary>
/// <remarks>
/// This type is similar to the other GUI formatting types declared in KSPDev.GUIUtils. Use it as a
/// generic parameter when creating a <c>KSPDev.GUIUtils.LocalizableMessage</c>
/// </remarks>
public sealed class PartType {
  /// <summary>A wrapped part.</summary>
  public readonly Part value;

  /// <summary>Constructs an object from a part.</summary>
  /// <param name="value">The part to represent.</param>
  /// <seealso cref="Format"/>
  public PartType(Part value) {
    this.value = value;
  }

  /// <summary>Coverts a part value into a type object.</summary>
  /// <param name="value">The part to convert.</param>
  /// <returns>An object.</returns>
  public static implicit operator PartType(Part value) {
    return new PartType(value);
  }

  /// <summary>Converts a type object into a part value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator Part(PartType obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string.</summary>
  /// <param name="value">The part to format.</param>
  public static string Format(Part value) {
    return value.partInfo.title;
  }

  /// <summary>Returns a string formatted as a human friendly volume specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

/// <summary>
/// Localized message formatting class for a numeric value that represents a <i>percentage</i> with
/// up to 2 digits after the dot regardless to the value scale.
/// </summary>
/// <remarks>
/// Use it as a generic parameter when creating a <see cref="LocalizableMessage"/> descendants.
/// </remarks>
/// FIXME: Provide examples and KSPDev style docs.
public sealed class PercentTypeTwoDigits {
  /// <summary>Suffix for the "percent" units (%).</summary>
  public const string unitName = PercentType.unitName;

  /// <summary>A wrapped numeric value.</summary>
  /// <remarks>This is the original non-rounded and unscaled value.</remarks>
  public readonly double value;

  /// <summary>Constructs a precentage type object.</summary>
  /// <param name="value">
  /// The numeric value which defines the ratio. Value <c>1.0</c> is <c>100%</c>.
  /// </param>
  /// <seealso cref="Format"/>
  public PercentTypeTwoDigits(double value) {
    this.value = value;
  }

  /// <summary>Coverts a numeric value into a type object.</summary>
  /// <param name="value">The numeric value to convert.</param>
  /// <returns>A type object.</returns>
  public static implicit operator PercentTypeTwoDigits(double value) {
    return new PercentTypeTwoDigits(value);
  }

  /// <summary>Converts a type object into a numeric value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator double(PercentTypeTwoDigits obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string with a unit specification.</summary>
  /// <remarks>
  /// The method tries to keep the resulted string meaningful and as short as possible. For this
  /// reason the big values may be scaled down and/or rounded.
  /// </remarks>
  /// <param name="value">The numeric value to format.</param>
  /// <param name="format">
  /// The specific float number format to use. If the format is not specified, then it's choosen
  /// basing on the value. Note, that the <paramref name="value"/> is multiplied by <c>100.0</c>
  /// before formatting. 
  /// </param>
  /// <returns>A formatted and localized string</returns>
  /// <example><code source="Examples/GUIUtils/TypeFormatters/PercentType-Examples.cs" region="PercentTypeDemo2_FormatDefault"/></example>
  /// <example><code source="Examples/GUIUtils/TypeFormatters/PercentType-Examples.cs" region="PercentTypeDemo2_FormatFixed"/></example>
  public static string Format(double value, string format = null) {
    var scaledValue = value * 100.0;
    if (format != null) {
      return scaledValue.ToString(format) + unitName;
    }
    if (Math.Abs(scaledValue) < double.Epsilon) {
      return "0" + unitName;  // Zero is zero.
    }
    return scaledValue.ToString("0.##") + unitName;
  }

  /// <summary>Returns a string formatted as a human friendly percentage specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

}  // namespace
