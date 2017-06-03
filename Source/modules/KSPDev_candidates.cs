// This is an intermediate module for methods and classes that are considred as candidates for
// KSPDev Utilities. Ideally, this module is always empty but there may be short period of time
// when new functionality lives here and not in KSPDev.

using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using UnityEngine;
using KSPDev.ConfigUtils;
using System;
using System.Linq;

using System.Collections.Generic;
using KSPDev.Extensions;
using KSPDev.KSPInterfaces;
using System.Reflection;
using StackTrace = System.Diagnostics.StackTrace;


namespace KSPDev {

/// <summary>Generic interface for the modules that implement a UI context menu.</summary>
/// <seealso href="https://kerbalspaceprogram.com/api/class_game_events.html#ae6daaa6f39473078514543a2f8485513">
/// KPS: GameEvents.onPartActionUICreate</seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_game_events.html#a7ccbd16e2aee0176a4431f0b5f9d63e5">
/// KPS: GameEvents.onPartActionUIDismiss</seealso>
public interface IHasContextMenu {
  /// <summary>
  /// A callback that is called every time the module's conetxt menu items need to update. 
  /// </summary>
  /// <remarks>
  /// <para>
  /// When a part needs to update its context menu, it must not be doing it in the methods other
  /// this one. By doing the update in a just one method, the part ensures there will be a
  /// consistency.
  /// </para>
  /// <para>
  /// It's very implementation dependent when and why the update is needed. However, at the very
  /// least this callback must be called from the <see cref="IPartModule.OnLoad">OnLoad</see> method
  /// to let the module to update the state and the titles.
  /// </para>
  /// </remarks>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.KSPInterfaces.IPartModule']/*"/>
  void UpdateContextMenu();
}

}  // namespace

namespace KSPDev.ResourceUtils {

/// <summary>
/// A helper class that holds string and ID defintions for all the game stock resources. 
/// </summary>
/// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Resource">KSP Wiki: Resource</seealso>
public static class StockResourceNames {
  /// <summary>Electric charge resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Electric_charge">
  /// KSP Wiki: Electric charge</seealso>
  public const string ElectricCharge = "ElectricCharge";

  /// <summary>Liquid fuel resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Liquid_fuel">
  /// KSP Wiki: Liquid fuel</seealso>
  public const string LiquidFuel = "LiquidFuel";

  /// <summary>Oxidizer resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Oxidizer">
  /// KSP Wiki: Oxidizer</seealso>
  public const string Oxidizer = "Oxidizer";

  /// <summary>Intake air resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Intake_air">
  /// KSP Wiki: Intake air</seealso>
  public const string IntakeAir = "IntakeAir";

  /// <summary>Solid fuel resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Solid_fuel">
  /// KSP Wiki: Solid fuel</seealso>
  public const string SolidFuel = "SolidFuel";

  /// <summary>Monopropellant resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Monopropellant">
  /// KSP Wiki: Monopropellant</seealso>
  public const string MonoPropellant = "MonoPropellant";

  /// <summary>EVA Propellant resource name.</summary>
  /// <remarks>It's the fuel that powers the EVA jetpack.</remarks>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Extra-Vehicular_Activity">
  /// KSP Wiki: Extra-Vehicular Activity</seealso>
  public const string EvaPropellant = "EVA Propellant";

  /// <summary>Xenon gas resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Xenon_gas">
  /// KSP Wiki: Xenon gas</seealso>
  public const string XenonGas = "XenonGas";

  /// <summary>Ore resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Ore">
  /// KSP Wiki: Ore</seealso>
  public const string Ore = "Ore";

  /// <summary>Ablator resource name.</summary>
  /// <seealso href="http://wiki.kerbalspaceprogram.com/wiki/Ablator">
  /// KSP Wiki: Ablator</seealso>
  public const string Ablator = "Ablator";

