// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.LogUtils;
using System.Collections.Generic;
using System;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace SaveUpgradePipeline.KAS {

/// <summary>Class that fixes incompatible KAS part in the saved games.</summary>
[UpgradeModule(LoadContext.SFS | LoadContext.Craft,
               sfsNodeUrl = "GAME/FLIGHTSTATE/VESSEL/PART",
               craftNodeUrl = "PART")]
// ReSharper disable once UnusedType.Global
internal sealed class PatchFilesProcessor : UpgradeScript {

  /// <summary>Patches per part name.</summary>
  /// <remarks>The patches are ordered by timestamp.</remarks>
  Dictionary<string, List<ConfigNodePatch>> _partPatches;

  #region implemented abstract members of UpgradeScript
  /// <inheritdoc/>
  public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName) {
    if (_partPatches.Count == 0) {
      return TestResult.Pass;  // No need to patch.
    }
    if (node.GetValue("$$failed") != null) {
      return TestResult.Failed;
    }
    var partName = PartNodePatcher.GetPartNameFromUpgradeNode(node, loadContext);
    if (partName == null) {
      return TestResult.Pass;  // Part was dropped during the upgrade.
    }
    var hasMatches = false;
    if (_partPatches.TryGetValue(partName, out var patches)) {
      for (var i = patches.Count - 1; i >= 0; --i) {
        var patch = patches[i];
        try {
          hasMatches |= PartNodePatcher.TestPatch(node, patch, loadContext);
        } catch (Exception ex) {
          DebugEx.Error("Cannot handle test condition: {0}", ex);
          DebugEx.Warning("Disabling patch: {0}", patch);
          patches.RemoveAt(i);
        }
      }
    }
    return hasMatches ? TestResult.Upgradeable : TestResult.Pass;
  }

  /// <inheritdoc/>
  public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode) {
    var partName = PartNodePatcher.GetPartNameFromUpgradeNode(node, loadContext);
    DebugEx.Warning("Patch saved game state for part: {0}", partName);
    var badPatches = new List<ConfigNodePatch>();
    var applyPatches = _partPatches[partName];
    foreach (var patch in applyPatches) {
      try {
        PartNodePatcher.PatchNode(node, patch, loadContext);
      } catch (Exception ex) {
        DebugEx.Error("Cannot apply patch '" + patch + "': " + ex);
        node.SetValue("$$failed", true, createIfNotFound: true);
      }
      // Ensure that the patch worked and won't trigger another patching round.
      if (PartNodePatcher.TestPatch(node, patch, loadContext, quietMode: true)) {
        badPatches.Add(patch);
        node.SetValue("$$failed", true, createIfNotFound: true);
      }
    }
    foreach (var badPatch in badPatches) {
      applyPatches.Remove(badPatch);
      DebugEx.Error("Patch hasn't fixed the part, disabling it: {0}", badPatch);
    }
  }

  /// <inheritdoc/>
  public override string Name => "KAS parts patcher v1.0";

  /// <inheritdoc/>
  public override string Description => "Applies a KAS compatibility scripts on the parts";

  /// <inheritdoc/>
  public override Version EarliestCompatibleVersion => new Version(0, 21, 0);

  /// <inheritdoc/>
  // Always needs to run.
  public override Version TargetVersion => new Version(
      Versioning.version_major, Versioning.version_minor, Versioning.Revision);
  #endregion

  #region UpgradeScript overrides
  /// <inheritdoc/>
  protected override void OnInit() {
    _partPatches = PartNodePatcher.GetPatches("KAS")
        .GroupBy(x => x.testSection.partTests.GetValue("name"))
        .ToDictionary(
            x => x.Key,
            x => x.OrderBy(n => n.patchCreationTimestamp)
                .ThenBy(n => n.modName)
                .ToList());
    DebugEx.Fine("Loaded {0} part patch nodes", _partPatches.Count);
  }

  /// <inheritdoc/>
  protected override TestResult VersionTest(Version v) {
    return TestResult.Upgradeable;  // Ignore the game's version.
  }
  #endregion
}

}
