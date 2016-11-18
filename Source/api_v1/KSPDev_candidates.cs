// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace KSPDev.GUIUtils {

/// <summary>A class to wrap a UI string for a boolean value.</summary>
/// <remarks>
/// <para>
/// When string needs to be presented use <see cref="Format"/> to make the parameter substitute.
/// </para>
/// <para>
/// In the future it may support localization but for now it's only a convinience wrapper.
/// </para>
/// </remarks>
/// <example>
/// Instead of presenting hardcoded strings on UI move them all into a special section, and assign
/// to fields of type <c>MessageBoolValue</c>.
/// <code><![CDATA[
/// class MyMod : MonoBehaviour {
///   static readonly MessageBoolValue SwitchMsg = new MessageBoolValue("ON", "OFF");
///   static readonly MessageBoolValue StateMsg = new MessageBoolValue("Enabled", "Disabled");
///
///   void Awake() {
///     Debug.LogFormat("Localized: {0}", SwitchMsg.Format(true));  // ON
///     Debug.LogFormat("Localized: {0}", StateMsg.Format(false));  // Disabled
///   }
/// }
/// ]]></code>
/// <para>
/// Note, that it's OK to name such members as constants in spite of they are not constants by
/// the C# language semantics. I.e. instead of <c>myMessage</c> you spell <c>MyMessage</c> to
/// highlight the fact it won't (and must not) change from the code.
/// </para>
/// </example>
public class MessageBoolValue {
  readonly string positiveStr;
  readonly string negativeStr;
  
  /// <summary>Creates a message.</summary>
  /// <param name="positiveStr">Message string for <c>true</c> value.</param>
  /// <param name="negativeStr">Message string for <c>false</c> value.</param>
  public MessageBoolValue(string positiveStr, string negativeStr) {
    this.positiveStr = positiveStr;
    this.negativeStr = negativeStr;
  }

  /// <summary>Formats message string with the provided arguments.</summary>
  /// <param name="arg1">An argument to substitute.</param>
  /// <returns>Complete message string.</returns>
  public string Format(bool arg1) {
    return arg1 ? positiveStr : negativeStr;
  }
}

/// <summary>A class to wrap a UI string for an enum value.</summary>
/// <remarks>
/// <para>
/// When string needs to be presented use <see cref="Format"/> to make the parameter substitute.
/// </para>
/// <para>
/// In the future it may support localization but for now it's only a convinience wrapper.
/// </para>
/// </remarks>
/// <example>
/// Instead of doing switches when an enum value should be presented on UI just define a message
/// that declares a map between values and their UI representations. You don't need specify every
/// single value in the map, there is an option to set a UI string for unknown value.  
/// <code><![CDATA[
/// class MyMod : MonoBehaviour {
///   enum MyEnum {
///     Disabled,
///     Enabled,
///     UnusedValue1,
///     UnusedValue2,
///     UnusedValue3,
///   }
///
///   // Lookup with custom value for an unknown key.
///   static readonly MessageEnumValue<MyEnum> Msg1 =
///       new MessageEnumValue<MyEnum>("UNKNOWN") {
///         {MyEnum.Enabled, "ENABLED"},
///         {MyEnum.Disabled, "DISABLED"},
///       };
///
///   // Default lookup.
///   static readonly MessageEnumValue<MyEnum> Msg2 =
///       new MessageEnumValue<MyEnum>() {
///         {MyEnum.Enabled, "ENABLED"},
///         {MyEnum.Disabled, "DISABLED"},
///         {MyEnum.UnusedValue1, "Value1"},
///         {MyEnum.UnusedValue2, "Value2"},
///       };
///
///   void Awake() {
///     Debug.LogFormat("Localized: {0}", Msg1.Format(MyEnum.Disabled));  // DISABLED
///     Debug.LogFormat("Localized: {0}", Msg1.Format(MyEnum.UnusedValue1));  // UNKNOWN
///
///     Debug.LogFormat("Localized: {0}", Msg2.Format(MyEnum.UnusedValue1));  // Value1
///     Debug.LogFormat("Localized: {0}", Msg2.Format(MyEnum.UnusedValue2));  // Value2
///     Debug.LogFormat("Localized: {0}", Msg2.Format(MyEnum.UnusedValue3));  // "" (null)
///   }
/// }
/// ]]></code>
/// <para>
/// Note, that it's OK to name such members as constants in spite of they are not constants by
/// the C# language semantics. I.e. instead of <c>myMessage</c> you spell <c>MyMessage</c> to
/// highlight the fact it won't (and must not) change from the code.
/// </para>
/// </example>
public class MessageEnumValue<T> : IEnumerable<KeyValuePair<T, string>> {
  readonly Dictionary<T, string> strings;
  readonly string unknownKeyValue;