  /// <summary>Returns an ID for the specified resource name.</summary>
  /// <remarks>This ID can be used in the methods that can only work with IDs.</remarks>
  /// <param name="resourceName">The name of the stock resource.</param>
  /// <returns>An ID of the resource.</returns>
  public static int GetId(string resourceName) {
    return resourceName.GetHashCode();
  }

  /// <summary>Returns a user friendly name of the resource.</summary>
  /// <param name="resourceName">The resource common name.</param>
  /// <returns>A user friendly string that identifies the resource.</returns>
  public static string GetResourceTitle(string resourceName) {
    return PartResourceLibrary.Instance.GetDefinition(resourceName).displayName;
  }

  /// <summary>Returns a user friendly name of the resource.</summary>
  /// <param name="resourceId">The resource ID.</param>
  /// <returns>A user friendly string that identifies the resource.</returns>
  public static string GetResourceTitle(int resourceId) {
    return PartResourceLibrary.Instance.GetDefinition(resourceId).displayName;
  }
}

}  // namepsace

namespace KSPDev.ModelUtils {

/// TODO: Merge with KSPDev.ModelUtils.Hierarchy
public static class Hierarchy2 {
  /// TODO: Replace default FindTransformByPath version.
  public static Transform FindTransformByPath(Transform parent, string path, Transform defValue = null) {
    var obj = Hierarchy.FindTransformByPath(parent, path);
    if (obj == null && defValue != null) {
      Debug.LogWarningFormat(
          "Cannot find model object: root={0}, path={1}. Using a fallback: {2}",
          DbgFormatter.TranformPath(parent), path, DbgFormatter.TranformPath(defValue));
      return defValue;
    }
    return obj;
  }

  /// <summary>Finds an object in the part's model.</summary>
  /// <param name="part">The part to look for the objects in.</param>
  /// <param name="path">The path to look for.</param>
  /// <param name="defValue">The default value to return when no object found.</param>
  /// <returns>The found object or <c>null</c>.</returns>
  public static Transform FindPartModelByPath(Part part, string path, Transform defValue = null) {
    return FindTransformByPath(Hierarchy.GetPartModelTransform(part), path, defValue: defValue);
  }
}
  
}  // namespace

namespace KSPDev.LogUtils {

/// TODO: Merge with KSPDev.LogUtils.DbgFormatter
public static class DbgFormatter2 {
  /// <summary>Formats a string providing a reference to the host part.</summary>
  /// <param name="host">The part that outputs into the log.</param>
  /// <param name="format">The format string.</param>
  /// <param name="args">The format arguments.</param>
  /// <returns>A logging string.</returns>
  public static string HostedLog(Part host, string format, params object[] args) {
    return string.Format("[Part:" + DbgFormatter.PartId(host) + "] " + format, args);
  }

  /// <summary>Formats a string providing a reference to the host part module.</summary>
  /// <param name="host">The part module that outputs into the log.</param>
  /// <param name="format">The format string.</param>
  /// <param name="args">The format arguments.</param>
  /// <returns>A logging string.</returns>
  public static string HostedLog(PartModule host, string format, params object[] args) {
    return string.Format(
        "[Part:" + DbgFormatter.PartId(host.part) + "#Module:" + host.moduleName + "] " + format,
        args);
  }

  /// <summary>Formats a string providing a reference to the host object.</summary>
  /// <param name="host">The object that outputs into the log.</param>
  /// <param name="format">The format string.</param>
  /// <param name="args">The format arguments.</param>
  /// <returns>A logging string.</returns>
  public static string HostedLog(Transform host, string format, params object[] args) {
    return string.Format("[Object:" + host.name + "] " + format, args);
  }

  /// <summary>Formats a string providing a reference to the host object.</summary>
  /// <param name="host">The object that outputs into the log.</param>
  /// <param name="format">The format string.</param>
  /// <param name="args">The format arguments.</param>
  /// <returns>A logging string.</returns>
  public static string HostedLog(GameObject host, string format, params object[] args) {
    return HostedLog(host.transform, format, args);
  }
}

}  // namespace

