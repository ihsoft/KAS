// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.GUIUtils {

/// <summary>Helper to format various values.</summary>
public static class Formatter {
  /// <summary>Formats a number that has special meaning for a predefined value.</summary>
  /// <param name="number">Number to format.</param>
  /// <param name="fmt">Format string for the number.</param>
  /// <param name="specialValue">Special value of the number.</param>
  /// <param name="specialString">String to show for the special value.</param>
  /// <returns></returns>
  public static string SpecialValue(
      float number, string fmt, float specialValue, string specialString) {
    return UnityEngine.Mathf.Approximately(number, specialValue)
        ? specialString
        : string.Format(fmt, number);
  }

  /// <summary>Formats a number that has special meaning for a predefined value.</summary>
  /// <param name="number">Number to format.</param>
  /// <param name="fmt">Format string for the number.</param>
  /// <param name="specialValue">Special value of the number.</param>
  /// <param name="specialString">String to show for the special value.</param>
  /// <returns></returns>
  public static string SpecialValue(
      int number, string fmt, int specialValue, string specialString) {
    return number == specialValue ? specialString : string.Format(fmt, number);
  }
}

}  // namespace
