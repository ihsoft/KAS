### 0.5.9 (June 27th, 2016)
- [Fix] #171: KSP 1.1.3 compatibility.

### 0.5.8 (June 12th, 2016)
- [Fix] #167: Loading winch connected same vessel.
- [Fix] #169: Winch motor acceleration is not scaled to FPS.
- [Fix] #168: #Disconnected winch head behaves against the gravity.

### 0.5.7 (2 may 2016)
- [Change] Compatibility change for KSP 1.1.2.
- [Change] Migrate parts to the new enum in KISItem (see KIS 1.2.8 release notes).

### 0.5.6 (21 April 2016)
- [Change] KSP 1.1 supported!
- [Change] Increase hooks break strength with respect to Unity 5 physics change.
- [Change] Increase static attach strength on pylon to prevent joint breakage.
- [Enhancement] Improved search tags and descriptions in parts.
- [Fix] Fix bottom & srf attach nodes on pylon to make it more stable and prevent explosions on physics start.

### 0.5.5 (11 November 2015)
- [Fix] Compatibility update for KSP 1.0.5

### 0.5.4 (29 July 2015)
- [Enhancement] Allow KAS to work without KIS (removed KIS dependancy)
- [Enhancement] Moving harpoons with KIS will not attach them on the ground anymore (attach key
must now be used)
- [Change] Removed old container module (used to move old KAS v0.4 items to KIS items)
- [Fix] Fixed KIS dependancy checker not working as expected

### 0.5.3 (11 July 2015)
- [Fix] Compatibility update for KSP 1.0.4
- [Fix] Updated KAS to use the latest version of KIS (v1.2)
- [Fix] Updated pylon to use the new KIS module for ground attachement

### 0.5.2 (31 May, 2015)
- [Enhancement] Converted parts textures to DDS 
- [Enhancement] AVC is now used for version check
- [Enhancement] Added a KIS version dependancy check 
- [Fix] Added stacking to connector port 
- [Fix] Fixed a compatibility issue with Kerbal Joint Reinforcement  
- [Fix] Updated OnKISAction to use BaseEventData (prevent KAS crash if KIS is missing)

### 0.5.1 (14 May 2015)
- Added a warning if KIS is not detected
- Fixed radial winch not allowing stack attach 

### 0.5.0 (14 May 2015)
- Updated for KSP 1.0.2
- Integrated KIS (KIS is now mandatory)
- Updated port and winch to use KIS "mount" behaviour for attaching hooks on winches
- Parts models update : Connector port, Stack and radial winch, magnet and grappling hook
- New part : harpoon
- Connector port can now be used for linking pipes
- Struts is now a lot stronger
- Disable pause menu when canceling a strut/pipe link (using escape key)
- Updated module 'KASModuleContainer' to move content to KIS storage system if detected
- Module manager is no more needed
- Removed modules depreciated by KIS : KASAddonPointer, KASAddonAddModule, KASModuleGrab, KASModulePartBay,  KASModuleTimedBomb.
- Removed parts : Container Type A & B and bay, Stack connector port, Hook Support, Pipe end point and Horizontal & Vertical radial winch

### 0.4.9 (17 November 2014)
- Updated for KSP 0.90
- Added ModuleManager sanity checks (thx to Starstrider42)
- Rewrite container cost and science computation (thx to angavrilov)

### 0.4.9 (8 October 2014)
- KSP 0.25 compatibility
- Implemented container content cost and recovery (thanks to ozraven)
- Implemented science recovery (thanks to ozraven)
- Converted addModule.cfg to module manager patches (thanks to ozraven)
- Adjusted some parts costs
- Fixed minor UI glitch in container editor (thanks to ozraven)
- Fixed a bug with grapple when the join is broken
- Fixed winch ejection for KSP x64 version
- Fixed retracting cable after using the winch ejection
- Fixed winch head locking in some situation (high gravity planet)
- Fixed KASModuleGrab fixedupdate method in case of inheritance

### 0.4.8 (1 September 2014)
- 0.24.2 compatibility
- Fixed explosions when grabbing a part with x64 version of KSP
- Fixed kerbal teleportation to zero velocity when grabbing a part in space (ex : orbiting moon)
- Fixed NRE when grabbing a stateful part from a container (Thanks to Angavrilov)
- Some Spelling/grammar fixes for part descriptions (Thanks to Dennovin)

### 0.4.7 (1 April 2014)
- Pipes can now be used as fuel lines.
- Stack winches consistently let stack fuel flow through if and only if the head is fully pulled in and locked.
- Fixed an issue with the horizontal stack winch's attach node.
- Fixed winches being permanently broken by non-port parts attached to the head node. This now allows using a decoupler to continue the stack.
- Fixed some explosions when saving and then loading vessels with undocked mode winches in orbit.
- Fixed a serious crash when attaching a port with a plugged winch head to the same vessel as the winch.

