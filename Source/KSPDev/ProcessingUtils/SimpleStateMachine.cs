// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using KSPDev.Extensions;

namespace KSPDev.ProcessingUtils {

/// <summary>
/// Simple state machine that allows tracking states and checking basic conditions.
/// </summary>
/// <remarks>
/// If module has more that two modes (which can be controlled by a simple boolean) in makes sense
/// to define each mode as a state, and intorduce a definite state transition diagram. Once it's
/// done most of the mode changes logic can be mvoed in state transition callbacks. Such approach
/// significantly simplifies the code and makes it less error prone.
/// </remarks>
/// <typeparam name="T">
/// Enum to use as state constants. Note, that state machine won't consider any value of the enum as
/// a valid state. Valid states must be defined via <see cref="SetTransitionConstraint"/>.
/// </typeparam>
/// <example>
/// Here is an example of a module with three states with the following logic:
/// <list type="bullet">
/// <item>State <c>One</c> can be transitioned into both <c>Two</c> and <c>Three</c>.</item>
/// <item>States <c>Two</c> and <c>Three</c> can only return back to <c>One</c>.</item>
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
/// related menu item. This way every state handler would control own menu item without interacting
/// with any existing or future items.
/// </para>
/// </example>
/// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
/// KSP: KSPField</seealso>
/// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_event.html">
/// KSP: KSPEvent</seealso>
public sealed class SimpleStateMachine<T> where T : struct, IConvertible {
  /// <summary>Current state of the machine.</summary>
  /// <remarks>
  /// Setting same state as the current one is NO-OP. Setting of a new state may be blocked in
  /// strict mode.
  /// </remarks>
  /// <seealso cref="isStrict"/>
  /// <seealso cref="ForceSetState"/>
  public T currentState {
    get { return _currentState; }
    set { SetState(value); }
  }
  T _currentState;

  /// <summary>Tells if state machine is started.</summary>
  /// <seealso cref="Start"/>
  public bool isStarted { get; private set; }

  /// <summary>
  /// Tells if invalid state transitions will be blocked.  
  /// </summary>
  /// <seealso cref="SetTransitionConstraint"/>
  public bool isStrict { get; private set; }

  /// <summary>Delegate for callback which notifies about state change.</summary>
  /// <param name="state">Current state of the machine.</param>
  public delegate void OnChange(T state);

  /// <summary>Special debug delegate to track state changes.</summary>
  /// <remarks>This callback is called before actual state change.</remarks>
  /// <param name="fromState">State before change.</param>
  /// <param name="toState">State after change.</param>
  public delegate void OnDebugChange(T fromState, T toState);

  /// <summary>
  /// Debug handler for tracking state changes. Avoid using it in normal code logic.
  /// </summary>
  public OnDebugChange OnDebugStateChange;

  Dictionary<T, HashSet<OnChange>> enterHandlers = new Dictionary<T, HashSet<OnChange>>();
  Dictionary<T, HashSet<OnChange>> leaveHandlers = new Dictionary<T, HashSet<OnChange>>();
  Dictionary<T, T[]> transitionContstraints = new Dictionary<T, T[]>();

  /// <summary>Constructs new unstarted state machine.</summary>
  /// <param name="strict">
  /// If <c>true</c> then only transitions defined via <see cref="SetTransitionConstraint"/> will be
  /// allowed.
  /// </param>
  public SimpleStateMachine(bool strict) {
    isStrict = strict;
  }

  /// <summary>Starts state machine and makes it available for state transitions.</summary>
  /// <remarks>
  /// Until machine is started state transitions are not possible. Attempt to change to any state
  /// will result in <see cref="InvalidOperationException"/>.
  /// <para>Starting of the machine will trigger enter state event.</para>
  /// </remarks>
  /// <param name="startState">Initial state of the machine.</param>
  /// <seealso cref="isStarted"/>
  /// <seealso cref="AddStateHandlers"/>
  public void Start(T startState) {
    CheckIsNotStarted();
    isStarted = true;
    _currentState = startState;
    if (OnDebugStateChange != null) {
      OnDebugStateChange(_currentState, _currentState);
    }
    FireEnterState();
  }

  /// <summary>Stops stat machine making it unavailable for any state transitions.</summary>
  /// <remarks>
  /// If machine is not started yet then this call is NO-OP.
  /// <para>Stoping of the machine will trigger leave state event.</para>
  /// </remarks>
  public void Stop() {
    if (isStarted) {
      if (OnDebugStateChange != null) {
        OnDebugStateChange(_currentState, _currentState);
      }
      FireLeaveState();
      isStarted = false;
    }
  }

  /// <summary>Defines source state and, optionally, allowed trasitions.</summary>
  /// <remarks>
  /// State machine figures out full set the allowed states from the transitions. Even if transition
  /// mode is not strict all the states must be defined via tarnsitions (eitehr as a source or a
  /// target).
  /// <para>In strict mode it's required that every transition is declared excplicitly.</para>
  /// <para>If called multiple times then only last call's setup will be stored.</para>
  /// <para>State machine must be in stopped state. Otherwise, an exception will thrown.</para>
  /// </remarks>
  /// <param name="fromState">Source state.</param>
  /// <param name="toStates">
  /// List of states that are allowed as targets for <paramref name="fromState"/>.
  /// </param>
  public void SetTransitionConstraint(T fromState, T[] toStates) {
    CheckIsNotStarted();
    transitionContstraints.Remove(fromState);
    transitionContstraints.Add(fromState, toStates);
  }

