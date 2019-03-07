// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.LogUtils;
using System;
using System.Linq;
using System.Collections;

namespace KSPDev.ProcessingUtils {

/// <summary>Set of tools to check the API method arguments.</summary>
/// <remarks>
/// The public API method <i>can</i> be called with an appropriate arguments. It's always a good
/// idea to fail earlier with a clear message rather than trying to use the wrong value and crash.
/// Always check the arguments for the accepted values. The only good reason to break this rule is
/// providing a high frequency API method.
/// </remarks>
public static class ArgumentGuard {
  /// <summary>Throws if argument is null.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="ArgumentNullException">If the argument is <c>null</c>.</exception>
  public static void NotNull(object arg, string argName,
                             string message = null, object context = null) {
    if (arg == null) {
      throw new ArgumentNullException(argName, Preconditions.MakeContextError(context, message));
    }
  }

  /// <summary>Throws if string is null or empty.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="ArgumentNullException">If the argument is <c>null</c>.</exception>
  /// <exception cref="ArgumentException">If the argument is an empty string.</exception>
  public static void NotNullOrEmpty(string arg, string argName,
                                    string message = null, object context = null) {
    NotNull(arg, argName, message: message, context: context);
    if (arg == "") {
      throw new ArgumentException(
          argName,
          Preconditions.MakeContextError(context, "Argument is EMPTY: {0}", message));
    }
  }

  /// <summary>Throws if ordinary value is out of bounds.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="minValue">The minumum allowed value.</param>
  /// <param name="maxValue">The maximum allowed value.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void InRange<T>(
      T arg, string argName, T minValue, T maxValue,
      string message = null, object context = null) where T : IComparable {
    if (arg.CompareTo(minValue) < 0 || arg.CompareTo(maxValue) > 0) {
      throw new ArgumentOutOfRangeException(
          argName, arg,
          Preconditions.MakeContextError(
              context, "Not in range [{0}; {1}]. {2}", minValue, maxValue, message));
    }
  }

  /// <summary>Throws if enum value is not in the expected set.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <param name="values">The acceptable values of the enum.</param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void OneOf<T>(T arg, string argName,
                              string message = null, object context = null,
                              params T[] values) {
    if (!values.Contains(arg)) {
      throw new ArgumentOutOfRangeException(
          argName, arg,
          Preconditions.MakeContextError(
              context, "Not one of: {1}. {2}", DbgFormatter.C2S(values), message));
    }
  }

  /// <summary>Throws if enum value is not in the expected set.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="values">The acceptable values of the enum.</param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void OneOf<T>(T arg, string argName, params T[] values) {
    OneOf(arg, argName, message: null, context: null, values: values);
  }

  /// <summary>Throws if collection has not the expected number of elements.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="size">The expected collection size.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void HasSize(IList arg, string argName, int size,
                             string message = null, object context = null) {
    if (arg.Count != size) {
      throw new ArgumentOutOfRangeException(
          argName, arg,
          Preconditions.MakeContextError(
              context, "Expected collection size {0}, found {1}. {2}", size, arg.Count, message));
    }
  }

  /// <summary>Throws if collection is empty.</summary>
  /// <param name="arg">The argument value to check.</param>
  /// <param name="argName">The argument name.</param>
  /// <param name="message">An optional message to present in the error.</param>
  /// <param name="context">The optional "owner" object.</param>
  /// <exception cref="ArgumentOutOfRangeException">If the argument is an empty string.</exception>
  public static void HasElements(IList arg, string argName,
                                string message = null, object context = null) {
    if (arg.Count == 0) {
      message = string.Format(
          "Collection '{0}' is expected to be not empty: {1}", argName, message);
      throw new ArgumentOutOfRangeException(
          argName, arg,
          Preconditions.MakeContextError(
              context, "Collection is expected to be not empty. {1}", message));
    }
  }
}

}  // namespace
