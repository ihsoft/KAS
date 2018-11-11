// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

namespace KSPDev.GUIUtils {

/// <summary>GUI text field to set a value yo one of the pre-defined options.</summary>
/// <remarks>
/// <para>
/// It's a cheap alternative to the dropdown list. Instead of presenting all the options in a list,
/// the control simply iterates backward and forward between teh values.
/// </para>
/// <para>Note, that this control must only be used in the <c>GUILayout</c> dialogs.</para>
/// </remarks>
public class GUILayoutSwitchField<T> where T : struct, IConvertible {
  readonly T[] options;
  readonly Func<T, string> toStringConverter;
  readonly bool useOwnLayout;
  readonly GUIStyle centeredTextStyle;

  /// <summary>Creates the control.</summary>
  /// <param name="options">The options to iterate thru.</param>
  /// <param name="toStringConverter">The method to use to serialize value to string.</param>
  /// <param name="useOwnLayout">
  /// If <c>false</c>, then the control will start own horizontal section to align the input field
  /// and buttons.
  /// </param>
  public GUILayoutSwitchField(
      T[] options,
      Func<T, string> toStringConverter = null,
      bool useOwnLayout = true) {
    this.options = options;
    this.toStringConverter = toStringConverter != null ? toStringConverter : x => x.ToString();
    this.useOwnLayout = useOwnLayout;
    this.centeredTextStyle = new GUIStyle(GUI.skin.label);
    this.centeredTextStyle.alignment = TextAnchor.MiddleCenter;
  }

  /// <summary>Method to have the control rendered ion GUI.</summary>
  /// <remarks>Call it from the <c>OnGUI</c> callback.</remarks>
  /// <param name="value">The source value.</param>
  /// <param name="ownLayoutStyle">The style to use when making own horizontal layout.</param>
  /// <param name="valueFieldOptions">
  /// The GUI options to apply to the field showing the current value.
  /// </param>
  /// <param name="onValueSet">The method to call when a value is applied.</param>
  /// <param name="actionsList">The action list to use to submit the value update job to.</param>
  /// <returns>
  /// The new value. It's the same as <paramref name="value"/> until user changes it.
  /// </returns>
  public T UpdateFrame(T value, GUIStyle ownLayoutStyle, GUILayoutOption[] valueFieldOptions,
                       Action<T> onValueSet = null, GuiActionsList actionsList = null) {
    if (useOwnLayout) {
      GUILayout.BeginHorizontal(ownLayoutStyle);
    }
    var newValue = value;
    var idx = 0;
    if (GUILayout.Button("<", GUILayout.ExpandWidth(false))) {
      idx = -1;
    }
    GUILayout.Label(toStringConverter(value), centeredTextStyle, valueFieldOptions);
    if (GUILayout.Button(">", GUILayout.ExpandWidth(false))) {
      idx = 1;
    }
    if (useOwnLayout) {
      GUILayout.EndHorizontal();
    }
    if (idx != 0) {
      var pos = options.IndexOf(value);
      if (pos == -1) {
        pos = 0;
      }
      newValue = options[(options.Length + pos + idx) % options.Length];
    }
    if (!newValue.Equals(value) && onValueSet != null) {
      if (actionsList == null) {
        onValueSet(newValue);
      } else {
        actionsList.Add(() => onValueSet(newValue));
      }
    }
    return newValue;
  }
}

}  // namespace