  /// <summary>Clears transitions for the soucre state if any.</summary>
  /// <remarks>
  /// Note, that source state is cleared as well. If it's not mentioned in other transitions then
  /// state machine will completely forget the state.
  /// </remarks>
  /// <param name="fromState">Source state to clear tarnsitions for.</param>
  public void ResetTransitionConstraint(T fromState) {
    CheckIsNotStarted();
    transitionContstraints.Remove(fromState);
  }

  /// <summary>Changes current state bypassing any transition or state changes.</summary>
  /// <remarks>
  /// It's discouraged to use this method in normal flow. Though, you it may be handy when
  /// recovering module from an unknown state (e.g. an unexpected exception in the middle of the
  /// process).
  /// </remarks>
  /// <param name="newState">
  /// New current state. It can be a state that is not mentioned in any state transition.
  /// </param>
  public void ForceSetState(T newState) {
    CheckIsStarted();
    if (OnDebugStateChange != null) {
      OnDebugStateChange(_currentState, newState);
    }
    if (isStarted) {
      FireLeaveState();
    }
    _currentState = newState;
    FireEnterState();
  }

  /// <summary>Adds listeners for state enter/leave events.</summary>
  /// <remarks>
  /// Note, that code must not expect that handlers will be called in the same order as they were
  /// added. Each handler must be independent from the others.
  /// <para>Multiple calls for the same handler and state won't create multiple entries.</para>
  /// </remarks>
  /// <param name="state">State to call callbacke on.</param>
  /// <param name="enterHandler">
  /// Callback to call when state machine has switched to a new state. Callback is triggered
  /// <i>after</i> the state has actually changed.
  /// </param>
  /// <param name="leaveHandler">
  /// Callback to call when state machine is going to leave the current state. Callback is triggered
  /// <i>before</i> the state has actually changed. 
  /// </param>
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

  /// <summary>Removes enter state change event handler.</summary>
  /// <remarks>It's safe to call it for non-existing handler.</remarks>
  /// <param name="state">State to delete handler for.</param>
  /// <param name="enterHandler">Enter state handler to delete.</param>
  /// <param name="leaveHandler">Leave state handler to delete.</param>
  public void RemoveHandlers(T state, OnChange enterHandler = null, OnChange leaveHandler = null) {
    CheckIsNotStarted();
    if (enterHandler != null && enterHandlers.ContainsKey(state)) {
      enterHandlers[state].Remove(enterHandler);
    }
    if (leaveHandler != null && leaveHandlers.ContainsKey(state)) {
      leaveHandlers[state].Remove(leaveHandler);
    }
  }

  /// <summary>Verifies if transition is allowed.</summary>
  /// <param name="newState">State to check transition into.</param>
  /// <returns><c>true</c> if transition is allowed.</returns>
  /// <seealso cref="isStrict"/>
  /// <seealso cref="SetTransitionConstraint"/>
  public bool CheckCanSwitchTo(T newState) {
    if (isStrict) {
      // Only allow change if there is explicit constraint.
      return transitionContstraints.ContainsKey(_currentState)
          && transitionContstraints[_currentState].IndexOf(newState) != -1;
    }
    // Allow chnage if the state is known to the machine.
    return !transitionContstraints.ContainsKey(_currentState)
        || transitionContstraints[_currentState].IndexOf(newState) != -1;
  }

  #region Local utility methods
  /// <summary>Verifies that state machine is started.</summary>
  /// <exception cref="InvalidOperationException">If state machine is not yet started.</exception>
  void CheckIsStarted() {
    if (!isStarted) {
      throw new InvalidOperationException("Not allowed in STOPPED state");
    }
  }

  /// <summary>Verifies that state machine is <i>not</i> started.</summary>
  /// <exception cref="InvalidOperationException">If state machine is already started.</exception>
  void CheckIsNotStarted() {
    if (isStarted) {
      throw new InvalidOperationException("Not allowed in STARTED state");
    }
  }

  /// <summary>
  /// Changes machine's state if current and new states are different. Checks if transition is
  /// allowed before actually changing state.
  /// </summary>
  /// <param name="newState">State to change to.</param>
  /// <seealso cref="CheckCanSwitchTo"/>
  /// <exception cref="InvalidOperationException">If transition is not allowed.</exception>
  void SetState(T newState) {
    CheckIsStarted();
    if (!_currentState.Equals(newState)) {
      if (!CheckCanSwitchTo(newState)) {
        throw new InvalidOperationException(string.Format(
            "Transition {0}=>{1} is not allowed", _currentState, newState));
      }
      ForceSetState(newState);
    }
  }

  /// <summary>Notifies all the leave handlers about leaving the current state.</summary>
  void FireLeaveState() {
    HashSet<OnChange> handlers;
    if (leaveHandlers.TryGetValue(_currentState, out handlers)) {
      foreach (var @event in handlers) {
        @event.Invoke(_currentState);
      }
    }
  }

  /// <summary>Notifies all the enter handlers about entering the current state.</summary>
  void FireEnterState() {
    HashSet<OnChange> handlers;
    if (enterHandlers.TryGetValue(_currentState, out handlers)) {
      foreach (var @event in handlers) {
        @event.Invoke(_currentState);
      }
    }
  }
  #endregion
}

}  // namespace
