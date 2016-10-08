// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Documented analogue of IModuleInfo interface.</summary>
/// <remarks>
/// Inherit from <see cref="IModuleInfo"/> to be able customizing module descriptions for the
/// editor. <see cref="IKSPDevModuleInfo"/> is a full equivalent except it's documented. Inheriting
/// modules from both interfaces gives better code documentation.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IPartModule, IModuleInfo, IKSPDevModuleInfo {
///   /// <inheritdoc/>
///   public override string GetInfo() {
///     return "<size=20><color=#ff0000ff><b>BLAH!</b></color></size>";
///   }
/// }
/// ]]></code>
/// </example>
public interface IKSPDevModuleInfo {
  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>
  /// Declared as virtual in <see cref="PartModule"/> and, hence, almost always needs to be
  /// overridden. Though, it's also a part of <see cref="IModuleInfo"/>.
  /// </remarks>
  /// <returns>
  /// Rich text to show the in GUI. Regular Unity rich text styles are supported.
  /// <para>
  /// Be careful when using &lt;size&gt;. It specifies size of the font in pixels which is an
  /// absolute value. As of KSP v1.1.3 normal info font size is 11px but in the future versions it
  /// may change.
  /// </para>
  /// </returns>
  /// <seealso href="https://docs.unity3d.com/Manual/StyledText.html">Unity 3D: Rich text</seealso>
  string GetInfo();

  /// <summary>Returns module title to show in the editor part's details panel.</summary>
  /// <returns>Title of the module.</returns>
  string GetModuleTitle();

  /// <summary>Returns a method delegate to draw a custom panel.</summary>
  /// <returns>Delegate or <c>null</c> if not necessary.</returns>
  Callback<UnityEngine.Rect> GetDrawModulePanelCallback();

  /// <summary>Return a string to be displayed in the main information box on the tooltip.</summary>
  /// <returns>String or <c>null</c> if nothing is that important to be up there.</returns>
  string GetPrimaryField();
}

}  // namespace