### 0.4.6 (16 February 2014)
- Parts can now be rotated around three axes while being attached. The default key bindings still rotate around the Z axis. Hold left alt to instead rotate around the X axis, and hold right alt for the Y axis.
- Part state is now preserved when parts are stored in containers. This means resource levels are stored as expected, and properties like whether solar panels have broken are also preserved. The mass of the container takes stored resource mass into account. Note that part statefulness must be enabled in the grab module config.
- Modules on parts retrieved from containers are now properly initialized. This means that, for example, solar panels will function properly when attached without reloading the scene.
- Containers are now opened through a context menu when in the editor.
- The small three-rung ladder can now be grabbed and stored.
- The medium dish antenna can now be grabbed and stored.
- Winch heads are now properly dropped when boarding a vessel.
- Tweaked rover wheel placement when held on EVA.
- Implemented [CompatibilityChecker (version 2)](http://forum.kerbalspaceprogram.com/threads/65395-Voluntarily-Locking-Plugins-to-a-Particular-KSP-Version?p=901682&viewfull=1#post901682) warnings.
- Fixed an issue where pipe colliders would exist transiently. This removes the small camera jump when linking pipes, and it may prevent spontaneous base explosions.
- Fixed an issue where unlinking a pipe cycle would break things.
- The control state of the largest vessel is preserved when two vessels are connected.
- Fixed an issue where an internal position for grabbed parts would get stuck on a bad value.
- Fixed an issue where ladder parts were improperly initialized.
- Fixed an issue where KAS would instantiate parts with the wrong size.
- Fixed an issue where vessel control state could be reset when locking or unlocking a winch piece.
- Fixed an issue where a vessel's rigidbody could be in the wrong position after being on rails.
- Fixed an issue where attach parts like struts and pipes would not reattach on vessel load if they were not active (staged).
- Part names are no longer modified when parts are instantiated by KAS.
- Docked state is now symmetric (both modules know they are docked) and redundant docking events have been removed.
- Reduced the performance overhead of the grab module.
- Struts, pipes and winches now clean themselves up on part destruction.
- Keystrokes are ignored while an edit field is active.
- Unlocking winches now works with any joint type.
- Fixed an ID conflict between the winch and container windows.

#### 0.4.5 (17 December 2013)
- Added parts to the stock tech tree.
- Updated module info strings in the editor part tooltips. Most of the strings have been simplified.
- EVA must be in range of both the source and destination in order to attach a part.
- The attach pointer will only turn yellow (indicating out-of-range) if the target surface is valid.
- "Drop" and "Grab" buttons will correctly change state in open context menus.
- Pipes and struts can no longer be linked while a Kerbal is carrying one end.

#### 0.4.4 (16 October 2013)
- The container editor will only show parts that have been unlocked when in career mode.

#### 0.4.3 (16 October 2013)

#####Bug Fixes 
- Attach pointer will no more disappear (for exemple after going to the map mode)
- Winches control key on warp are now disabled to avoid issues (mainly explosions)
- Warping with an hook or a part attached at a high velocity (in space) will no more cause explosions
- Warping with a part grabbed is now fixed (freezed kerbal)
- Fixed drop after warp in space
- Some physic handling changes that can cause less explosions (hopefully)

#### 0.4.2 (8 October 2013)

#####Bug Fixes 
- Grabbing and attaching hooks is now working correctly
- Magnet cannot be attached without a power source anymore
- Added a new button on the connector for mounting the hook whithout dropping it
- Attach node on the horizontal stack winch are now correctly placed
- Winches GUI cannot be opened anymore with the "P" key when no winches are present on the vessel
- Debug warning message "...heightFromTerrain are negative..." do not show up anymore when vessel are not landed
- Changed shortcut key numpad "." (eject) to numpad 8 as it was already used for showing/hidding the navball
- Magnet on/off context menu are now hidden when the part is grabbed
- Moved some stuff in the code
- Added the last module I worked on (prototype state, no part yet)

#### 0.4.1 (4 October 2013)

#####Bug Fixes 
- Container mass are now correctly calculated
- Hopefully fixed under ground part spawning bug after warping/loading
- Universe will no more vanish when the coupled vessel was the active vessel (can happen with winches)
- Universe will no more vanish sometimes after loading
- Ground attached object now re-attach correctly to the ground after save/load
- Added a warning message in log if heightFromTerrain get negative (just in case, related to under ground part spawning) 
- Fixed winch connector which disconnects after a save/load
- Removed pipe and strut tube renderer from attach pointer
- Attaching a pipe or strut linked now unlink automatically
- Struts now use the correct texture
- Adjusted the coupling behaviour 

#### 0.4.0 (1 October 2013)
#####Features
- Added eva constructible struts and pipes 
- Added containers for storing grabbable parts
- Added supports for storing containers on the ship
- Winch & connector are now merged into the same part
- Rewrited the magnet module to make it usable without a winch.
- Added a configuration file for adding grab module to parts without modifying their original .cfg file 
- Some smallest stock parts are now natively grabbable and storable in container (RCS Block, battery, etc...)
- Added two new radial winches (horizontal and vertical)
- Added a new part (pylon) as ground support for attaching stuff (pipes, solar panels, batteries, etc...)
- Added a new part for attaching a hook in a fixed position (hook support)
- Grabbed parts now move in accordance with the eva kerbal movement
- Added a "reaction wheel" to connector ports for rotating winch attached part
- Part bay module now support multiple storage position
- Existing attach node on parts are now used for grab and attach
- Winch speed can now be changed from the winch GUI.
- Winch now have an acceleration effect
- Anchor will now sink in the water
- Kerbal EVA can attach to a winch while a part is grabbed.
- Attach mode now align the part to the target orientation by default.
- Added an option in the config file for enabling physic joint between eva and grabbed part.
- Grab module now support multi-colliders parts (ladders, wheels, etc...)
- Added a setting file for configuring keyboard shortcuts

#####Bug Fixes and Tweaks
- Fixed attach problem appearing when orbiting some planets (mun, minmus, etc...)
- Grabbed parts will now be dropped before entering a pod instead of being destroyed
- Using winch keyboard control key will no more give input to others winches nearby
- Alone parts attached on the ground will no more disapear after a save/load/warp
- Greatly reduced sounds radius of KAS parts
- Changed some default keyboard shortcut
- Removed the attraction effect of the magnet (better performance and unnecessary complexity)
- Removed the portable connector port and made the standard one grabbable
- Removed the hook bay as containers are now better for storing parts
- Debug menu can be used by pressing a key combination instead of adding lines to part.cfg
- Rewritten most of the modules, moved some to KSPAddon
- Cleaned code and removed unused stuff
- Misc fixes

#### 0.3.1 (29 May 2013)
- Ignoring sound(s) or texture(s) not loaded (in case of a wrong installation directory), instead of messing up KAS parts behaviour
- Warning message if a sounds or a texture is not found by the plugin. 

#### 0.3 (27 May 2013)
 - KSP 0.20 Compatibility
 - Sounds & textures are now loaded properly from the new game database system
 - Anchor is back (temporary model however). In addition to its weight, it also provide drag and friction when touching the ground
 - Attach mode will now show a preview of the part destination instead of a sphere. Rotation can also be modified by pressing "b" and "n" keys
 - Grabbed part mass is now added to eva mass for realism (can be disabled in the part.cfg)
 - Hooks are now correctly aligned in the hook bay when stored
 - Added a type parameter for the partBay module
 - Part bay grab/store context menu reworked
 - Separated EvaGrab module into two dedicated modules (KASModuleGrab & KASModuleAttach)
 - Added sound path parameters to each module
 - New action groups for enabling/disabling key control
 - New action groups for enabling/disabling the GUI
 - New action groups and context menu to Invert key control
 - Camera no more reset after deploying a hook

#### 0.2.3 (1 May 2013)
- Fixed collisions between plug docked parts leading to unwanted explosions
- Fixed some functions not working properly when winch and connector are reverted from the root part order 
- Switching to docked mode will now correctly switch vessel and set neutral control to it
 
#### 0.2.2 (28 April 2013)
- Fixed grappling-hook which became buggy after a save/load
- Fixed slowness caused by the GUI. (thanks to a.g.)
- Fixed retract after using eject

- Removed hook grab context menu for hook stored in the hook bay
- Removed "Unplug and grab" action on plugged mountable connector port
- Restricted winch to connector only to avoid non supported configuration
- Checking connector if upside down
- Added an attach orientation parameter for the evagrab module

#### 0.2.1 (23 April 2013)
- Fixed disconnected/disappearing connector after a save/load in certain circumstances.

#### 0.2 (22 April 2013)
Features :
-	Major overhaul of the winch system, introducing the new Connector/Port system
-	Exchangeable hooks.
-	New parts : Connector, 0.5m and small radial connector port
-	New parts : 0.5m horizontal and vertical winch
-	New parts : grappling hook and hook bay
-	New models : Electro magnet
-	Winches can now eject parts locked (for grappling hook)
-	Added an button to instantly strain the cable. 
- 	Improved parts description in editor.
-	GUI menu reworked, added some new actions.
-	Winches can be renamed on the GUI.
-	New sounds, reduced mod size by using .ogg files.
- 	Added control of the winches with the numpad key (extend, retract, eject, current hook action)
-	Pressing grab key will not more grab already attached, grabbed or winch locked parts. Context menu are now the only way to grab this parts.
-	Added eva context menu and key press control on the connector for winch control (release, retract, extend)
-	Improved hook state context menus

Bug Fixes and Tweaks:
-	Stacking winches and grab the hook on one of the attached winch no more misalign the cable position.
-	Hook always return to the original position on lock instead of moving more and more at each lock.
-	Hook with child parts now return to the correct position after load/save/warp.
-	Misc bugfixes

#### 0.1.1
- Fixed missing cable texture and sounds for the Mac version.
- Added messages when electricity is depleted.
- Added cable distance before strain on the winch GUI
- Fixed grab when hooks are attached together.

#### 0.1
- Initial release
