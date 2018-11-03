// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

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

}  // namespace
