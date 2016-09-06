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

  Dictionary<T, List<OnChange>> enterHandlers = new Dictionary<T, List<OnChange>>();
  Dictionary<T, List<OnChange>> leaveHandlers = new Dictionary<T, List<OnChange>>();
  Dictionary<T, T[]> transitionContstraints = new Dictionary<T, T[]>();

  public SimpleStateMachine(bool strict) {
    isStrict = strict;
  }

  public void Start(T startState) {
    if (!isStarted) {
      isStarted = true;
      _currentState = startState;
      if (OnDebugStateChange != null) {
        OnDebugStateChange(_currentState, _currentState);
      }
      FireEnterState();
    }
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
    transitionContstraints.Remove(fromState);
    transitionContstraints.Add(fromState, toStates);
  }

  public void ResetTransitionConstraint(T fromState) {
    transitionContstraints.Remove(fromState);
  }

  public void ForceSetState(T newState) {
    if (OnDebugStateChange != null) {
      OnDebugStateChange(_currentState, newState);
    }
    if (isStarted) {
      FireLeaveState();
    }
    _currentState = newState;
    FireEnterState();
  }

  //FIXME: drop
  public void AddEnterHandler(T state, OnChange handler) {
    enterHandlers.SetDefault(state).Add(handler);
  }

  //FIXME: drop
  public void AddLeaveHandler(T state, OnChange handler) {
    leaveHandlers.SetDefault(state).Add(handler);
  }

  public void RemoveEnterHandler(T state, OnChange handler) {
    if (enterHandlers.ContainsKey(state)) {
      enterHandlers[state].Remove(handler);
    }
  }

  public void RemoveLeaveHandler(T state, OnChange handler) {
    if (leaveHandlers.ContainsKey(state)) {
      leaveHandlers[state].Remove(handler);
    }
  }

  public void AddStateHandlers(
      T state, OnChange enterHandler = null, OnChange leaveHandler = null) {
    if (enterHandler != null) {
      enterHandlers.SetDefault(state).Add(enterHandler);
    }
    if (leaveHandler != null) {
      leaveHandlers.SetDefault(state).Add(leaveHandler);
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

  void SetState(T newState) {
    if (!isStarted) {
      throw new InvalidOperationException(string.Format(
          "State machine is not started. Transition to state {0} is impossible", newState));
    }
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

  void FireEvents(List<OnChange> events, T trigerringEvent) {
    foreach (var @event in events) {
      @event.Invoke(trigerringEvent);
    }
  }
}

}  // namespace
