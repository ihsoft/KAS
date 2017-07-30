// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

using KSPDev.LogUtils;
using KSPDev.Types;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPDev.ModelUtils {

/// <summary>Helper methods to align transformations relative to each other.</summary>
public static class AlignTransforms2 {
  /// <summary>
  /// Aligns the source node so that it's located at the target, and the source and target are
  /// "looking" in the same direction.
  /// </summary>
  /// <remarks>
  /// The object's "look" direction is a <see cref="Transform.forward"/> direction. The resulted
  /// <see cref="Transform.up"/> direction of the source will be the same as on the target.
  /// </remarks>
  /// <param name="source">The node to align.</param>
  /// <param name="sourceChild">The child node of the source to use as the align point.</param>
  /// <param name="target">The target node to align with.</param>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Transform']/*"/>
  public static void Align(Transform source, Transform sourceChild, Transform target) {
    source.rotation = source.rotation.Inverse() * sourceChild.rotation;
    source.position = source.position - (sourceChild.position - target.position);
  }
}

}  // namespace

namespace KSPDev.Extensions {

/// FIXME: move to extensions &amp; add local methods to PosAndRot
public static class PosAndRotExtensions {

  /// <summary>
  /// Transforms a pos&amp;rot object from world space to local space. The opposite to
  /// <see cref="TransformPosAndRot"/>.
  /// </summary>
  /// <param name="node">The node to use as a parent.</param>
  /// <param name="posAndRot">The object in world space.</param>
  /// <returns>A new pos&amp;rot object in the local space.</returns>
  public static PosAndRot InverseTransformPosAndRot(this Transform node, PosAndRot posAndRot) {
    var inverseRot = node.rotation.Inverse();
    return new PosAndRot(inverseRot * (posAndRot.pos - node.position),
                         (inverseRot * posAndRot.rot).eulerAngles);
  }

  /// <summary>
  /// Transforms a pos&amp;rot object from local space to world space. The opposite to
  /// <see cref="InverseTransformPosAndRot"/>.
  /// </summary>
  /// <param name="node">The node to use as a parent.</param>
  /// <param name="posAndRot">The object in local space.</param>
  /// <returns>A new pos&amp;rot object in the wold space.</returns>
  public static PosAndRot TransformPosAndRot(this Transform node, PosAndRot posAndRot) {
    return new PosAndRot(node.position + node.rotation * posAndRot.pos,
                         (node.rotation * posAndRot.rot).eulerAngles);
  }
}

}  // namespace

namespace KSPDev.GUIUtils {

///FIXME: docs
public sealed class KeyboardEventType {
  /// <summary>A wrapped event value.</summary>
  public readonly Event value;

  /// <summary>Constructs an object from an event.</summary>
  /// <param name="value">The keyboard event.</param>
  /// <seealso cref="Format"/>
  //FIXME examples
  public KeyboardEventType(Event value) {
    this.value = value;
  }

  /// <summary>Converts a numeric value into a type object.</summary>
  /// <param name="value">The event value to convert.</param>
  /// <returns>An object.</returns>
  public static implicit operator KeyboardEventType(Event value) {
    return new KeyboardEventType(value);
  }

  /// <summary>Converts a type object into an event value.</summary>
  /// <param name="obj">The object type to convert.</param>
  /// <returns>A numeric value.</returns>
  public static implicit operator Event(KeyboardEventType obj) {
    return obj.value;
  }

  /// <summary>Formats the value into a human friendly string.</summary>
  /// <param name="value">The keyboard event value to format.</param>
  /// <returns>A formatted and localized string</returns>
  //FIXME: examples
  public static string Format(Event value) {
    if (value.type != EventType.KeyDown) {
      return "<non-keyboard event>";
    }
    var parts = new List<string>();
    if ((value.modifiers & EventModifiers.Control) != 0) {
      parts.Add("Ctrl");
    }
    if ((value.modifiers & EventModifiers.Shift) != 0) {
      parts.Add("Shift");
    }
    if ((value.modifiers & EventModifiers.Alt) != 0) {
      parts.Add("Alt");
    }
    if ((value.modifiers & EventModifiers.Command) != 0) {
      parts.Add("Cmd");
    }
    parts.Add(value.keyCode.ToString());
    return string.Join("+", parts.ToArray());
  }

  /// <summary>Returns a string formatted as a human friendly key specification.</summary>
  /// <returns>A string representing the value.</returns>
  /// <seealso cref="Format"/>
  public override string ToString() {
    return Format(value);
  }
}

}  // namespace

namespace KSPDev.PartUtils {

/// <summary>
/// Utility class to deals with the attributed fields and methods of the KPS part modules.
/// </summary>
public static class PartModuleUtils {
  /// <summary>Returns an event for the requested method.</summary>
  /// <remarks>
  /// This method requests a KPS event of the part's module by its signature instead of a string
  /// literal name. The same result could be achieved by accessing the <c>Events</c> field of the
  /// <c>PartModule</c> object. However, in case of using a string literal the refactoring and the
  /// source tracking tools won't be able to track the reference. The main goal of this method is to
  /// provide a compile time checking mechanism for the cases when the exact method is known at the
  /// compile time (e.g. in the class descendants).
  /// </remarks>
  /// <param name="partModule">The module to get the event for.</param>
  /// <param name="eventFn">The signature of the event in scope of the module.</param>
  /// <returns>An event, or <c>null</c> if nothing found.</returns>
  //FIXME: example + refs to the module and events 
  public static BaseEvent GetModuleEvent(PartModule partModule, Action eventFn) {
    return partModule.Events[eventFn.Method.Name];
  }

  /// <summary>Applies a setup function on a KSP part module event.</summary>
  /// <param name="partModule">The module to find the event in.</param>
  /// <param name="eventFn">The event's method signature.</param>
  /// <param name="actionFn">The action to apply to the event if the one is found.</param>
  /// <returns>
  /// <c>true</c> if the event was found and the action was applied, <c>false</c> otherwise.
  /// </returns>
  public static bool SetupModuleEvent(
      PartModule partModule, Action eventFn, Action<BaseEvent> actionFn) {
    var moduleEvent = partModule.Events[eventFn.Method.Name];
    if (moduleEvent == null) {
      HostedDebugLog.Error(partModule, "Cannot find event: {0}", eventFn.Method.Name);
      return false;
    }
    actionFn.Invoke(moduleEvent);
    return true;
  }
}
  
}  // namespace