namespace KSPDev.Types {

/// <summary>Type to hold position and rotation of a transform. It can be serialized.</summary>
/// <remarks>
/// The value serializes into 6 numbers separated by a comma. They form two triplets:
/// <list type="bullet">
/// <item>The first triplet is a position: x, y, z.</item>
/// <item>
/// The second triplet is a Euler rotaion around each axis: x, y, z.
/// </item>
/// </list>
/// </remarks>
public sealed class PosAndRot2 : IPersistentField {
  /// <summary>Position of the transform.</summary>
  public Vector3 pos;
  
  /// <summary>Euler rotation.</summary>
  /// <remarks>
  /// The rotation angles are automatically adjusted to stay within the [0; 360) range.
  /// </remarks>
  public Vector3 euler {
    get { return _euler; }
    set {
      _euler = value;
      NormlizeAngles();
      rot = Quaternion.Euler(_euler.x, _euler.y, _euler.z);
    }
  }
  Vector3 _euler;

  /// <summary>Orientation of the transform.</summary>
  public Quaternion rot { get; private set; }

  /// <summary>Constructs a default instance.</summary>
  /// <remarks>Required for the persistence to work correctly.</remarks>
  /// <para>
  /// By default position is <c>(0,0,0)</c>, Euler angles are <c>(0,0,0)</c>, and the rotation is
  /// <c>Quaternion.identity</c>.  
  /// </para>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Vector3.html">
  /// Unity3D: Vector3</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Quaternion-identity.html">
  /// Unity3D: Quaternion</seealso>
  public PosAndRot2() {
  }

  /// <summary>Constructs a copy of an object of the same type.</summary>
  /// <param name="from">Source object.</param>
  public PosAndRot2(PosAndRot2 from) {
    pos = from.pos;
    euler = from.euler;
  }

  /// <summary>Constructs an object from a transform properties.</summary>
  /// <param name="pos">Position of the transform.</param>
  /// <param name="euler">Euler rotation of the transform.</param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Vector3.html">
  /// Unity3D: Vector3</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Transform.html">
  /// Unity3D: Transform</seealso>
  public PosAndRot2(Vector3 pos, Vector3 euler) {
    this.pos = pos;
    this.euler = euler;
  }

  /// <summary>Gives a deep copy of the object.</summary>
  /// <returns>New object.</returns>
  public PosAndRot2 Clone() {
    return new PosAndRot2(this);
  }

  /// <inheritdoc/>
  public string SerializeToString() {
    return string.Format(
        "{0},{1},{2}, {3},{4},{5}", pos.x, pos.y, pos.z, euler.x, euler.y, euler.z);
  }

  /// <inheritdoc/>
  public void ParseFromString(string value) {
    var elements = value.Split(',');
    if (elements.Length != 6) {
      throw new ArgumentException(
          "PosAndRot type needs exactly 6 elements separated by a comma but found: " + value);
    }
    var args = elements.Select(float.Parse).ToArray();
    pos = new Vector3(args[0], args[1], args[2]);
    euler = new Vector3(args[3], args[4], args[5]);
  }

  /// <summary>Shows a human readable representation.</summary>
  /// <returns>String value.</returns>
  public override string ToString() {
    return string.Format(
        "[PosAndRot Pos={0}, Euler={1}]", DbgFormatter.Vector(pos), DbgFormatter.Vector(euler));
  }

  /// <summary>Creates a new instance from the provided string.</summary>
  /// <param name="strValue">The value to parse.</param>
  /// <param name="failOnError">
  /// If <c>true</c> then a parsing error will fail the creation. Otherwise, a default isntance will
  /// be returned.
  /// </param>
  /// <returns>An instance, intialized from the string.</returns>
  public static PosAndRot2 FromString(string strValue, bool failOnError = false) {
    var res = new PosAndRot2();
    try {
      res.ParseFromString(strValue);
    } catch (ArgumentException ex) {
      if (failOnError) {
        throw;
      }
      Debug.LogWarningFormat("Cannot parse PosAndRot, using default: {0}", ex.Message);
    }
    return res;
  }
  
