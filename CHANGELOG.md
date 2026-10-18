## [v1.3.0] - Performance Overhaul, Thunderstorm Effects, Tunnel Improvements, Radio Static, & Radio Amplifier Vehicles

### Added
* New `Performance` configuration section under `RealisticVehicleRadio`.
* New performance settings: `VehiclesSearchDistance`, `MaxProcessVehicles`, `LogicUpdateIntervalMs`, `ScanIntervalMs`, `MaxCachedVehicles`, `MaxProcessDistance`, and `RemoveDistance`.
* New tunnel behavior settings: `MuteRadioInTunnels` and `EnableStaticRadioInTunnels`.
* New loudness setting: `LoudEchoVehicleCount`, exposed in the menu as `Radio Amplifier Vehicle Count`.
* New `Radio Amplifier Vehicle Count` option in the Loudness Settings menu.
* New thunderstorm background-static settings: `EnableThunderstormStatic`, `StaticSignalMin`, `StaticSignalMax`, `StaticDriftSpeed`, and `StaticHissIntensity`.
* New thunderstorm static-pop settings: `EnableStaticPop`, `StaticPopChancePercentage`, and `StaticPopMaxIntensity`.
* New `NormalizeMinMaxPairs()` method for normalizing thunderstorm Min/Max pairs after loading from JSON.
* New `ResetToDefaults()` support for restoring configuration values from a fresh default configuration.
* Asynchronous load and save APIs for configuration and tunnel JSON files.
* Semaphore-based locks for configuration and tunnels, serializing access to each file independently.
* Generic fallback loader for reading a main JSON file, then a backup file, then falling back to a default.
* Cached JSON serializer settings for normal saving/loading and settings comparison.
* Contextual success message when a backup configuration or tunnels file is loaded after the main file fails.
* Async settings-section save operation that compares and writes only when changes are detected.
* 2D Perlin noise generator to `MathFunctions`.
* `Clamp` helpers for `float` and `int` values.
* Contextual GUI message helper that switches between subtitle text and help text based on menu visibility.
* Main-thread message queue for deferred GUI messages via `ProcessMessages`.
* Thread-safe logging path using a dedicated writer lock.
* New **Performance Settings** menu with options for:
  * `Vehicles Search Distance`
  * `Max Process Vehicles`
  * `Logic Update Interval`
  * `Scan Interval`
  * `Max Tracked Vehicles`
  * `Max Process Distance`
  * `Remove Distance`
* Live performance statistics in the Performance Settings menu descriptions, including current found vehicles, processed vehicles, skipped vehicles, tracked vehicles, and vehicles pending removal.
* New tunnel management flow with a tunnel selection list and a separate **Edit Tunnel** menu.
* New tunnel editing actions:
  * **Rename**
  * **Teleport to Start**
  * **Delete**
* New **Reset All Settings to Default** button in the main Settings menu, with confirmation and full configuration reset support.
* New thunderstorm background static settings:
  * `Enable Background Static`
  * `Static Min Signal`
  * `Static Max Signal`
  * `Static Drift Speed`
  * `Static Hiss Intensity`
* New thunderstorm static pop settings:
  * `Enable Static Pop`
  * `Static Pop Chance`
  * `Static Pop Max Intensity`
* Thunderstorm Min/Max inputs now automatically adjust their paired value in the menu to keep `Min` from exceeding `Max`.
* Manual Recording menu now disables menu mouse behavior so the gameplay camera can move to view raycast hits.
* Exclusion saving now notifies the main script to clear cached exclusion booleans after changes are saved.
* Menu save operations now run asynchronously using `FileOperations.SaveSettingsSectionAsync`.
* Menu save actions are now wrapped with exception handling and logging.
* Support for drawing tunnel debug visuals from `IReadOnlyList<TunnelPoint>` instead of requiring a `TunnelPoint[]` array.
* Cached vehicle projection result for the tunnel debug projection marker.
* Dedicated helper methods for finding closest tunnel projections and drawing projection markers.
* Asynchronous tunnel loading via `LoadAsync`.
* O(1) tunnel lookup by ID using an internal dictionary and `GetTunnelById`.
* New `TunnelsChanged` event raised when tunnels are added, deleted, or renamed.
* New `RenameTunnel` method for updating tunnel names and saving the change.
* Load-time tunnel sanitization, including null checks, missing point-list initialization, and skipping tunnels with fewer than two points.
* Duplicate tunnel ID handling during load, using temporary IDs followed by sequential reassignment.
* Contextual user messages for tunnel add/delete results and errors.
* `ManualPointCount` property exposing the live unsaved manual point count.
* Automatic deletion of nearby interfering peds and vehicles while recording, with safety checks to never delete the player or the player’s current vehicle.
* Vehicle-lost aware messaging for:
  * `HandleToggleKey`
  * `ClearManualPoints`
  * `SaveManualChanges`
