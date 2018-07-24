// Kerbal Attachment System - Examples
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.LogUtils;

namespace Examples {

#region KasEventsExample1
public class KasEventsExample1: PartModule {
  public override void OnAwake() {
    base.OnAwake();
    KASAPI.KasEvents.OnStartLinking.Add(LinkStarted);
    KASAPI.KasEvents.OnStopLinking.Add(LinkStopped);
    KASAPI.KasEvents.OnLinkCreated.Add(LinkCreated);
    KASAPI.KasEvents.OnLinkBroken.Add(LinkBroken);
  }

  void OnDestroy() {
    KASAPI.KasEvents.OnStartLinking.Remove(LinkStarted);
    KASAPI.KasEvents.OnStopLinking.Remove(LinkStopped);
    KASAPI.KasEvents.OnLinkCreated.Remove(LinkCreated);
    KASAPI.KasEvents.OnLinkBroken.Remove(LinkBroken);
  }

  void LinkStarted(ILinkSource source) {
    DebugEx.Info("Link started by: {0}", source);
  }

  void LinkStopped(ILinkSource source) {
    DebugEx.Info("Link stopepd by: {0}", source);
  }

  void LinkCreated(IKasLinkEvent ev) {
    DebugEx.Info("Link created: {0} <=> {1}", ev.source, ev.target);
  }

  void LinkBroken(IKasLinkEvent ev) {
    DebugEx.Info("Link broken: {0} <=> {1}", ev.source, ev.target);
  }
}
#endregion

};  // namespace