  /// <summary>
  /// Ensures that all the angles are in the range of <c>[0; 360)</c>. 
  /// </summary>
  void NormlizeAngles() {
    while (_euler.x > 360) _euler.x -= 360;
    while (_euler.x < 0) _euler.x += 360;
    while (_euler.y > 360) _euler.y -= 360;
    while (_euler.y < 0) _euler.y += 360;
    while (_euler.z > 360) _euler.z -= 360;
    while (_euler.z < 0) _euler.z += 360;
  }
}

}  // namespace

namespace KSPDev.ProcessingUtils {

/// <summary>
/// Simple state machine that allows tracking of the states and checking the basic transition
/// conditions.
/// </summary>
/// <remarks>
/// If a module has more that two modes (which can be controlled by a simple boolean) it makes sense
/// to define each mode as a state, and introduce a definite state transition diagram. Once it's
/// done, a state machine can be setup by defining which transitions are allowed. At this point the
/// module will be able to just react on the state change events instead of checking multiple
/// conditions.
/// </remarks>
/// <typeparam name="T">The enum to use as the state constants.</typeparam>
/// <example>
/// Let's pretend there is a module with three states:
/// <list type="bullet">
/// <item>The state <c>One</c> can be transitioned into both <c>Two</c> and <c>Three</c>.</item>
/// <item>The states <c>Two</c> and <c>Three</c> can only return back to <c>One</c>.</item>
/// <item>In states <c>Two</c> and <c>Three</c> different menu options are available.</item>
/// <item>In state <c>One</c> no menu options are available.</item>
/// </list>
/// <code><![CDATA[
/// class MyModule : PartModule {
///   enum MyState {
///     One, Two, Three
///   }
///
///   [KSPField(isPersistant = true)]
///   public MyState persistedState = MyState.One;  // ALWAYS provide a default value!
///
///   SimpleStateMachine<MyState> linkStateMachine;
///
///   [KSPEvent(guiName = "State: TWO")]
///   public void StateTwoMenuAction() {
///     Debug.LogInfo("StateTwoMenuAction()");
///   }
///
///   [KSPEvent(guiName = "State: THREE")]
///   public void StateThreeMenuAction() {
///     Debug.LogInfo("StateThreeMenuAction()");
///   }
///
///   public override OnAwake() {
///     linkStateMachine = new SimpleStateMachine<MyState>(true /* strict */);
///     linkStateMachine.SetTransitionConstraint(
///         MyState.One,
///         new[] {MyState.Two, MyState.Three});
///     linkStateMachine.SetTransitionConstraint(
///         MyState.Two,
///         new[] {MyState.One});
///     linkStateMachine.SetTransitionConstraint(
///         MyState.Three,
///         new[] {MyState.One});
///     linkStateMachine.AddStateHandlers(
///         MyState.One,
///         enterHandler: x => {
///           Events["StateTwoMenuAction"].active = false;
///           Events["StateThreeMenuAction"].active = false;
///         });
///     linkStateMachine.AddStateHandlers(
///         MyState.Two,
///         enterHandler: x => {
///           Events["StateTwoMenuAction"].active = true;
///           Events["StateThreeMenuAction"].active = false;
///         });
///     linkStateMachine.AddStateHandlers(
///         MyState.Three,
///         enterHandler: x => {
///           Events["StateTwoMenuAction"].active = false;
///           Events["StateThreeMenuAction"].active = true;
///         });
///   }
///
///   public override void OnStart(PartModule.StartState state) {
///     linkStateMachine.Start(persistedState);  // Restore state from the save file.
///   }
///
///   void OnDestory() {
///     // Usually, this isn't needed. But if code needs to do a cleanup job it makes sense to wrap
///     // it into a handler, and stop the machine in Unity destructor.
///     linkStateMachine.Stop();
///   }
///
///   public override OnUpdate() {
///     if (Input.GetKeyDown("1")) {
///       // This transition will always succceed. 
///       stateMachine.currentState = MyState.One;
///     }
///     if (Input.GetKeyDown("2")) {
///       // This transition will only succceed if current state is MyState.One. 
///       stateMachine.currentState = MyState.Two;
///     }
///     if (Input.GetKeyDown("3")) {
///       // This transition will only succceed if current state is MyState.One. 
///       stateMachine.currentState = MyState.Three;
///     }
///   }
/// }
/// ]]></code>
/// <para>
/// The same logic could be achivied in a different way. Instead of enabling/disabling all the menu
/// items in every "enter" handler the code could define "leave" handlers that would disable the
/// related menu item. This way every state handler would control its own menu item without
/// interacting with any existing or the future items.
/// </para>
/// </example>
public sealed class SimpleStateMachine2<T> where T : struct, IConvertible {
  /// <summary>Current state of the machine.</summary>
  /// <remarks>
  /// Setting the same state as the current one is a NO-OP. Setting of a new state may throw an
  /// exception in the strict mode.
  /// </remarks>
  /// <value>The current state.</value>
  /// <seealso cref="isStrict"/>
  public T currentState {
    get {
      CheckIsStarted();
      return _currentState.Value;
    }
    set {
      CheckIsStarted();
      SetState(value);
    }
  }
  T? _currentState;

