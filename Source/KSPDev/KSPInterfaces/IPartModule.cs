// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Interface for KSP part module.</summary>
/// <remarks>
/// Naturally, KSP doesn't declare any part module interface (unfortunately), and all modder's
/// modules just inherit from <see cref="PartModule"/>. This interface is introduced for the better
/// OOP approach. It reveals methods that a regular module can override, and provides documentation
/// for each of them.
/// <para>
/// Some methods of the module interface look familiar to the ones from Unity but they are not
/// behaving in the same way in every scene. Moreover, not all methods get called in every scene.
/// </para>
///
/// <para>In the <i>loading scene</i> the callbacks are executed in the following order:</para>
/// <list type="table">
/// <item>
/// <term><see cref="OnAwake"/></term>
/// <description>
/// Notifies about creating new module. If it's a clone operation then all <see cref="KSPField"/>
/// annotated fields have values from the part's config. Otherwise, all the fields are in the
/// initial states.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnLoad"/></term>
/// <description>
/// The provided config node is the original configuration from the part's definition. All the
/// annotated fields are populated before this method gets control.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnStart"/></term>
/// <description>
/// Is <b>not called</b> since the parts being created are prefabs and icon models. They are not
/// real parts that behave on a vessel.
/// </description>
/// </item>
/// </list>
///
/// <para>In the <i>editor</i> the callbacks are executed in the following order:</para>
/// <list type="table">
/// <item>
/// <term><see cref="OnAwake"/></term>
/// <description>
/// Notifies about creating new module. If it's a clone operation then all <see cref="KSPField"/>
/// annotated fields have values from the part's config. Otherwise, all the fields are in the
/// initial states.
/// <para>
/// New parts in the editor are created via the clone operation. I.e. each time a part is dragged
/// from the toolbar it's get cloned from the prefab.
/// </para>
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnLoad"/></term>
/// <description>Is <b>not called</b> for the new parts since they are clonned. When a saved vessel
/// is loaded in the editor every part on the vessel gets this method called with the values from
/// the save file. The annotated fields are populated from the file <i>before</i> this method gets
/// control, so it's safe to use them.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnInitialize"/></term>
/// <description>Hard to say what it means for the edtior, but important difference from the flight
/// scenes is that this method is called before <see cref="OnStart"/>.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnStart"/></term>
/// <description>The code must check if the current scene is editor, and do the behavior changes as
/// needed. In the editor parts must not have active behavior.
/// </description>
/// </item>
/// </list>
///
/// <para>In the <i>fligth scenes</i> the callbacks are executed in the following order:</para>
/// <list type="table">
/// <item>
/// <term><see cref="OnAwake"/></term>
/// <description>Notifies about creating new module. All <see cref="KSPField"/> annotated fields
/// have initial values.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnLoad"/></term>
/// <description>The provided config node is the config from the save file. The annotated fields are
/// populated from the file <i>before</i> this method gets control, so it's safe to use them.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnStart"/></term>
/// <description>This method is called when all parts in the vessel are created and loaded. The code
/// must check if the current scene is flight, and do the behavior changes as needed.
/// </description>
/// </item>
/// <item>
/// <term><see cref="OnInitialize"/></term>
/// <description>Indicates that part should start handling physics if any. It may be called multiple
/// times during the part's life. First time it's called when vessel is completely loaded in the
/// secene and all parts are started. Other calls may happen when game returns from a physics
/// suspended state (e.g. from a warp mode back to x1 time speed).
/// <para>
/// Code must check if editor scene is loaded since this method is called differently in the editor.
/// </para>
/// </description>
/// </item>
/// </list>
///
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public class MyModule : PartModule, IPartModule {
///   /// <inheritdoc/>
///   public override void OnAwake() {
///   }
/// }
/// ]]></code>
/// </example>
/// <seealso href="https://kerbalspaceprogram.com/api/class_part_module.html">
/// KSP: PartModule</seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
/// KSP: KSPField</seealso>
public interface IPartModule {
  /// <summary>Initializes a new instance of the module on the part.</summary>
  /// <remarks>
  /// Called on a newly created part. Note, that this method is a bad place to interact with the
  /// other modules on the part since module initialization order is not defined.
  /// <para>See more details on the calling sequence in <see cref="IPartModule"/>.</para>
  /// </remarks>
  void OnAwake();

  /// <summary>Notifies that the part's config is loaded.</summary>
  /// <remarks>
  /// All the fields annotated by <see cref="KSPField"/> are already loaded at this moment. Use the
  /// node from this method to handle special values that are not supported by KSP.
  /// <para>See more details on the calling sequence in <see cref="IPartModule"/>.</para>
  /// </remarks>
  /// <param name="node">Either the part's config node or a configuration from a save file.</param>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_config_node.html">
  /// KSP: ConfigNode</seealso>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  void OnLoad(ConfigNode node);

  /// <summary>Initializes module's state after all other modules have been created.</summary>
  /// <remarks>
  /// Note, that this is not the right place to start physics on the part. This callback is good to
  /// establish connections between the other modules on the part.
  /// </remarks>
  /// <para>See more details on the calling sequence in <see cref="IPartModule"/>.</para>
  /// <param name="state">State that specifies the situation of the vessel.</param>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_part_module.html#ac6597127392e002b92f7427cf50244d3">
  /// KSP: PartModule.StartState</seealso>
  void OnStart(PartModule.StartState state);

  /// <summary>
  /// Called on vessel go off rails. Basically, every time the vessel becomes physics.
  /// </summary>
  /// <remarks>Can be called multiple times during the part's life.</remarks>
  void OnInitialize();

  /// <summary>Notifies about a frame update.</summary>
  /// <remarks>
  /// Be very careful about placing functionality into this callback even if it's bare "if/else"
  /// statement. This callback is called on <b>every</b> frame update. It means that even a simple
  /// piece of code will be called for every part that implements the module. Too many parts with
  /// such modules may significantly drop FPS.
  /// </remarks>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html">
  /// Unity 3D: Update</seealso>
  void OnUpdate();

  /// <summary>Notifies about a physics frame update.</summary>
  /// <remarks>
  /// Physics in Unity is updated every <c>20ms</c> which gives 50 calls per a second. Be
  /// <i>extremly</i> careful about placing functionality into this callback. All fixed updates are
  /// required to complete, so if 50 updates take longer than one second then the game's speed will
  /// degrade.
  /// <para>In general, don't even override this callback unless it's absolutely required.</para>
  /// </remarks>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.FixedUpdate.html">
  /// Unity 3D: FixedUpdate</seealso>
  void OnFixedUpdate();

  /// <summary>Notifies about saving module state.</summary>
  /// <remarks>
  /// This isn't required to be saving into a real file. This method is a generic way to save module
  /// state when it's needed. Note, that saving <c>null</c> is usually a problem for KSP, so always
  /// give default non-null values to every persisted field.
  /// <para>
  /// Persistent fields annotated woth <see cref="KSPField"/> are saved before this callback is
  /// called. Only save values that need special handling. 
  /// </para>
  /// </remarks>
  /// <param name="node">Config node to save data into.</param>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_config_node.html">
  /// KSP: ConfigNode</seealso>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  void OnSave(ConfigNode node);
}

}  // namespace