* Separate `ShowPendingMessage` and `StartPendingSaveIfReady` helpers for processing queued tunnel saves.
* `TryDequeuePendingSave` and `IsSaveActive` helpers used by the blocking flush path.
* Background tunnel saves now use the asynchronous `FileOperations.SaveTunnelsAsync` API.
* New `LoudnessEvaluator` class that centralizes loud-radio evaluation, tunnel/water muting checks, and radio enable/disable application.
* New `ThunderInterference` class that manages thunderstorm mute bursts and background static signal level.
* New `KeybindOverlay` class that handles the key-binding fullscreen overlay, typewriter text, fade animation, and color-code skipping.
* New Radio Amplifier Vehicles feature that makes loud vehicle radios more audible after the player exits the vehicle.
* New `LoudEchoManager` class that manages Radio Amplifier Vehicles.
* Radio Amplifier Vehicles spawn invisible ghost vehicles attached to the player's last vehicle when the vehicle has loud radio, radio enabled, and engine running.
* Radio Amplifier Vehicles use the last known radio station of the player's vehicle.
* Radio Amplifier Vehicles are automatically removed when the player enters the same vehicle again.
* Radio Amplifier Vehicles are disabled while the vehicle is muted by tunnel logic or thunderstorm interference.
* Setting `Radio Amplifier Vehicle Count` to 0 disables the feature and removes active Radio Amplifier Vehicles.
* Radio Amplifier Vehicles are excluded from normal vehicle scanning and processing.
* Temporary invisible driver handling is used for Radio Amplifier Vehicles to reduce radio station resets.
* New `VehicleExclusionManager` class for checking whether a vehicle hash is excluded.
* Player-specific tunnel effect handling, including tunnel static/mute behavior and contribution to the global radio signal level.
* Centralized global radio signal manager using `SET_GLOBAL_RADIO_SIGNAL_LEVEL`, applying the lowest active signal level between thunderstorm interference and player tunnel effects.
* Thunderstorm background static support using Perlin-style organic signal drift.
* Thunderstorm static pop support using random sharp signal deductions.
* Struct-based vehicle tracking with `VehicleEntry` and `VehicleRadioState` to reduce heap allocations.
* Player vehicle promotion/demotion logic when entering, switching, or leaving vehicles.
* Separate handling for the player vehicle versus cached NPC vehicles.
* Manual recording now clears nearby interfering peds and vehicles every frame while active.
* Manual recording now automatically saves or clears unsaved points with a vehicle-lost message if the vehicle becomes invalid.
* Automatic tunnel recording now supports vehicle-lost stopping and messaging.
* Key binding assignment can now be cancelled with `Escape`.
* Exclusion cache invalidation callback when vehicle exclusion settings change.
* Temporary invisible driver ped cleanup during logic updates and script abort.
* Script abort now restores the global radio signal level to normal.
* Hybrid exclusion lookup that uses a fast linear array search for small exclusion lists and a `HashSet<VehicleHash>` for larger lists.
* Automatic switch to `HashSet` lookup when the configured exclusion list reaches 4 or more entries.
* Cached last-seen exclusion array so the lookup structure is only rebuilt when the configuration array changes.