  /// <summary>Tells if the state machine is started.</summary>
  /// <value>The started state.</value>
  /// <seealso cref="Start"/>
  public bool isStarted { get { return _currentState.HasValue; } }

  /// <summary>Tells if all the transitions must be excplicitly declared.</summary>
  /// <remarks>
  /// The state transitions are defined via <see cref="SetTransitionConstraint"/>.
  /// </remarks>
  /// <value>The strict mode state.</value>
  /// <seealso cref="SetTransitionConstraint"/>
  /// <seealso cref="ResetTransitionConstraint"/>
  public bool isStrict { get; private set; }

  /// <summary>Delegate for a callback which notifies about a state change.</summary>
  /// <param name="state">
  /// The state of the machine. It exact meaning depends on the circumstances under which the
  /// callback has been called.
  /// </param>
  public delegate void OnChange(T? state);

  /// <summary>Delegate to track an arbitrary state transition.</summary>
  /// <param name="fromState">
  /// The state before the change. If it's <c>null</c> then the transition was requested by the
  /// <see cref="Start"/> method to initialize the machine.
  /// </param>
  /// <param name="toState">
  /// The state after the change. If it's <c>null</c> then the transition was requested by the
  /// <see cref="Stop"/> method to stop the machine.
  /// </param>
  public delegate void OnStateChangeHandler(T? fromState, T? toState);

  /// <summary>Event that fires when the state machine ha changed its state.</summary>
  /// <remarks>The event is fired <i>after</i> the actual state change.</remarks>
  /// <seealso cref="OnStateChangeHandler"/>
  public event OnStateChangeHandler onAfterTransition;

  readonly Dictionary<T, HashSet<OnChange>> enterHandlers = new Dictionary<T, HashSet<OnChange>>();
  readonly Dictionary<T, HashSet<OnChange>> leaveHandlers = new Dictionary<T, HashSet<OnChange>>();
  readonly Dictionary<T, T[]> transitionContstraints = new Dictionary<T, T[]>();

  /// <summary>Constructs a new unstarted state machine.</summary>
  /// <param name="strict">The strict mode.</param>
  /// <seealso cref="isStrict"/>
  public SimpleStateMachine2(bool strict) {
    isStrict = strict;
  }

  /// <summary>Starts the state machine and makes it available for the state transitions.</summary>
  /// <remarks>
  /// <para>
  /// Until the machine is started, the state transitions are not possible. An attempt to move the
  /// machine into any state will result in a <see cref="InvalidOperationException"/> exception.
  /// </para>
  /// <para>
  /// Starting of the machine will trigger an enter state event. The <c>oldState</c> parameter in
  /// the callback will be the same as the <paramref name="startState"/>.
  /// </para>
  /// </remarks>
  /// <param name="startState">The initial state of the machine.</param>
  /// <seealso cref="isStarted"/>
  /// <seealso cref="AddStateHandlers"/>
  public void Start(T startState) {
    CheckIsNotStarted();
    SetState(startState);
  }

