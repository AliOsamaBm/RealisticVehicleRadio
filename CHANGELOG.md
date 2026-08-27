## [v1.2.1] - Logic Bug Fixes in Loudness & Weather

### Fixed
* Loudness re-evaluation now triggers correctly in **both directions** (e.g., breaking an intact window or opening a closed door will now instantly make the radio loud, instead of requiring the player to exit and re-enter the vehicle).
* Window detection now checks all vehicle glass (including the windshield and rear glass) using SHVDN's `AllWindowsIntact` property, fixing a bug where shooting out the front/rear glass didn't trigger the loud radio mode.
* Thunderstorm interference now correctly detects the weather during active transitions by using SHVDN's native `World.Weather` property, fixing a blindspot when the game was actively transitioning into a storm.

---

## [v1.2.0] - Quality of Life & Performance Fixes

### Added
* Automatic GitHub-based update checker to notify you when a new version is available.

### Improved
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