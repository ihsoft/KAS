// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

namespace KSPDev.VesselUtils {

class PartsImpl : IParts {
  /// <inheritdoc/>
  public Part GetPartById(string vesselId, uint partId) {
    var vessel = FlightGlobals.Vessels.Find(ves => ves.id.ToString() == vesselId);
    if (vessel == null) {
      Debug.LogErrorFormat("Vessel {0} is not found", vesselId);
      return null;
    }
//    if (!vessel.loaded) {
//      Debug.LogWarningFormat("Vessel {0} is not loaded, loading it...", vesselId);
//      vessel.Load();
//    }
    if (!vessel.loaded) {
      Debug.LogErrorFormat("Vessel {0} is not loaded", vesselId);
      return null;
    }
    return GetPartById(vessel, partId);
  }

  /// <inheritdoc/>
  public Part GetPartById(Vessel vessel, uint partId) {
    var part = vessel.Parts.Find(p => p.flightID == partId);
    if (part == null) {
      Debug.LogErrorFormat("Cannot find part {0} in vessel {1}", partId, vessel.id);
    }
    return part;
  }
}

}  // namespace

namespace KSPDevAPI {

[KSPAddon(KSPAddon.Startup.Instantly, true /*once*/)]
class KSPDevAPILauncher : MonoBehaviour {
  public static void LoadApi() {
    if (!Checker.isLoaded) {
      VesselUtils.Parts = new KSPDev.VesselUtils.PartsImpl();
      Checker.isLoaded = true;
      Debug.LogWarning("KAS API v1 LOADED");
    }
  }
  
  void Awake() {
    LoadApi();
  }
}

}  // namespace