  /// <summary>Stops the state machine making it unavailable for any state transition.</summary>
  /// <remarks>
  /// <para>Stoping of the started machine will trigger a leave state event. The <c>newState</c>
  /// parameter in the callback will be the same as the <see cref="currentState"/>.
  /// </para>
  /// <para>
  /// If the machine is not started then this call is a NO-OP.
  /// </para>
  /// </remarks>
  /// <seealso cref="isStarted"/>
  /// <seealso cref="AddStateHandlers"/>
  public void Stop() {
    SetState(null);
  }

  /// <summary>Defines a state and the allowed target states for it.</summary>
  /// <remarks>
  /// In the strict mode it's required that every transition is declared excplicitly.
  /// </remarks>
  /// <param name="fromState">The source state.</param>
  /// <param name="toStates">The list of the states that are allowed as the targets.</param>
  /// <seealso cref="isStrict"/>
  public void SetTransitionConstraint(T fromState, T[] toStates) {
    CheckIsNotStarted();
    transitionContstraints.Remove(fromState);
    transitionContstraints.Add(fromState, toStates);
  }

  /// <summary>Clears the transitions for the source state if any.</summary>
  /// <param name="fromState">The source state to clear the tarnsitions for.</param>
  /// <seealso cref="isStrict"/>
  public void ResetTransitionConstraint(T fromState) {
    CheckIsNotStarted();
    transitionContstraints.Remove(fromState);
  }

  /// <summary>Adds a state change event.</summary>
  /// <remarks>
  /// <para>
  /// When the state is changed by setting the <see cref="currentState"/> property, the transition
  /// callbacks are only called when the state has actually changed. However, <see cref="Start"/>
  /// and <see cref="Stop"/> methods will trigger the callbacks regardless to the current state.
  /// </para>
  /// <para>
  /// Note, that the code must not expect that the handlers will be called in the same order as they
  /// were added. Each handler must be independent from the others.
  /// </para>
  /// </remarks>
  /// <param name="state">The state to call a callback on.</param>
  /// <param name="enterHandler">
  /// The callback to call when the state machine has switched to a new state. The callback is
  /// triggered <i>after</i> the state has actually changed. The callback's parameter is the
  /// old state, from which the machine has switched.
  /// </param>
  /// <param name="leaveHandler">
  /// The callback to call when the state machine is going to leave the current state. The callback
  /// is triggered <i>before</i> the state has actually changed. The callback's parameter is the
  /// new state, to which the machine is going to switch. 
  /// </param>
  /// <seealso cref="currentState"/>
  public void AddStateHandlers(
      T state, OnChange enterHandler = null, OnChange leaveHandler = null) {
    CheckIsNotStarted();
    if (enterHandler != null) {
      enterHandlers.SetDefault(state).Add(enterHandler);
    }
    if (leaveHandler != null) {
      leaveHandlers.SetDefault(state).Add(leaveHandler);
    }
  }

  /// <summary>Removes a state change event handler.</summary>
  /// <remarks>It's safe to call it for a non-existing handler.</remarks>
  /// <param name="state">The state to delete a handler for.</param>
  /// <param name="enterHandler">The enter state handler to delete.</param>
  /// <param name="leaveHandler">The leave state handler to delete.</param>
  public void RemoveHandlers(T state, OnChange enterHandler = null, OnChange leaveHandler = null) {
    CheckIsNotStarted();
    if (enterHandler != null && enterHandlers.ContainsKey(state)) {
      enterHandlers[state].Remove(enterHandler);
    }
    if (leaveHandler != null && leaveHandlers.ContainsKey(state)) {
      leaveHandlers[state].Remove(leaveHandler);
    }
  }

