// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.
using System;
using System.Collections.Generic;
using KSPDev.Extensions;

namespace KSPDev.ProcessingUtils {

//FIXME docs
public sealed class SimpleStateMachine<T> where T : struct, IConvertible {
  public T currentState {
    get { return _currentState; }
    set { SetState(value); }
  }
  T _currentState;
  
  public bool isStarted { get; private set; }
  public bool isStrict { get; private set; }

  public delegate void OnChange(T state);

  public delegate void OnDebugChange(T fromState, T toState);
  public OnDebugChange OnDebugStateChange;

  Dictionary<T, HashSet<OnChange>> enterHandlers = new Dictionary<T, HashSet<OnChange>>();
  Dictionary<T, HashSet<OnChange>> leaveHandlers = new Dictionary<T, HashSet<OnChange>>();
  Dictionary<T, T[]> transitionContstraints = new Dictionary<T, T[]>();

  public SimpleStateMachine(bool strict) {
    isStrict = strict;
  }

  public void Start(T startState) {
    CheckIsNotStarted();
    isStarted = true;
    _currentState = startState;
    if (OnDebugStateChange != null) {
      OnDebugStateChange(_currentState, _currentState);
    }
    FireEnterState();
  }

  public void Stop() {
    if (isStarted) {
      if (OnDebugStateChange != null) {
        OnDebugStateChange(_currentState, _currentState);
      }
      FireLeaveState();
      isStarted = false;
    }
  }

  public void SetTransitionConstraint(T fromState, T[] toStates) {
    CheckIsNotStarted();
    transitionContstraints.Remove(fromState);
    transitionContstraints.Add(fromState, toStates);
  }

  public void ResetTransitionConstraint(T fromState) {
    CheckIsNotStarted();
    transitionContstraints.Remove(fromState);
  }

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

  public bool CheckCanSwitchTo(T newState) {
    if (isStrict) {
      return transitionContstraints.ContainsKey(_currentState)
          && transitionContstraints[_currentState].IndexOf(newState) != -1;
    }
    return !transitionContstraints.ContainsKey(_currentState)
        || transitionContstraints[_currentState].IndexOf(newState) != -1;
  }

  #region Local utility methods
  void CheckIsStarted() {
    if (!isStarted) {
      throw new InvalidOperationException("Not allowed in STOPPED state");
    }
  }

  void CheckIsNotStarted() {
    if (!isStarted) {
      throw new InvalidOperationException("Not allowed in STARTED state");
    }
  }

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

  void FireLeaveState() {
    if (leaveHandlers.ContainsKey(_currentState)) {
      FireEvents(leaveHandlers[_currentState], _currentState);
    }
  }

  void FireEnterState() {
    if (enterHandlers.ContainsKey(_currentState)) {
      FireEvents(enterHandlers[_currentState], _currentState);
    }
  }

  void FireEvents(HashSet<OnChange> events, T trigerringEvent) {
    foreach (var @event in events) {
      @event.Invoke(trigerringEvent);
    }
  }
}

}  // namespace
