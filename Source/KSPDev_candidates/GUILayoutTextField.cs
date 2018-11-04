// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

namespace KSPDev.GUIUtils {

/// <summary>GUI text field to edit and set complex types.</summary>
/// <remarks>
/// <para>
/// Use it when the type is more comples than a single value. E.g. a vector. Thos GUI field needs to
/// have methods to parse/serialize the value from/to string. The actual value is untouched until
/// user explictily selects set or reset in the control.
/// </para>
/// <para>Note, that this control must only be used in the <c>GUILayout</c> dialogs.</para>
/// </remarks>
public class GUILayoutTextField<T> {
  readonly Func<string, T> fromStringConverter;
  readonly Func<T, string> toStringConverter;
  readonly bool useOwnLayout;

  string currentTxt;
  T currentValue;

  /// <summary>Creates the control.</summary>
  /// <param name="toStringConverter">The method to use to serialize value to string.</param>
  /// <param name="fromStringConverter">The method to parse the value from string.</param>
  /// <param name="useOwnLayout">
  /// If <c>false</c>, then the control will start own horizontal section to align the input field
  /// and buttons.
  /// </param>
  public GUILayoutTextField(
      Func<T, string> toStringConverter, Func<string, T> fromStringConverter,
      bool useOwnLayout = true) {
    this.fromStringConverter = fromStringConverter;
    this.toStringConverter = toStringConverter;
    this.useOwnLayout = useOwnLayout;
  }

  /// <summary>Clears any previous state accumulated.</summary>
  public void Reset() {
    currentTxt = null;
  }

  /// <summary>Method to have the control rendered ion GUI.</summary>
  /// <remarks>Call it from the <c>OnGUI</c> callback.</remarks>
  /// <param name="value">The source value.</param>
  /// <param name="ownLayoutStyle">The style to use when making own horizontal layout.</param>
  /// <param name="textfieldOptions">The options to apply to the input field.</param>
  /// <param name="onValueSet">The method to call when a value is applied.</param>
  /// <param name="actionsList">The action list to use to submit the value update job to.</param>
  /// <returns>
  /// The new value. It's the same as <paramref name="value"/> until user explicitly chose to set
  /// the value.
  /// </returns>
  public T UpdateFrame(T value, GUIStyle ownLayoutStyle, GUILayoutOption[] textfieldOptions,
                       Action<T> onValueSet = null, GuiActionsList actionsList = null) {
    if (useOwnLayout) {
      GUILayout.BeginHorizontal(ownLayoutStyle);
    }
    
    var valueTxt = toStringConverter(value);
    currentTxt = currentTxt ?? valueTxt;
    var changed = currentTxt != valueTxt;
    bool validValue = true;
    try {
      currentValue = fromStringConverter(currentTxt);
    } catch (Exception) {
      currentValue = default(T);
      validValue = false;
    }
    using (new GuiColorScope(contentColor: validValue ? Color.white : Color.red)) {
      currentTxt = GUILayout.TextField(changed ? currentTxt : valueTxt, textfieldOptions);
    }
    using (new GuiEnabledStateScope(changed)) {
      using (new GuiEnabledStateScope(changed && validValue)) {
        if (GUILayout.Button("S", GUILayout.ExpandWidth(false))) {
          value = currentValue;
          currentTxt = toStringConverter(value);
          if (onValueSet != null) {
            if (actionsList == null) {
              onValueSet(currentValue);
            } else {
              actionsList.Add(() => onValueSet(currentValue));
            }
          }
        }
      }
      if (GUILayout.Button("C", GUILayout.ExpandWidth(false))) {
        currentTxt = valueTxt;
      }
    }
    if (useOwnLayout) {
      GUILayout.EndHorizontal();
    }
    return value;
  }
}

/// <summary>GUI field to modify a float value.</summary>
/// <seealso cref="GUILayoutTextField&lt;T&gt;"/>
public class GUILayoutFloatField : GUILayoutTextField<float> {
  /// <summary>Creates the control.</summary>
  /// <param name="useOwnLayout">
  /// If <c>false</c>, then the control will start own horizontal section to align the input field
  /// and buttons.
  /// </param>
  public GUILayoutFloatField(bool useOwnLayout = true)
      : base(v => string.Format("{0}", v), s => float.Parse(s), useOwnLayout) {
  }
}

/// <summary>GUI field to modify a double value.</summary>
/// <seealso cref="GUILayoutTextField&lt;T&gt;"/>
public class GUILayoutDoubleField : GUILayoutTextField<double> {
  /// <summary>Creates the control.</summary>
  /// <param name="useOwnLayout">
  /// If <c>false</c>, then the control will start own horizontal section to align the input field
  /// and buttons.
  /// </param>
  public GUILayoutDoubleField(bool useOwnLayout = true)
      : base(v => string.Format("{0}", v), s => double.Parse(s), useOwnLayout) {
  }
}

/// <summary>GUI field to modify a integer value.</summary>
/// <seealso cref="GUILayoutTextField&lt;T&gt;"/>
public class GUILayoutIntegerField : GUILayoutTextField<int> {
  /// <summary>Creates the control.</summary>
  /// <param name="useOwnLayout">
  /// If <c>false</c>, then the control will start own horizontal section to align the input field
  /// and buttons.
  /// </param>
  public GUILayoutIntegerField(bool useOwnLayout = true)
      : base(v => string.Format("{0}", v), s => int.Parse(s), useOwnLayout) {
  }
}

/// <summary>GUI field to modify a string value.</summary>
/// <seealso cref="GUILayoutTextField&lt;T&gt;"/>
public class GUILayoutStringField : GUILayoutTextField<string> {
  /// <summary>Creates the control.</summary>
  /// <param name="useOwnLayout">
  /// If <c>false</c>, then the control will start own horizontal section to align the input field
  /// and buttons.
  /// </param>
  public GUILayoutStringField(bool useOwnLayout = true)
      : base(v => v, s => s, useOwnLayout) {
  }
}

}  // namespace
