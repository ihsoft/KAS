// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Documented analogue of IModuleInfo interface.</summary>
/// <remarks>Inherit from <see cref="IModuleInfo"/> to be able customizing module descriptions for
/// the editor. <see cref="IKSPDevModuleInfo"/> is a full equivalent except it's documented.
/// Inheriting modules from both interfaces gives better code documentation.
/// <example>
/// <code>
/// public class MyModule : PartModule, IPartModule, IModuleInfo, IKSPDevModuleInfo {
///   /// &lt;inheritdoc/&gt;
///   public override string GetInfo() {
///     return "&lt;size=20>&lt;color=#ff0000ff>&lt;b>BLAH!&lt;/b>&lt;/color>&lt;/size>";
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IKSPDevModuleInfo {
  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>Declared as virtual in <see cref="PartModule"/> and, hence, needs to be overridden.
  /// Though, it's also a part of <see cref="IModuleInfo"/>.
  /// </remarks>
  /// <returns>Rich text to show the in GUI. Regular
  /// <see href="https://docs.unity3d.com/Manual/StyledText.html">Unity rich text styles</see> are
  /// supported.
  /// <para>Be careful when using &lt;size&gt;. It specifies size of the font in pixels which is an
  /// absolute value. As of KSP v1.1.3 normal info font size is 11px but in the future versions it
  /// may change.</para>
  /// </returns>
  /// <seealso href="https://docs.unity3d.com/Manual/StyledText.html">Unity 3D: Rich text</seealso>
  string GetInfo();

  /// <summary>Returns module title to show in the editor part's details panel.</summary>
  /// <returns>Title of the module.</returns>
  string GetModuleTitle();

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  Callback<UnityEngine.Rect> GetDrawModulePanelCallback();

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  string GetPrimaryField();
}

}