  /// <summary>Verifies if the machine can move into the desired state.</summary>
  /// <param name="newState">The state to check the transition for.</param>
  /// <returns><c>true</c> if the transition is allowed.</returns>
  /// <seealso cref="isStrict"/>
  /// <seealso cref="SetTransitionConstraint"/>
  public bool CheckCanSwitchTo(T newState) {
    return !isStrict
        || transitionContstraints.ContainsKey(_currentState.Value)
           && transitionContstraints[_currentState.Value].IndexOf(newState) != -1;
  }

  #region Local utility methods
  /// <summary>Verifies that the state machine is started.</summary>
  /// <exception cref="InvalidOperationException">If state machine is not yet started.</exception>
  void CheckIsStarted() {
    if (!isStarted) {
      throw new InvalidOperationException("Not allowed in STOPPED state");
    }
  }

  /// <summary>Verifies that the state machine is <i>not</i> started.</summary>
  /// <exception cref="InvalidOperationException">If state machine is already started.</exception>
  void CheckIsNotStarted() {
    if (isStarted) {
      throw new InvalidOperationException("Not allowed in STARTED state");
    }
  }

  /// <summary>
  /// Changes the machine's state if the current and the new states are different. Checks if the
  /// transition is allowed before actually changing the state.
  /// </summary>
  /// <param name="newState">
  /// The state to change to. If <c>null</c> then the machine will be stopped.
  /// </param>
  /// <seealso cref="CheckCanSwitchTo"/>
  /// <exception cref="InvalidOperationException">If the transition is not allowed.</exception>
  void SetState(T? newState) {
    if (!_currentState.Equals(newState)) {
      var oldState = _currentState;
      if (oldState.HasValue && newState.HasValue) {
        if (!CheckCanSwitchTo(newState.Value)) {
          throw new InvalidOperationException(string.Format(
              "Transition {0}=>{1} is not allowed", oldState.Value, newState.Value));
        }
      }
      if (oldState.HasValue) {
        FireLeaveState(newState);
      }
      _currentState = newState;
      if (newState.HasValue) {
        FireEnterState(oldState);
      }
      if (onAfterTransition != null) {
        onAfterTransition(oldState, newState);
      }
    }
  }

  /// <summary>Notifies all the handlers about leaving the current state.</summary>
  /// <param name="newState">The new state where the machine is going to.</param>
  void FireLeaveState(T? newState) {
    HashSet<OnChange> handlers;
    if (leaveHandlers.TryGetValue(_currentState.Value, out handlers)) {
      foreach (var @event in handlers) {
        @event.Invoke(newState);
      }
    }
  }

  /// <summary>Notifies all the handlers about entering a new state.</summary>
  /// <param name="oldState">The old state where the machine is going from.</param>
  void FireEnterState(T? oldState) {
    HashSet<OnChange> handlers;
    if (enterHandlers.TryGetValue(_currentState.Value, out handlers)) {
      foreach (var @event in handlers) {
        @event.Invoke(oldState);
      }
    }
  }
  #endregion
}

}  // namespace

namespace KSPDev.ModelUtils {

/// FIXME: merge with AlignTransforms 
public static class AlignTransforms2 {
  /// <summary>
  /// Aligns the source node so that it's located at the target, and source and target are "looking"
  /// at the each other.
  /// </summary>
  /// <remarks>
  /// The object's "look" direction is a <see cref="Transform.forward"/> direction.
  /// </remarks>
  /// <param name="source">The node to align.</param>
  /// <param name="sourceChild">The child node of the source to use as the align point.</param>
  /// <param name="target">The target node to align with.</param>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Transform']/*"/>
  public static void SnapAlign(Transform source, Transform sourceChild, Transform target) {
    // Don't relay on the localRotation since the child may be not an immediate child.
    var localChildRot = source.rotation.Inverse() * sourceChild.rotation;
    source.rotation = Quaternion.LookRotation(-target.forward, target.up) * localChildRot;
    source.position = source.position - (sourceChild.position - target.position);
  }
}

}  // namespace
