// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using System;
using System.Linq;
using System.Collections;

namespace KSPDev.ProcessingUtils {

/// <summary>Set of tools to check various preconditions during the code execution.</summary>
/// <remarks>
/// This class offers single line checkers for the case when a critical "go"/"no go" decision needs
/// to be made. Instead of flooding your code with <c>if/throw</c> conditions, simply call a
/// precondition check. If the condition met, then execution continues. Otherwise, an exception with
/// a clear message is thrown.
/// </remarks>
public static class Preconditions {
  /// <summary>Throws if value is null.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="NullReferenceException">If the argument is <c>null</c>.</exception>
  public static void NotNull(object arg, string message = null, object context = null) {
    if (arg == null) {
      throw new NullReferenceException(MakeContextError(context, message));
    }
  }

  /// <summary>Throws if string is null or empty.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="NullReferenceException">If the value is <c>null</c>.</exception>
  /// <exception cref="InvalidOperationException">If the value is an empty string.</exception>
  public static void NotNullOrEmpty(string arg, string message = null, object context = null) {
    NotNull(arg, message: message, context: context);
    if (arg == "") {
      message = message ?? "Value is an empty string";
      throw new InvalidOperationException(MakeContextError(context, message));
    }
  }

  /// <summary>Throws if value form a config node is null or empty string.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="path">The path to the value or node.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="NullReferenceException">If the value is <c>null</c>.</exception>
  /// <exception cref="InvalidOperationException">If the value is an empty string.</exception>
  public static void ConfValueExists(object arg, string path, object context = null) {
    if (context is ConfigNode) {
      var node = context as ConfigNode;
      context = node.GetValue("name") ?? "ConfigNode#" + node.name;
    } else if (context is UrlDir.UrlConfig) {
      var config = context as UrlDir.UrlConfig;
      context = "Config#" + config.url;
    }
    if (arg is string) {
      NotNullOrEmpty(arg as string, message: "No value at '" + path + "'", context: context);
    } else {
      NotNull(arg, message: "No node value at '" + path + "'", context: context);
    }
  }

  /// <summary>Throws if collection has less elements than required.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="minSize">The minimum collection size.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="InvalidOperationException">If the collection has less elements.</exception>
  public static void MinElements(
      IList arg, int minSize, string message = null, object context = null) {
    NotNull(arg, message: "Collection instance must not be null", context: context);
    if (arg.Count < minSize) {
      message = string.Format("Collection must have at least {0} elemets, it had {1}: {2}",
                              minSize, arg.Count, message ?? "");
      throw new InvalidOperationException(MakeContextError(context, message));
    }
  }

  /// <summary>Throws if enum value is not in the expected set.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <param name="values">The acceptable values of the enum.</param>
  /// <exception cref="InvalidOperationException">
  /// If the value is not one of the specified.
  /// </exception>
  public static void OneOf<T>(T arg, T[] values, string message = null, object context = null) {
    if (!values.Contains(arg)) {
      throw new InvalidOperationException(
          Preconditions.MakeContextError(
              context, "Not one of: {1}. {2}", DbgFormatter.C2S(values), message));
    }
  }

  /// <summary>Throws if collection has not the expected number of elements.</summary>
  /// <param name="arg">The value to check.</param>
  /// <param name="size">The expected collection size.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="InvalidOperationException">
  /// If the value has different number of elements.
  /// </exception>
  public static void HasSize(IList arg, int size, string message = null, object context = null) {
    if (arg.Count != size) {
      throw new InvalidOperationException(
          Preconditions.MakeContextError(
              context, "Expected collection size {0}, found {1}. {2}", size, arg.Count, message));
    }
  }

  /// <summary>Makes error text with respect to the context.</summary>
  /// <param name="context">
  /// The context or <c>null</c>. Context can be any string (keep it short) or an object.
  /// </param>
  /// <param name="message">The message to present.</param>
  /// <returns>The complete error string.</returns>
  public static string MakeContextError(object context, string message) {
    var res = "";
    if (context != null) {
      if (!string.IsNullOrEmpty(context as string)) {
        res = "[" + context + "]";
      } else {
        res = DebugEx.ObjectToString(context).ToString();
      }
    }
    if (!string.IsNullOrEmpty(message)) {
      res += (res != "" ? " " : "") + message;
    }
    return res;
  }

  /// <summary>Makes error text with respect to the context.</summary>
  /// <param name="context">
  /// The context or <c>null</c>. Context can be any string (keep it short) or an object.
  /// </param>
  /// <param name="message">The message to present.</param>
  /// <param name="args">The arguments to format the message.</param>
  /// <returns>The complete error string.</returns>
  public static string MakeContextError(object context, string message, params object[] args) {
    return MakeContextError(context, string.Format(message, args));
  }
}

}  // namespace