### Improved
* `VehiclesSearchDistance` and `MaxProcessVehicles` are now part of the dedicated `Performance` section instead of `MainSettings`.
* Configuration values now use safer validation and clamping, including minimum values for distances/intervals and 0–100 clamps for percentage settings.
* `OpenMenuKey`, `DebugToggleKey`, and `RecordToggleKey` now fall back to their default keys when the configured value is null, empty, or whitespace.
* `VehiclesWithNoRadio` now uses defensive array cloning to avoid sharing array references.
* Reset/copy logic now copies nested configuration objects in place and clones arrays instead of reusing references.
* Safe file writing is now asynchronous and forces disk flushing without blocking the calling thread.
* Fallback loading logic is now centralized, reducing duplicated code and cyclomatic complexity.
* Settings-section saving now uses shared comparison serializer settings instead of creating new settings each time.
* Save feedback now uses contextual GUI messages instead of ticker notifications.
* File operations are now organized under the `RealisticVehicleRadio` namespace.
* Logger initialization is now lazy instead of creating the log writer during type initialization.
* Logging now fails more safely when the log file or directory cannot be created.
* `EnsureDirectoryExist` now returns a success/failure result instead of using exception-driven control flow.
* Read-only directory recovery is now handled by a dedicated helper.
* Error messages are now routed through the contextual GUI message system.
* Helpers are now organized under the `RealisticVehicleRadio` namespace.
* Main Settings now focuses on core radio behavior, while performance-related options are moved to the dedicated Performance Settings menu.
* Tunnel radio behavior is now split into separate options:
  * `Mute Radio In Tunnels`
  * `Static Radio In Tunnels`
* Numeric menu input now uses clamp-based validation instead of loose validator callbacks.
* Numeric parsing and formatting now use invariant culture to reduce locale-related input issues.
* Configuration range enforcement is now handled by the configuration classes, simplifying menu input logic.
* Menu feedback now uses contextual GUI messages instead of directly posting ticker notifications in many places.
* Tunnel Manager now automatically refreshes when tunnel data changes.
* Tunnel Manager now shows an empty state message when no tunnels are recorded.
* Key binding assignment now uses a dedicated overlay helper instead of manually managed overlay text state.
* Menus are now organized under the `RealisticVehicleRadio` namespace.
* Tunnel debug drawing now uses squared distance checks for max draw distance, avoiding unnecessary distance calculations.
* Cross-sections are now drawn once per tunnel point instead of being redrawn from adjacent segment endpoints.
* `DrawAllTunnels` no longer converts tunnel point collections to arrays before drawing.
* Vehicle projection debugging now reuses cached projection results between recalculations.
* Debug visualizer code is now organized under the `RealisticVehicleRadio` namespace.
* Tunnel add/delete operations now keep the ID lookup dictionary synchronized.
* `AddTunnel` now rejects tunnels with null points or fewer than two points.
* `DeleteTunnel` now reports when the requested tunnel ID does not exist.
* Tunnel notifications now use contextual GUI messages instead of ticker notifications.
* `TunnelManager` is now organized under the `RealisticVehicleRadio` namespace.
* Automatic recording sessions with fewer than two points are now cleared after showing the insufficient-points message.
* Recorder feedback now uses contextual GUI messages instead of direct ticker/help text messages.
* `TunnelRecorder` is now organized under the `RealisticVehicleRadio` namespace.
* Tunnel save processing is now split into smaller, clearer helper methods.
* Pending save messages now use contextual GUI messages instead of direct ticker notifications.
* `FlushBlocking` now uses the asynchronous tunnel save API and waits synchronously only during shutdown.
* `TunnelSaveQueue` is now organized under the `RealisticVehicleRadio` namespace.
* Update check messages now use contextual GUI messages instead of direct ticker notifications.
* `UpdateChecker` is now organized under the `RealisticVehicleRadio` namespace.
* Script version constant renamed from `CurrentScriptVersion` to `CURRENT_SCRIPT_VERSION`.
* Vehicle scanning is now separated from heavy vehicle logic and uses its own scan interval.
* Vehicle exclusion checks are now lazily computed and cached per vehicle instead of being fully recomputed every logic tick.
* Vehicle processing now uses squared-distance checks for max process distance and removal distance.
* NPC vehicle cache now uses backward iteration for cheaper removals without shifting list elements.
* Vehicle cache now uses a `HashSet` of handles to prevent duplicate tracked vehicles.
* Water depth checking now reuses a shared `OutputArgument` instead of allocating one per vehicle.
* Loudness re-evaluation now tracks the last random chance percentage, so changing the percentage triggers re-evaluation.
* Thunderstorm interference is now handled by a dedicated class and exposes a target signal level instead of only a boolean mute state.
* Tunnel spatial index rebuilding now accounts for tunnel muting, tunnel static, and debug visualizer usage.
* Script interval now stays active during player tunnel effects, vehicle enter/exit transitions, and keybind overlay activity.
* Many script messages now use contextual GUI messages instead of direct help/subtitle/ticker calls.
* Keybind overlay state is now handled by `KeybindOverlay` instead of public menu overlay fields.
* Configuration loading now normalizes thunderstorm Min/Max pairs before use.
* Main script is now organized under the `RealisticVehicleRadio` namespace.

