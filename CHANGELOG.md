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