  /// <summary>Creates an empty message with a default value for unknow entries.</summary>
  /// <param name="unknownKeyValue">
  /// Value to return if lookup dictionary doesn't have the requested key.
  /// </param>
  public MessageEnumValue(string unknownKeyValue = null) {
    this.strings = new Dictionary<T, string>();
    this.unknownKeyValue = unknownKeyValue;
  }

  /// <inheritdoc/>
  public IEnumerator<KeyValuePair<T, string>> GetEnumerator() {
    return strings.GetEnumerator();
  }

  /// <summary>Adds a new lookup for the key.</summary>
  /// <param name="key">Unique key.</param>
  /// <param name="value">GUI string for the key.</param>
  public void Add(T key, string value) {
    strings.Add(key, value);
  }

  /// <summary>Formats message string with the provided arguments.</summary>
  /// <param name="arg1">An argument to substitute.</param>
  /// <returns>Complete message string.</returns>
  public string Format(T arg1) {
    string value;
    return strings.TryGetValue(arg1, out value) ? value : unknownKeyValue;
  }

  IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
  }
}

}  // namespace KSPDev.GUIUtils

namespace KSPDev.KSPInterfaces {

/// <summary>Interface for modules that need handling physics.</summary>
/// <remarks>
/// Events of this inteface are triggered by Unity engine via reflections. It's not required for the
/// module to implement the interface to be notified but by implementing it the code becomes more
/// consistent and less error prone.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IsPhysicalObject {
///   /// <inheritdoc/>
///   public void FixedUpdate() {
///     // Do physics stuff.
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.FixedUpdate.html">
/// Unity 3D: FixedUpdate</seealso>
public interface IsPhysicalObject {
  /// <summary>Notifies that fixed framerate frame is being handled.</summary>
  /// <remarks>
  /// This method is called by Unity via reflections, so it's not required to implement the
  /// interface. Though, it's a good idea to implement this interface in objects/modules that need
  /// physics updates to make code more readable.
  /// </remarks>
  void FixedUpdate();
}

}  // KSPDev.KSPInterfaces

namespace KSPDev.LogUtils {

  /// <summary>A set of tools to format various game enities for debugging purposes.</summary>
  public static class DbgFormatter {
    /// <summary>Returns a user friendly unique description of the part.</summary>
    /// <param name="p">Part to get ID string for.</param>
    /// <returns>ID string.</returns>
    public static string PartId(Part p) {
      return p != null ? string.Format("{0} (id={1})", p.name, p.flightID) : "NULL";
    }

    /// <summary>Flatterns collection items into a comma separated string.</summary>
    /// <remarks>This method's name is a shorthand for "Collection-To-String". Given a collection
    /// (e.g. list, set, or anything else implementing <c>IEnumarable</c>) this method transforms it
    /// into a human readable string.</remarks>
    /// <param name="collection">A collection to represent as a string.</param>
    /// <param name="predicate">A predicate to use to extract string representation of an item. If
    /// <c>null</c> then standard <c>ToString()</c> is used.</param>
    /// <returns>Human readable form of the collection.</returns>
    /// <typeparam name="TSource">Collection's item type.</typeparam>
    public static string C2S<TSource>(
        IEnumerable<TSource> collection, Func<TSource, string> predicate = null) {
      var res = new StringBuilder();
      var firstItem = true;
      foreach (var item in collection) {
        if (firstItem) {
          firstItem = false;
        } else {
          res.Append(',');
        }
        if (predicate != null) {
          res.Append(predicate(item));
        } else {
          res.Append(item.ToString());
        }
      }
      return res.ToString();
    }
  }

}  // namespace