### Fixed
* Thunderstorm Min/Max settings are now normalized after JSON deserialization to ensure `Min` values never exceed `Max` values.
* `File.Replace` could fail with an `IOException` when an existing `.bak` file was present; the old backup is now deleted before replacement.
* Read-modify-write race conditions when saving a settings section are now prevented by holding the configuration lock for the entire transaction.
* Logger type initialization could throw if the log writer could not be created; the writer is now initialized lazily and avoids type-init exceptions.
* A failed log writer could remain unusable; failed writers are now disposed and reset so the next logging attempt can create a fresh writer.
* Duplicate tunnel IDs loaded from the tunnels file are now repaired by assigning temporary IDs and then reassigning sequential IDs.
* High-speed recording could leave gaps between tunnel points; missing points are now interpolated between recorded samples.
* Broken or open hoods/trunks no longer count as open doors for loud radio evaluation.
* Driverless NPC vehicles could have their radio station reset when muting/unmuting; a temporary invisible driver ped is now used during the radio enable/disable call to preserve the station.
* Radio station changing during mute and unmute.

### Removed
* `VehiclesSearchDistance` and `MaxProcessVehicles` from `MainSettings`; they are now located in `PerformanceSettings`.
* `DisableRadioInTunnels`; tunnel behavior is now controlled by `MuteRadioInTunnels` and `EnableStaticRadioInTunnels`.
* Synchronous `LoadConfiguration`, `SaveConfiguration`, `LoadTunnels`, `SaveTunnels`, and `SaveSettingsSection` methods were replaced by asynchronous equivalents.
* Immediate automatic saving of a default configuration file when both main and backup configuration files fail to load.
* Immediate static log writer creation during type initialization.
* Recursive/exception-driven error handling in `EnsureDirectoryExist`.
* Direct ticker notification posting from `DisplayAndLogError` in favor of contextual message handling.
* `VehiclesSearchDistance` and `MaxProcessVehicles` from Main Settings; they are now part of Performance Settings.
* Old `DisableRadioInTunnels` checkbox, replaced by separate mute and static tunnel options.
* Old manual key-binding overlay fields and typing animation helpers:
  * `FullOverlayText`
  * `TypedOverlayText`
  * `TypingIndex`
  * `LastTypeTime`
  * `Alpha`
  * `GetTypingText`
  * `DrawFadeOverlay`
