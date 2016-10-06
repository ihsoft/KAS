// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.KSPInterfaces {

/// <summary>Interface for KSP part module.</summary>
/// <remarks>Naturally, KSP doesn't declare any part module interface (unfortunately), and all
/// modder's modules just inherit from <see cref="PartModule"/>. This interface is introduced for
/// the better OOP approach. It reveals methods that a regular module can override, and provides
/// documentation for each of them.
/// <para>Some methods of the module interface look familiar to the ones from Unity but they are not
/// behaving in the same way in every scene. Moreover, not all methods get called in every scene.
/// </para>
/// <para>In the <see cref="GameScenes.LOADING">loading scene</see> the callbacks are executed in
/// the following order:</para>
/// <list>
/// <item><see cref="OnAwake"/>. Notifies about creating new module. If it's a clone operation then
/// all <see cref="KSPField">[KSPField]</see> annotated fields have values from the part's config.
/// Otherwise, all the fields are in the initial states.</item>
/// <item><see cref="OnLoad"/>. The provided config node is the original configuration from the
/// part's definition. All the annotated fields are populated before this method gets control.
/// </item>
/// <item><see cref="OnStart"/> is <b>not called</b> since the parts being created are prefabs and
/// icon models. They are not real parts that behave on a vessel.
/// </item>
/// </list>
/// <para>In the <i>editor</i> the callbacks are executed in the following order:</para>
/// <list>
/// <item><see cref="OnAwake"/>. Notifies about creating new module. If it's a clone operation then
/// all <see cref="KSPField">[KSPField]</see> annotated fields have values from the part's config.
/// Otherwise, all the fields are in the initial states.
/// <para>New parts in the editor are created via the clone operation. I.e. each time a part is
/// dragged from the toolbar it's get cloned from the prefab.</para>
/// </item>
/// <item><see cref="OnLoad"/>. Is <b>not called</b> for the new parts since they are clonned.
/// When a saved vessel is loaded in the editor every part on the vessel gets this method called
/// with the values from the save file. The annotated fields are populated from the file
/// <i>before</i> this method gets control, so it's safe to use them.</item>
/// <item><see cref="OnInitialize"/>. Hard to say what it means for the edtior, but imnportant
/// difference from the flight scenes is that this method is called <i>before</i> <c>Start()</c>.
/// </item>
/// <item><see cref="OnStart"/>. The code must check if the current scene is editor, and do the
/// behavior changes as needed. In the editor parts must not have active behavior.</item>
/// </list>
/// <para>In the <i>fligth scenes</i> the callbacks are executed in the following order:</para>
/// <list>
/// <item><see cref="OnAwake"/>. Notifies about creating new module. All
/// <see cref="KSPField">[KSPField]</see> annotated fields have initial values.</item>
/// <item><see cref="OnLoad"/>. The provided config node is the config from the save file. The
/// annotated fields are populated from the file <i>before</i> this method gets control, so it's
/// safe to use them.</item>
/// <item><see cref="OnStart"/>. This method is called when all parts in the vessel are created and
/// loaded. The code must check if the current scene is flight, and do the behavior changes as
/// needed.</item>
/// <item><see cref="OnInitialize"/>. Indicates that part should start handling physics if any. It
/// may be called multiple times during the part's life. First time it's called when vessel is
/// completely loaded in the secene, and all parts are started. Other calls may happen when game
/// returns from a physics suspend state (e.g. from warp mode back to x1 time speed).
/// <para>Code must check if editor scene is loaded since this method is called differently in the
/// editor.</para>
/// </item>
/// </list>
/// </remarks>
// FIXME: verify each scenario for the correctness.
public interface IPartModule {
  /// <summary>Initializes a new instance of the module on the part.</summary>
  /// <remarks>Called on a newly created part. Note, that this method is a bad place to interact
  /// with the other modules on the part since module initialization order is not defined.
  /// <para>See more details on the calling sequence in <see cref="IPartModule"/>.</para>
  /// </remarks>
  void OnAwake();

  /// <summary>Notifies that the part's config is loaded.</summary>
  /// <remarks>All the fields annotated by <see cref="KSPField">[KSPField]</see> are already loaded
  /// at this moment. Use the node from this method to handle special values that are not supported
  /// by KSP.
  /// <para>See more details on the calling sequence in <see cref="IPartModule"/>.</para>
  /// </remarks>
  /// <param name="node">Either the part's config node or a configuration from a save file.</param>
  void OnLoad(ConfigNode node);

  /// <summary>Initializes module's state after all other modules have been created.</summary>
  /// <remarks>Note, that this is not the right place to start physics on the part. This callback
  /// is good to establish connections between the other modules on the part.</remarks>
  /// <para>See more details on the calling sequence in <see cref="IPartModule"/>.</para>
  /// <param name="state">State that specifies the situation of the vessel.</param>
  void OnStart(PartModule.StartState state);

  /// <summary>
  /// Called on vessel go off rails. Basically, every time the vessel becomes physics.
  /// </summary>
  /// <remarks>Can be called multiple times during the part's life.</remarks>
  void OnInitialize();

  /// <summary>Notifies about a frame update.</summary>
  /// <remarks>Be very careful about placing functionality into this callback even if it's bare
  /// "if/else" statement. This callback is called on <b>every</b> frame update. It means that even
  /// a simple piece of code will be called for every part that implements the module. Too many
  /// parts with such modules may significantly drop FPS.</remarks>
  void OnUpdate();

  /// <summary>Notifies about a physics frame update.</summary>
  /// <remarks>Physics in Unity is updated every <c>20ms</c> which gives 50 calls per a second. Be
  /// <i>extremly</i> careful about placing functionality into this callback. All fixed updates are
  /// required to complete, so if 50 updates take longer than one second then the game's speed will
  /// degrade.
  /// <para>In general, don't even override this callback unless it's absolutely required.</para>
  /// </remarks>
  void OnFixedUpdate();

  //FIXME doc
  // FIXME: check if saving to the file is the only scenario.
  void OnSave(ConfigNode node);

  //FIXME doc
  void OnActive();

  //FIXME doc
  void OnInactive();

  // Move to IsStageable. maybe
//  bool IsStageable();
//  bool StagingEnabled();
//  void UpdateStagingToggle();
//  void SetStaging(bool newValue);
//  bool StagingToggleEnabledEditor();
//  bool StagingToggleEnabledFlight();
//  string GetStagingEnableText();
//  string GetStagingDisableText();
}

}  // namespace
