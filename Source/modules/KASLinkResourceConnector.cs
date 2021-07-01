// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.MathUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.ResourceUtils;
using System.Collections.Generic;
using System.Linq;
using KASAPIv2;
using KSP.UI;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KAS {

/// <summary>Module which transfer resources between two linked vessels.</summary>
/// <seealso cref="KASLinkSourcePhysical"/>
// Next localization ID: #kasLOC_12018
[PersistentFieldsDatabase("KAS/settings/KASConfig")]
// ReSharper disable once InconsistentNaming
public sealed class KASLinkResourceConnector : KASLinkSourcePhysical,
    // KAS interfaces.
    IHasGUI {

  #region Localizable GUI strings.
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WindowTitleTxt = new Message(
      "#kasLOC_12000",
      defaultTemplate: "Resource Transfer",
      description: "The title of the resource transfer dialog.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> ResourceName = new Message<string>(
      "#kasLOC_12001",
      defaultTemplate: "<<1>>",
      description: "The resource in the transfer options table. Its main purpose is dealing"
      + " with the Lingoona modifiers, applied to the resource name."
      + "\nArgument <<1>> is the full localized resource name with the Lingoona modifiers"
      + " (if any).");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<PercentFixedType, string> CompoundResourceName =
      new Message<PercentFixedType, string>(
          "#kasLOC_12002",
          defaultTemplate: "<<1>> <<2>>",
          description: "The string to present for a fuel mixture component."
          + "\nArgument <<1>> is the percent ratio of the component in the mixture of type"
          + " PercentType."
          + "\nArgument <<2>> is the abbreviated localized resource name with the Lingoona"
          + "modifiers (if any).",
          example: "45 % Ox");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<CompactNumberType, CompactNumberType> ResourceAmounts =
      new Message<CompactNumberType, CompactNumberType>(
          "#kasLOC_12003",
          defaultTemplate: "<<1>> / <<2>>",
          description: "The status string saying current and maximum amounts of the resource in the"
          + " vessel. The gui tags are suggested to define the minimum size of the text, to avoid"
          + " the dialog flickering when the resource is being transferred."
          + "\nArgument <<1>> is the current amount of type CompactNumberType."
          + "\nArgument <<1>> is the maximum amount (capacity) of type CompactNumberType.",
          example: "2.56 / 1,234");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CompactNumberType> TransferSpeedTxt = new Message<CompactNumberType>(
      "#kasLOC_12004",
      defaultTemplate: "Current transfer speed: <<1>> units per second",
      description: "The information string that tells what is the selected or calculated transfer"
      + " speed is.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CloseDialogBtn = new Message(
      "#kasLOC_12005",
      defaultTemplate: "Close dialog",
      description: "The caption on the button that closes the transfer dialog.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> OwnerVesselTxt = new Message<string>(
      "#kasLOC_12006",
      defaultTemplate: "Owner (left): <<1>>",
      description: "The string that tells which vessels owns the resource transfer part. Its stats"
      + " are displayed on the left side of the dialog."
      + "\nArgument <<1>> is the name of the owner vessel.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> ConnectedVesselTxt = new Message<string>(
      "#kasLOC_12007",
      defaultTemplate: "Connected (right): <<1>>",
      description: "The string that tells which vessels is connected to the resource transfer part."
      + " Its stats are displayed on the right side of the dialog."
      + "\nArgument <<1>> is the name of the connected vessel.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> MixtureHint = new Message<string>(
      "#kasLOC_12008",
      defaultTemplate: "A mixture of components: <<1>>",
      description: "The hint to explain the mixture of the fuel components to transfer."
      + "\nArgument <<1>> is the comma-separated list of the component names.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message AutoScaleToggleTxt = new Message(
      "#kasLOC_12009",
      defaultTemplate: "Auto scale transfer speed",
      description: "The caption for the control that enables the mode, which automatically deducts"
      + " the speed of the resource transfer.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CompactNumberType> AutoScaleToggleHint = new Message<CompactNumberType>(
      "#kasLOC_12010",
      defaultTemplate: "The speed will be set so that the transfer is complete in <<1>> seconds",
      description: "The GUI hint that explains what will happen if the auto-speed options is"
      + " chosen.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LeftToRightToggleHint = new Message(
      "#kasLOC_12011",
      defaultTemplate: "Trigger transfer from the connected vessel to the owner",
      description: "The hint text to explain the button action that starts transferring the"
      + " resource from the connected vessel to the owner of the resource transfer part.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LeftToRightButtonHint = new Message(
      "#kasLOC_12012",
      defaultTemplate: "Transfer from the connected vessel to the owner",
      description: "The hint text to explain the button action that does transferring the"
      + " resource from the connected vessel to the owner of the resource transfer part. When the"
      + " button is released, the transfer stops.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RightToLeftToggleHint = new Message(
      "#kasLOC_12013",
      defaultTemplate: "Trigger transfer from the owner vessel to the connected vessel",
      description: "The hint text to explain the button action that starts transferring the"
      + " resource from the owner of the resources transfer part to the connected vessel.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RightToLeftButtonHint = new Message(
      "#kasLOC_12014",
      defaultTemplate: "Transfer from the owner vessel to the connected vessel",
      description: "The hint text to explain the button action that does transferring the"
      + " resource from the owner of the resource transfer part to the connected vessel. When the"
      + " button is released, the transfer stops.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NotAvailableInDockedMode = new Message(
      "#kasLOC_12016",
      defaultTemplate: "Not available in the docked mode",
      description: "The message to present in the transfer dialog when the parts are docked."
      + " Hence, the stock game functionality must be used to transfer the resources.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoResourcesFound = new Message(
      "#kasLOC_12017",
      defaultTemplate: "Not found any resources for transfer",
      description: "The message to present when there are no resources that can be transferred in any direction between"
      + " the vessels.");
  #endregion

  #region Part's config fields
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable CollectionNeverUpdated.Global
  // ReSharper disable ClassNeverInstantiated.Global
  // ReSharper disable ConvertToConstant.Global
  // ReSharper disable FieldCanBeMadeReadOnly.Global

  /// <summary>The maximum allowed speed of transferring a resource.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Transfer speed")]
  public float maxTransferSpeed = 20.0f;

  /// <summary>
  /// The duration of the complete transfer when the speed is selected automatically.
  /// </summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Auto speed duration threshold")]
  public float autoSpeedTransferDuration = 4.0f;

  /// <summary>
  /// Pattern to find the model which will be rotating around X-axis when the hose is
  /// extended/retracted.
  /// </summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string rotatingWinchCylinderModel = "";

  /// <summary>
  /// The total length of the cylinder on the outer radius. It's used to calculate the ratio of how
  /// significantly the cylinder need to rotate when 1m of hose is extended/retracted.
  /// </summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Rotating winch model perimeter")]
  public float cylinderPerimeterLength = 1.0f;

  /// <summary>
  /// The full list of the resources that this part can transfer in the undocked mode. Anything
  /// beyond this list will be ignored.
  /// </summary>
  /// <remarks>
  /// <see cref="resourceOverrides"/> is ignored when the allowed resources list is set.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("allowedResource", isCollection = true,
                   group = StdPersistentGroups.PartConfigLoadGroup)]
  public List<string> allowedResources = new List<string>();

  /// <summary>
  /// The list of the resources that will be forcibly allowed or disallowed for the transfer via
  /// the part.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This settings is only handled when no specific resources are defined for the part. In this
  /// case all the resources on the vessel are allowed to be moved, except the resources that are:
  /// </para>
  /// <list type="bullet">
  /// <item>Not material. I.e. their unit cost or volume is <i>zero</i>.</item>
  /// <item>Not allowed for pumping (e.g. "solid fuel").</item>
  /// </list>
  /// <para>
  /// To override the rule above, the override can be used. List the names of the resources with a
  /// prefix to tell how to handle the resource: prefix "+" means the resource must be allowed to
  /// move no matter what; prefix "-" means the resource(s) must not be allowed to move.
  /// </para>
  /// <para>
  /// The simplest example is <c>ElectricCharge</c> resource, which is not material (no volume).
  /// To allow it on the part, add a positive override: <c>+ElectricCharge</c>. Similarly, to
  /// disallow a resource, add a negative override: <c>-LiquidFuel</c>.
  /// </para>
  /// </remarks>
  /// <seealso cref="allowedResources"/>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("resourceOverride", isCollection = true,
                   group = StdPersistentGroups.PartConfigLoadGroup)]
  public List<string> resourceOverrides = new List<string>();

  /// <summary>Container for the fuel mixture component.</summary>
  // ReSharper disable once ClassNeverInstantiated.Global
  public class FuelMixtureComponent {
    /// <summary>Name of the resource.</summary>
    [PersistentField("name")]
    public string name = "";

    /// <summary>
    /// Weight of the component in the mixture. It can be any number, it will be scaled down to
    /// <c>1.0</c> to get the percentage.
    /// </summary>
    [PersistentField("ratio")]
    public double ratio = 0.0;
  }

  /// <summary>Container for the fuel mixture.</summary>
  public class FuelMixture {
    /// <summary>The mixture components.</summary>
    [PersistentField("component", isCollection = true)]
    public List<FuelMixtureComponent> components = new List<FuelMixtureComponent>();
  }

  /// <summary>List of the supported fuel mixtures.</summary>
  /// <remarks>
  /// The mixture will only be presented if <i>all</i> of the components are present in any of the
  /// vessels.
  /// </remarks>
  [PersistentField("RTS/fuelMixture", isCollection = true)]
  public List<FuelMixture> fuelMixtures = new List<FuelMixture>();

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore CollectionNeverUpdated.Global
  // ReSharper restore ClassNeverInstantiated.Global
  // ReSharper restore ConvertToConstant.Global
  // ReSharper restore FieldCanBeMadeReadOnly.Global
  #endregion

  #region Configuration settings
  // ReSharper disable FieldCanBeMadeReadOnly.Local
  // ReSharper disable ConvertToConstant.Local

  /// <summary>Tells if the control hints should be shown in the transfer dialog.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("RTS/showTransferDialogHints", group = StdPersistentGroups.Default)]
  bool _showTransferDialogHints = true;

  // ReSharper enable FieldCanBeMadeReadOnly.Local
  // ReSharper enable ConvertToConstant.Local
  #endregion

  #region Context menu events/actions
  /// <include file="../SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_12015",
      defaultTemplate = "Open GUI",
      description = "The context menu event that opens the resources transfer GUI.")]
  public void OpenGuiEvent() {
    if (isLinked && !isGuiOpen) {
      isGuiOpen = true;
      SetPendingTransferOption(null);
      _resourceListNeedsUpdate = true;
      MaybeUpdateResourceOptionList();
    }
  }
  #endregion

  #region Local fields & properties
  /// <summary>Actual screen position of the console window.</summary>
  Rect _windowRect = new Rect(100, 100, 1, 1);
  
  /// <summary>A list of actions to apply at the end of the GUI frame.</summary>
  readonly GuiActionsList _guiActions = new GuiActionsList();

  /// <summary>Tells if GUI is open.</summary>
  bool isGuiOpen {
    get => _isGuiOpen;
    set {
      if (value) {
        UpdateResourcesTransferGui(force: true);
      }
      _isGuiOpen = value;
    }
  }
  bool _isGuiOpen;

  /// <summary>GUI table to align resource names and quantities.</summary>
  /// <remarks>Left Name + Left Amount + Right Amount + Right Name</remarks>
  readonly GUILayoutStringTable _guiResourcesTable = new GUILayoutStringTable(4, keepMaxSize: true);

  /// <summary>Definition of all the resources for the both linked vessels.</summary>
  ResourceTransferOption[] _resourceRows = new ResourceTransferOption[0];

  /// <summary>List of resources that can actually be transferred in any direction.</summary>
  /// <remarks>
  /// It's derived from <see cref="_resourceRows"/> and only have rows where both sides of the link have non zero
  /// capacity for the resource.
  /// </remarks>
  ResourceTransferOption[] _canTransferResources = new ResourceTransferOption[0];

  /// <summary>Index of the vessels resources.</summary>
  Dictionary<int, ResourceTransferOption> _resourceRowsHash = new();

  /// <summary>The currently behaving resource transfer.</summary>
  ResourceTransferOption _pendingOption;

  /// <summary>The current resource transfer speed.</summary>
  float _transferSpeed = 1.0f;

  /// <summary>Tells if the transfer speed can be managed by the code.</summary>
  bool _autoScaleSpeed;

  /// <summary>Model of the cylinder to rotate when the hose is extended/retracted.</summary>
  /// <remarks>Can be <c>null</c>.</remarks>
  Transform _rotatingCylinder;

  /// <summary>
  /// Tells if the resources options need to be refreshed from the attached vessels.
  /// </summary>
  bool _resourceListNeedsUpdate;

  /// <summary>Last time the resources counts were updated in GUI.</summary>
  float _lastResourcesGuiUpdate;

  /// <summary>The timeout to update the resources counters in GUI in seconds.</summary>
  /// <remarks>It's a performance affecting settings.</remarks>
  const float TransferStateUpdatePeriod = 0.1f;

  /// <summary> The controller of the game's UI scale.</summary>
  GuiScale _guiScale;
  #endregion

  #region Cached values
  Part _currentFromPart;
  double[] _currentFromPartCapacities;
  double[] _currentFromPartAmounts;
  Part _currentToPart;
  double[] _currentToPartCapacities;
  double[] _currentToPartAmounts;
  #endregion

  #region GUI styles & contents
  GUIStyle _guiNoWrapCenteredStyle;
  GUIContent _autoScaleToggleCnt;
  GUIContent _leftToRightToggleCnt;
  GUIContent _leftToRightButtonCnt;
  GUIContent _rightToLeftToggleCnt;
  GUIContent _rightToLeftButtonCnt;
  #endregion

  #region Local types
  /// <summary>Helper class to hold the data for the transfer option selected.</summary>
  class ResourceTransferOption {
    public readonly int[] resources;
    public readonly double[] resourceRatios;
    public readonly double[] leftAmounts;
    public readonly double[] leftCapacities;
    public readonly double[] rightAmounts;
    public readonly double[] rightCapacities;
    public readonly GUIContent caption = new();
    public readonly GUIContent leftInfo = new();
    public readonly GUIContent rightInfo = new();

    public bool canMoveRightToLeft;
    public bool canMoveLeftToRight;
    public bool leftHasCapacity;
    public bool rightHasCapacity;
    public double previousUpdate;
    
    readonly int _hashCode;

    public bool leftToRightTransferToggle {
      get => _leftToRightTransferToggle;
      set => UpdateTransferTriggerFlag(ref _leftToRightTransferToggle, value);
    }
    bool _leftToRightTransferToggle;

    public bool leftToRightTransferPress {
      get => _leftToRightTransferPress;
      set => UpdateTransferTriggerFlag(ref _leftToRightTransferPress, value);
    }
    bool _leftToRightTransferPress;

    public bool rightToLeftTransferToggle {
      get => _rightToLeftTransferToggle;
      set => UpdateTransferTriggerFlag(ref _rightToLeftTransferToggle, value);
    }
    bool _rightToLeftTransferToggle;

    public bool rightToLeftTransferPress {
      get => _rightToLeftTransferPress;
      set => UpdateTransferTriggerFlag(ref _rightToLeftTransferPress, value);
    }
    bool _rightToLeftTransferPress;

    /// <inheritdoc/>
    public override int GetHashCode() {
      return _hashCode;
    }

    /// <summary>Makes the transfer option.</summary>
    /// <param name="availableResources"></param>
    /// <param name="resourceRatio"></param>
    public ResourceTransferOption(
        IEnumerable<int> availableResources, IEnumerable<double> resourceRatio) {
      resources = availableResources.ToArray();
      _hashCode = resources.Aggregate((t, v) => ((t << 3) | (t >> 29)) ^ v);
      resourceRatios = resourceRatio.ToArray();
      leftAmounts = new double[resources.Length];
      leftCapacities = new double[resources.Length];
      rightAmounts = new double[resources.Length];
      rightCapacities = new double[resources.Length];
      UpdateStaticStrings();
    }

    /// <summary>Updates the GUI strings that don't depend on the amounts/capacities.</summary>
    public void UpdateStaticStrings() {
      if (resources.Length == 1) {
        caption.text = ResourceName.Format(
            StockResourceNames.GetResourceTitle(resources[0], removeLingoonaTags: false));
      } else {
        var texts = new string[resources.Length];
        var totalAmount = resourceRatios.Sum();
        for (var i = 0; i < resources.Length; i++) {
          texts[i] = CompoundResourceName.Format(
              resourceRatios[i] / totalAmount,
              StockResourceNames.GetResourceAbbreviation(resources[i], removeLingoonaTags: false));
        }
        var resourceNames = resources.Select(r => StockResourceNames.GetResourceTitle(r)).ToArray();
        caption.text = string.Join("\n", texts);
        caption.tooltip = MixtureHint.Format(string.Join(" + ", resourceNames));
      }
    }

    /// <summary>Aborts any transfers that were in progress.</summary>
    public void StopAllTransfers() {
      _leftToRightTransferPress = false;
      _leftToRightTransferToggle = false;
      _rightToLeftTransferPress = false;
      _rightToLeftTransferToggle = false;
    }

    /// <summary>Updates the transfer trigger flag.</summary>
    /// <remarks>Ensures that the values are not updated when the property hasn't changed.</remarks>
    void UpdateTransferTriggerFlag(ref bool property, bool newValue) {
      if (property != newValue) {
        if (property) {
          StopAllTransfers();
        }
        property = newValue;
        if (newValue) {
          previousUpdate = Planetarium.GetUniversalTime();
        }
      }
    }
  }
  #endregion

  #region KASLinkSourcePhysical overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    _guiScale = new GuiScale(
        getPivotFn: () => new Vector2(_windowRect.x, _windowRect.y), onScaleUpdatedFn: MakeGuiStyles);
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    RegisterGameEventListener(GameEvents.onVesselWasModified, OnVesselUpdated);
    RegisterGameEventListener(GameEvents.onVesselDestroy, OnVesselUpdated);
    RegisterGameEventListener(GameEvents.onVesselCreate, OnVesselUpdated);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    _rotatingCylinder = Hierarchy.FindPartModelByPath(part, rotatingWinchCylinderModel);
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (_rotatingCylinder != null) {
      if (cableJoint.realCableLength > float.Epsilon) {
        var angle = 360.0f * (cableJoint.realCableLength % cylinderPerimeterLength) / cylinderPerimeterLength;
        _rotatingCylinder.localRotation = Quaternion.Euler(angle, 0, 0);
      }
    }
  }

  /// <inheritdoc/>
  public override void LocalizeModule() {
    base.LocalizeModule();
    _resourceRows.ToList().ForEach(x => x.UpdateStaticStrings());

    _autoScaleToggleCnt = new GUIContent(
        AutoScaleToggleTxt, AutoScaleToggleHint.Format(autoSpeedTransferDuration));
    _leftToRightToggleCnt = new GUIContent("<<", LeftToRightToggleHint);
    _leftToRightButtonCnt = new GUIContent("<", LeftToRightButtonHint);
    _rightToLeftToggleCnt = new GUIContent(">>", RightToLeftToggleHint);
    _rightToLeftButtonCnt = new GUIContent(">", RightToLeftButtonHint);

    // Force the strings loading since their guiTags are used in GUI.
    ResourceName.LoadLocalization();
    ResourceAmounts.LoadLocalization();
  }
  
  /// <inheritdoc/>
  public override void UpdateContextMenu() {
    base.UpdateContextMenu();

    PartModuleUtils.SetupEvent(this, OpenGuiEvent, e => {
      e.active = linkTarget != null && linkTarget.part != null
          && linkTarget.part.vessel != null && !linkTarget.part.vessel.isEVA;
    });
  }

  /// <inheritdoc/>
  protected override void LogicalLink(ILinkTarget target) {
    base.LogicalLink(target);
    _guiResourcesTable.ResetMaxSizes();
  }

  /// <inheritdoc/>
  protected override void PhysicalLink() {
    base.PhysicalLink();
    SetCableLength(float.PositiveInfinity);
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    isGuiOpen &= linkTarget != null && !linkTarget.part.isVesselEVA;
    if (!isGuiOpen || Time.timeScale <= float.Epsilon || !UIMasterController.Instance.IsUIShowing) {
      return;
    }
    using (new GuiMatrixScope()) {
      _guiScale.UpdateMatrix();
      _windowRect = GUILayout.Window(
          GetInstanceID(), _windowRect, TransferResourcesWindowFunc, WindowTitleTxt,
          GUILayout.MaxHeight(1), GUILayout.MaxWidth(1));
    }
  }
  #endregion

  #region GUI methods
  /// <summary>The GUI tooltip control. Only used in the <see cref="TransferResourcesWindowFunc"/> method.</summary>
  readonly GuiTooltip _tooltip = new();

  /// <summary>Shows a window that displays the resource transfer controls.</summary>
  /// <param name="windowId">Window ID.</param>
  void TransferResourcesWindowFunc(int windowId) {
    // In the docked mode the players must use the stock transfer mechanism.
    if (vessel == linkTarget.part.vessel) {
      GUILayout.Label(NotAvailableInDockedMode, _guiNoWrapCenteredStyle);
      if (GUILayout.Button(CloseDialogBtn)) {
        isGuiOpen = false;
      }
      SetPendingTransferOption(null);  // Cancel all transfers.
      GUI.DragWindow();
      return;
    }

    if (_guiActions.ExecutePendingGuiActions()) {
      MaybeUpdateResourceOptionList();
      _guiResourcesTable.UpdateFrame();
      if (_pendingOption != null) {
        if (!DoTransfer()) {
          SetPendingTransferOption(null);  // Cancel all transfers.
        }
      }
      UpdateResourcesTransferGui();
    }

    GUILayout.Label(OwnerVesselTxt.Format(vessel.vesselName), _guiNoWrapCenteredStyle);
    GUILayout.Label(ConnectedVesselTxt.Format(linkTarget.part.vessel.vesselName), _guiNoWrapCenteredStyle);

    // No resources, no transfer.
    if (_canTransferResources.Length == 0) {
      GUILayout.Label(NoResourcesFound, _guiNoWrapCenteredStyle);
      if (GUILayout.Button(CloseDialogBtn)) {
        _guiActions.Add(() => _isGuiOpen = false);
      }
      SetPendingTransferOption(null);  // Cancel all transfers.
      GUI.DragWindow();
      return;
    }

    for (var i = _canTransferResources.Length - 1; i >= 0; i--) {
      var row = _resourceRows[i];
      _guiResourcesTable.StartNewRow();
      using (new GUILayout.HorizontalScope()) {
        _guiResourcesTable.AddTextColumn(row.caption, _guiNoWrapCenteredStyle);
        _guiResourcesTable.AddTextColumn(row.leftInfo, _guiNoWrapCenteredStyle);
        using (new GuiEnabledStateScope(row.canMoveRightToLeft)) {
          row.rightToLeftTransferToggle = GUILayoutButtons.Toggle(
              row.rightToLeftTransferToggle, _leftToRightToggleCnt, GUI.skin.button, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, _guiActions);
          row.rightToLeftTransferPress = GUILayoutButtons.Push(
              row.rightToLeftTransferPress, _leftToRightButtonCnt, GUI.skin.button, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, _guiActions);
        }
        using (new GuiEnabledStateScope(row.canMoveLeftToRight)) {
          row.leftToRightTransferPress = GUILayoutButtons.Push(
              row.leftToRightTransferPress, _rightToLeftButtonCnt, GUI.skin.button, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, _guiActions);
          row.leftToRightTransferToggle = GUILayoutButtons.Toggle(
              row.leftToRightTransferToggle, _rightToLeftToggleCnt, GUI.skin.button, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, _guiActions);
        }
        _guiResourcesTable.AddTextColumn(row.rightInfo, _guiNoWrapCenteredStyle);
        _guiResourcesTable.AddTextColumn(row.caption, _guiNoWrapCenteredStyle);
      }
    }

    // Resource transfer speed.
    _autoScaleSpeed = GUILayoutButtons.Toggle(
        _autoScaleSpeed, _autoScaleToggleCnt, GUI.skin.toggle, null,
        MaybeAutoScaleSpeed, null, _guiActions);
    using (new GuiEnabledStateScope(!_autoScaleSpeed)) {
      _transferSpeed = GUILayout.HorizontalSlider(_transferSpeed, 0f, maxTransferSpeed);
      if (_transferSpeed < float.Epsilon && _pendingOption != null) {
        _guiActions.Add(() => SetPendingTransferOption(null));  // Cancel all transfers.
      }
    }
    GUILayout.Label(TransferSpeedTxt.Format(_transferSpeed));

    if (GUILayout.Button(CloseDialogBtn)) {
      _guiActions.Add(() => isGuiOpen = false);
    }
    if (_showTransferDialogHints) {
      _tooltip.Update();
    }
    GUI.DragWindow();
  }

  /// <summary>Finds the currently active option and makes it active.</summary>
  void GuiActionUpdateTransferItem() {
    var row = _resourceRows.FirstOrDefault(r =>
        r != _pendingOption && (r.leftToRightTransferPress || r.leftToRightTransferToggle
                               || r.rightToLeftTransferPress || r.rightToLeftTransferToggle));
    SetPendingTransferOption(row);
    MaybeAutoScaleSpeed();
  }

  /// <summary>Creates the styles when the scale changes or initializes.</summary>
  void MakeGuiStyles() {
    var skin = GUI.skin; 
    _guiResourcesTable.ResetMaxSizes();
    _guiNoWrapCenteredStyle = new GUIStyle(skin.box) {
        wordWrap = false,
        alignment = TextAnchor.MiddleCenter,
        margin = skin.button.margin,
        padding = skin.button.padding,
    };
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// If the auto-scale options is chosen, finds the scale at which the whole amount of the resource
  /// will be transferred in a definite duration.
  /// </summary>
  void MaybeAutoScaleSpeed() {
    if (!_autoScaleSpeed || _pendingOption == null) {
      return;
    }
    // Determine the maximum unscaled amount to transfer.
    var maxUnscaledAmount = double.PositiveInfinity;
    for (var i = _pendingOption.resources.Length - 1; i >= 0; i--) {
      var unit = _pendingOption.resourceRatios[i];
      var amount = _currentFromPartAmounts[i] / unit;
      if (amount < maxUnscaledAmount) {
        maxUnscaledAmount = amount;
      }
      var capacity = (_currentToPartCapacities[i] - _currentToPartAmounts[i]) / unit;
      if (capacity < maxUnscaledAmount) {
        maxUnscaledAmount = capacity;
      }
    }

    _transferSpeed = Mathf.Min(maxTransferSpeed, (float) maxUnscaledAmount / autoSpeedTransferDuration);
  }
  
  /// <summary>Does actual resource transfer on the selected option.</summary>
  /// <remarks>This method must be performance optimized since it called each frame.</remarks>
  bool DoTransfer() {
    var updateDelta = Planetarium.GetUniversalTime() - _pendingOption.previousUpdate;
    _pendingOption.previousUpdate = Planetarium.GetUniversalTime();
    if (updateDelta < float.Epsilon) {
      return true;  // Cannot do transfer, but the state must not be reset yet.
    }

    // Below a tricky logic starts. It's intended to properly work with the mixtures of multiple
    // resources. When moving a mixture, we should know in advance how much amount of each component
    // can be transferred before the capacity/reserve limit hit.
    var resources = _pendingOption.resources;
    var moveAmounts = new double[_pendingOption.resources.Length];
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      moveAmounts[i] = _transferSpeed * _pendingOption.resourceRatios[i] * updateDelta;
    }
    // Now, check if each component request transfer can be fulfilled.
    var scale = 1.0;
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      _currentFromPart.GetConnectedResourceTotals(
          resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out _currentFromPartAmounts[i], out _currentFromPartCapacities[i]);
      _currentToPart.GetConnectedResourceTotals(
          resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out _currentToPartAmounts[i], out _currentToPartCapacities[i]);
      var amount = moveAmounts[i];
      if (amount > _currentFromPartAmounts[i]) {
        amount = _currentFromPartAmounts[i];
      }
      if (amount > _currentToPartCapacities[i] - _currentToPartAmounts[i]) {
        amount = _currentToPartCapacities[i] - _currentToPartAmounts[i];
      }
      var newScale = amount / moveAmounts[i];
      if (newScale < scale) {
        scale = newScale;
      }
    }
    // Do the transfer with respect to the scale.
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      var resource = resources[i];
      var amount = scale * moveAmounts[i];
      var actualAmount = _currentFromPart.RequestResource(
          resource, amount, ResourceFlowMode.ALL_VESSEL_BALANCE);
      _currentToPart.RequestResource(
          resource, -actualAmount, ResourceFlowMode.ALL_VESSEL_BALANCE);
    }
    return Mathd.AreSame(scale, 1.0);
  }

  /// <summary>Updates GUI for all the resources.</summary>
  /// <remarks>
  /// To not waste too much CPU, this method updates by timer. However, when an instant update is
  /// needed, it can be requested via the parameter.
  /// </remarks>
  /// <param name="force">Tells if GUI must be updated regardless to the timer.</param>
  void UpdateResourcesTransferGui(bool force = false) {
    if (!force && Time.unscaledTime - _lastResourcesGuiUpdate < TransferStateUpdatePeriod) {
      return;
    }
    _lastResourcesGuiUpdate = Time.unscaledTime;
    for (var i = _resourceRows.Length - 1; i >= 0; i--) {
      UpdateOptionTransferGui(_resourceRows[i]);
    }
    _canTransferResources =
        _resourceRows.Where(r => r.leftHasCapacity && r.rightHasCapacity).ToArray();
  }

  /// <summary>Updates the resources amounts and the transfer states in GUI.</summary>
  /// <remarks>This method must be performance optimized.</remarks>
  /// <param name="resOption">The resource transfer option to update.</param>
  void UpdateOptionTransferGui(ResourceTransferOption resOption) {
    var leftInfoString = "";
    var rightInfoString = "";
    resOption.canMoveRightToLeft = true;
    resOption.canMoveLeftToRight = true;
    var leftCapacity = 0.0;
    var rightCapacity = 0.0;
    for (var i = 0; i < resOption.resources.Length; i++) {
      part.GetConnectedResourceTotals(
          resOption.resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out resOption.leftAmounts[i], out resOption.leftCapacities[i]);
      leftCapacity += resOption.leftCapacities[i];
      leftInfoString += (i > 0 ? "\n" : "")
          + CompactNumberType.Format(resOption.leftAmounts[i])
          + " / "
          + CompactNumberType.Format(resOption.leftCapacities[i]);
      linkTarget.part.GetConnectedResourceTotals(
          resOption.resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out resOption.rightAmounts[i], out resOption.rightCapacities[i]);
      rightCapacity += resOption.rightCapacities[i];
      rightInfoString += (i > 0 ? "\n" : "")
          + CompactNumberType.Format(resOption.rightAmounts[i])
          + " / "
          + CompactNumberType.Format(resOption.rightCapacities[i]);
      if (resOption.rightAmounts[i] < double.Epsilon
          || resOption.leftAmounts[i] >= resOption.leftCapacities[i]) {
        resOption.canMoveRightToLeft = false;
      }
      if (resOption.leftAmounts[i] < double.Epsilon
          || resOption.rightAmounts[i] >= resOption.rightCapacities[i]) {
        resOption.canMoveLeftToRight = false;
      }
    }
    resOption.leftHasCapacity = leftCapacity >= double.Epsilon;
    resOption.rightHasCapacity = rightCapacity >= double.Epsilon;
    resOption.leftInfo.text = leftInfoString;
    resOption.rightInfo.text = rightInfoString;
  }

  /// <summary>
  /// Makes the list of all fuels and mixtures that can be moved between the linked vessels.
  /// </summary>
  /// <remarks>This is a very expensive operation.</remarks>
  void MaybeUpdateResourceOptionList() {
    if (!_resourceListNeedsUpdate) {
      return; // Nothing to do.
    }
    _resourceListNeedsUpdate = false;
    HostedDebugLog.Fine(this, "Refreshing resources...");

    // Gather all the resources that *both* vessel have.
    var leftResources = new HashSet<int>(
        vessel.parts
            .SelectMany(p => p.Resources)
            .Select(r => r.info.id));
    var rightResources = new HashSet<int>(
        linkTarget.part.vessel.parts
            .SelectMany(p => p.Resources)
            .Select(r => r.info.id));
    var availableResources = leftResources
        .Union(rightResources)
        .Distinct()
        .ToList();

    // Find the predefined resources that the part can pump between the vessels.
    var allowedResourceIds = allowedResources
        .Select(StockResourceNames.GetId)
        .ToArray();

    if (allowedResourceIds.Length == 0) {
      // If no specific resources set, then allow all the vessel resources that are material and
      // not restricted for pumping. Allow overriding to include/exclude a specific resource. 
      var overrideEnabled = resourceOverrides
          .Where(x => x.Length > 0 && x[0] == '+')
          .Select(x => StockResourceNames.GetId(x.Substring(1)))
          .ToArray();
      var overrideDisabled = resourceOverrides
          .Where(x => x.Length > 0 && x[0] == '-')
          .Select(x => StockResourceNames.GetId(x.Substring(1)));
      var nonMovableIds = PartResourceLibrary.Instance.resourceDefinitions
          .Cast<PartResourceDefinition>()
          .Where(d => overrideEnabled.IndexOf(d.id) == -1
                      && (d.unitCost < float.Epsilon
                          || d.volume < float.Epsilon
                          || d.resourceTransferMode == ResourceTransferMode.NONE))
          .Select(d => d.id)
          .Union(overrideDisabled)
          .ToArray();
      allowedResourceIds = availableResources
          .Where(x => nonMovableIds.IndexOf(x) == -1)
          .ToArray();
    }
    var movableResources = availableResources
        .Where(id => allowedResourceIds.IndexOf(id) != -1)
        // The GUI function will render the list in the reversed order.
        .OrderByDescending(id => id)
        .Select(id => new ResourceTransferOption(new[] {id}, new[] {1.0}))
        .ToList();

    // Add the mixtures.
    var availableMixtures = fuelMixtures
        .Where(m =>
            m.components.All(c => availableResources.Contains(StockResourceNames.GetId(c.name))));
    foreach (var mixture in availableMixtures) {
      movableResources.Insert(0, new ResourceTransferOption(
          mixture.components.Select(x => StockResourceNames.GetId(x.name)).ToArray(),
          mixture.components.Select(x => x.ratio).ToArray()));
    }

    _resourceRows = movableResources
        .Select(resource => _resourceRowsHash.ContainsKey(resource.GetHashCode())
            ? _resourceRowsHash[resource.GetHashCode()]
            : resource)
        .ToArray();
    _resourceRowsHash = _resourceRows.ToDictionary(r => r.GetHashCode());
  }

  /// <summary>Sets the currently transferring option. Erasing the previous one.</summary>
  /// <param name="newOption">The new option or <c>null</c>.</param>
  void SetPendingTransferOption(ResourceTransferOption newOption) {
    if (newOption != _pendingOption) {
      _pendingOption?.StopAllTransfers();
    }
    _pendingOption = newOption;
    if (_pendingOption == null && _autoScaleSpeed) {
      _transferSpeed = 1.0f;
    }
    if (_pendingOption != null) {
      if (_pendingOption.leftToRightTransferPress || _pendingOption.leftToRightTransferToggle) {
        _currentFromPart = part;
        _currentFromPartCapacities = _pendingOption.leftCapacities;
        _currentFromPartAmounts = _pendingOption.leftAmounts;
        _currentToPart = linkTarget.part;
        _currentToPartCapacities = _pendingOption.rightCapacities;
        _currentToPartAmounts = _pendingOption.rightAmounts;
      } else {
        _currentFromPart = linkTarget.part;
        _currentFromPartCapacities = _pendingOption.rightCapacities;
        _currentFromPartAmounts = _pendingOption.rightAmounts;
        _currentToPart = part;
        _currentToPartCapacities = _pendingOption.leftCapacities;
        _currentToPartAmounts = _pendingOption.leftAmounts;
      }
    } else {
      _currentFromPartCapacities = null;
      _currentFromPartAmounts = null;
      _currentToPartCapacities = null;
      _currentToPartAmounts = null;
    }
    UpdateResourcesTransferGui(force: true);
  }

  /// <summary>
  /// Forces an update of the list of the available resources. It's an expensive operation.
  /// </summary>
  void OnVesselUpdated(Vessel v) {
    _resourceListNeedsUpdate = true;
  }
  #endregion
}

}  // namespace