* `PendingKeyBind` enum and related pending key state.
* Synchronous menu save actions, replaced by asynchronous save handlers.
* Array-based `DrawCapturedTunnelDebug` signature; replaced by `IReadOnlyList<TunnelPoint>`.
* Special-case yellow/cyan drawing for a single captured tunnel point.
* Synchronous `Load` method; replaced by `LoadAsync`.
* Direct `Notification.PostTicker` tunnel messages.
* Unsanitized tunnel loading that accepted loaded tunnel data without validation.
* `IsAutomaticRecordingActive` property.
* `GetLiveManualRecording` method.
* `DeleteNearestTunnel` method.
* Automatic reset of `PendingTunnelName` after saving a manual tunnel.
* Direct ticker/help-text notifications in favor of contextual messages.
* Direct `Notification.PostTicker` calls for background tunnel save messages.
* Synchronous `FileOperations.SaveTunnels` calls in the save and flush paths.
* Inline dequeue/state-check logic in `FlushBlocking`, replaced by dedicated helper methods.
* Direct `Notification.PostTicker` calls for update-check messages.
* Old `CurrentScriptVersion` constant name; replaced by `CURRENT_SCRIPT_VERSION`.
* Inline loudness, tunnel/water, and thunderstorm logic from the main script; this logic is now handled by dedicated classes.
* Old dictionary-based vehicle state tracking.
* Old touched-radio and touched-loud cleanup sets.
* Old cached vehicle array and per-tick active-handle/removal-buffer cleanup.
* Old built-in thunderstorm state machine and random thunder helper from the main script.
* Old keybind overlay fields and drawing methods from the menu/script path, replaced by `KeybindOverlay`.
* Direct use of many `Screen.ShowHelpText`/`ShowSubtitle` calls in favor of contextual messages.
* Old synchronous configuration and tunnel loading during script startup, replaced by blocking async load calls.

---

## [v1.2.2] - Slight Cleanup Optimization & Menu Bug Fix

### Improved
* Cleanup logic now reuses an existing collection instead of allocating a new collection on every logic tick, reducing unnecessary garbage collection overhead.

### Fixed
* Vehicle radio state could remain disabled after a vehicle entered a tunnel, had its engine turned off, and then left the tunnel without restarting the engine. The radio state is now correctly restored when the engine is off.
* Vehicle exclusion menu, the pending vehicle list remains correctly initialized on startup and continues to be updated by the Add/Remove buttons.

---

## [v1.2.1] - Logic Bug Fixes in Loudness & Weather

### Fixed
* Loudness re-evaluation now triggers correctly in **both directions** (e.g., breaking an intact window or opening a closed door will now instantly make the radio loud, instead of requiring the player to exit and re-enter the vehicle).
* Window detection now checks all vehicle glass (including the windshield and rear glass) using SHVDN's `AllWindowsIntact` property, fixing a bug where shooting out the front/rear glass didn't trigger the loud radio mode.
* Thunderstorm interference now correctly detects the weather during active transitions by using SHVDN's native `World.Weather` property, fixing a blindspot when the game was actively transitioning into a storm.

---

## [v1.2.0] - Quality of Life & Performance Fixes

### Added
* Automatic GitHub-based update checker to notify you when a new version is available.
* "Reset to Defaults" and "Save Changes" buttons now behave correctly — resetting only stages the changes in the menu, and you must press Save to actually apply them to the game and JSON file.
* Cleanup logic on script abort now properly resets the radio states of all vehicles that were processed by the script, rather than just the ones currently cached in the dictionary.

### Fixed
* Cached vehicle states not refreshing correctly when a loudness feature (roof, windows, doors) is toggled on or off via the configuration menu.
* Redundant native calls being sent to the game engine for every newly spawned/processed vehicle, further improving performance.
* Menu items (like checkboxes) sometimes showing a mismatched status compared to the actual runtime state when reopening the menu.
* Manual recording preview debug lines remaining visible on screen even when the main Debug Mode was turned off.

---

## [v1.0.0] - Initial Release

### Added
* Dynamic radio muting inside custom recorded tunnels.
* Radio muting when vehicles are deeply submerged in water.
* Loud radio mode for open roofs, broken windows, and open/broken doors.
* Random chance for loud radio in fully closed vehicles.
* Comprehensive vehicle class and model exclusions.
* Built-in automatic and manual tunnel recorder.
* Real-time debug visualizer for tunnel boundaries and recording previews.
* Spatial-hashing grid architecture for zero-FPS-impact tunnel detection.
* LemonUI configuration menu with keybinding support.
* JSON-based storage for settings and tunnel coordinates.
* Async based I/O operations.
* Predefined tunnels.
* Support for both versions of GTA V (Legacy and Enhanced).