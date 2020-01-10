* e6b8d52 (HEAD -> master, origin/master, origin/HEAD) Bump changelog
* ef881fd Don't require GamePath for launch behavior
* 1c22a48 Update help dialog with new ini option
* 4bb5c17 Added an option to forcefully kill externals on exit
* 1329e6b Improved Battle.net v2 launch behavior
* 5bcc746 Fixed errant behavior when LauncherPath is invalid
* b79de81 Fixed monitor behavior when tracking Electron
* 5d57c82 (staging) Commit changelog before mainline merge
* a962c51 (origin/staging) Bump the changelog
* 95693db Bump the version number
* 969dc68 Minor build tweaks in the project file
* ca87983 Another attempt at fixing broken EGL behavior
* 19ef8a0 Bump changelog for posterity
* 692120f Implemented an alternate fix for EGL behavior
* 80a84fc Bump changelog again
* 58c1f75 Use EGL specific behavior to fix URI timeout bug
* 56bfbda Bump the changelog
* e3d9770 Just some INI defaults tweaks
* b9dda86 Relaxed process validation before enumerating
* 7fde8e0 Fixed various bugs with process detection
* 92f3728 Don't filter unknown processes during WMI querying
* 3f9e66b Bump version number
* cea4efe Bump changelog
* 935c385 Bug fixes for launcher window detection
* bbd3066 Preserve path data when updating new configs
* e49de1f More bug fixes for EGL and URI launching
* 8b28fc0 Implemented legacy config file migration
* ef29503 Small bug fix for process avoidance when launching game
* 87dfb18 Bug fixes for Battle.net child process detection
* 566a873 Improvements to process enumeration
* 39e42d1 Reimplementation of basic process handling
* 4b0224a Implemented child process detection and validation
* 0338dae Bump the changelog
* 10ef027 Unit tests for config validation and refactoring
* b330afd Bug fixes for Battle.net and MonitorPath behavior
* 9adc0cc Sanitize path inputs and prevent false validation
* 3b087fd Bug fixes for threaded timer behavior
* 3145645 Update Help panel for new config format
* 8a14908 Major rewrite of OSOL internals
* f93714b Normalize line endings in WindowUtils
* 39a9705 Implemented PreGameWaitTime option
* 92f1195 (tag: v1.08d) Bump the changelog and revision again
* 68c0ffd Added pretty app icon and project file fixes
* da67b54 Bump the changelog again
* 54425b7 Fixed an exception in process handling during reacquisition
* 67b985d Bump the changelog
* 4fad8ea Improved process detection of launcher windows
* d29c0ba Improved aggregate exception handling
* 0d9379d Don't check game process for window type to avoid early exit
* 0bbdab3 (tag: v1.08) Sync release changelog before merging with master
* 1d18921 Bump our changelog for posterity
* 1e7c2ef Major overhaul of how process monitoring is done
* a90482f Fixed URI launcher mode not working with a blank LauncherPath
* 48d5519 Be intelligent about newlines when writing to the console
* 946d3b7 Catch null ref of procObj in ProcessMonitor
* 4a4e248 Write logs to the debugger console for convenience
* c350298 Use string interpolation instead of string formatting
* 565b462 Let process monitor return applicable string time format
* a2ee1c7 Prevent handle leaks when force killing processes
* 2018e4f Make sure to log exceptions from ManagementObjectSearcher
* 51f6668 Bumped changelog for posterity
* dfa0df7 Implemented reacquisition of game process if it is respawned within a timeout
* 4d4f8bb Use local time for logging and increase acquisition timeout
* 45d30c0 Some minor fixes to the game detection loop
* 84b1b2a Improvements to process acquisition and monitoring
* 011d437 Shorten the default max process acquisition wait time
* 69e7b2b Prevent exceptions when a rebound process is invalid
* ae3bee2 (tag: v1.07g) Reimplemented process acquisition ... also added a user tuneable to allow customizing process wait timer
* c43d031 (tag: v1.07f) Fixed some games not being detected if their launcher starts minimized
* 5d2d665 Bumped version number for point release
* 19f0797 Fixed Epic Games Launcher not being detected in URI mode
* 9751f47 Code cleanup and abstraction from core Program class
* 3418ca5 Forgot periods in install bullets
* 1ef2952 Changed install instructions to be more concise
* 222a138 (tag: v1.07d) Update Changelog for v1.07d point release
* 353f904 Improved process detection for hidden windows
* 413eef6 Implemented SkipLauncher option and GameArgs for URI mode
* e32b5b5 (tag: v1.07c) Unit tests for BitmaskExtensions and bug fixes
* 928f8de Some more unit tests for Program class
* 2df305e Just list our options to be validated cleanly for readability
* 67f72bd Remove a typo in validated INI options
* 8a5f748 Implemented launcher type auto-detection
* 23ee505 Implement some unit testing
* 1a1265b Fixes for CommandlineProxy when detecting unix-style paths (UE4)
* 929d56b (tag: v1.07b) Code cleanups for ProcessTracking
* fd4b512 Fixes and a workaround for Battle.net launcher
* 1c489b6 Fixes for parsing double-digit ints in affinity mask strings
* c51e65b Help panel revisions to add new INI stubs
* 201d1cf Added ? to the list of help args
* 9b18aa6 Implement suiciding OSOL upon request and some code cleanup
* a78348d Implemented setting game process priority
* 1d628ad Forgot to remove debug code in ProcessTracking
* d99da19 Implemented CPU affinity and fixed launcher path validation
* b500355 Added a note about the dot net redist
* 6a7e344 Code cleanup in ValidateProcTree logic
* ec98ddc (tag: v1.06l) Update Changelog for v1.06l
* 6324422 Make sure ValidateInt always returns a positive integer on parse
* 92b2ee5 Removed redundant PreGameOverlayWaitTime option and improved wait logging
* ae01ec8 Moved PreGameLauncherWaitTime outside of LauncherMode path
* 0a04095 Fix typo in help dialog
* 7ebb270 Enabled DPI awareness and added ForceLauncher for use with CommandlineProxy
*   d6af270 Merge branch 'master' of github.com:WombatFromHell/OriginSteamOverlayLauncher
|\  
| * 3957c97 Update README.md
* | 3634c6c Fixed Win32Exception causing failure to return PID
|/  
* ecde9f5 Bug fixes for CommandlineProxy and Pre-PostGameExec with more code cleanups
*   9489414 Merge branch 'master' of github.com:WombatFromHell/OriginSteamOverlayLauncher
|\  
| * b19d840 Fix Gitter badge formatting
| * e436f25 Add Gitter badge (#22)
* | 0b86d3d Show the name of the process GetProcessTreeHandle has bound to
* | e343385 Make ProcessLauncher more resilient when GetProcessTreeHandle fails
* | aa70c44 Fixed blank LauncherPath exception upon game exit
* | fcd1aa4 Make OSOL less reliant on LauncherPath when starting up
|/  
* c0d1758 Update our changelog
* 120ef5d A second attempt at fixing CommandlineProxy behavior for Tarkov
* a410dfc Move ReLaunch inside ValidatePath and add sanity for DetectedCommandline
* 5a146ce Fix exception in PostGameWaitTime timeout
* c5172cf Convert old-style complex logging to newer method with better comments
* bea55f4 Use arguments from DetectedCommandline when launching game process
* 5afbe29 Revert "Only use launcher behavior if CommandlineProxy is disabled"
* dd7d9f2 Do not relaunch using CommandlineProxy if DetectedCommandline is populated
* ce6d9f9 Only use launcher behavior if CommandlineProxy is disabled
* 7a11ca0 Update the changelog for previous commit
* 1c61b9e Reissue minimize window message after the game exits
* ba49762 Make sure to use copied arguments not the ones from the INI
* 299ec44 Refactored CommandlineProxy support and more code cleanup
* c246446 Support for CommandlineProxy and lots of code cleanup
* 09a874b Further improved process detection when parsing process tree with children
* 236e2cd Implemented utility function to cleanup system tray area after exit
* b3fdc2c Improved process detection so that OSOL can track more launchers
* a65fb24 Updated assembly version
* 2e5045e Fixed hard coded INI read buffer limit of 255 characters
* a7f5892 Added MonitorPath and fixed GameArgs not being read into StartInfo OSOL can now use MonitorPath to monitor a remote executable instead of GamePath reducing process acquisition desyncs
* edcd71c README cleanups
* c809243 OSOL can now be renamed arbitrarily in prep for mediating launcher support
* b086dc7 INI overview changes to reflect MinimizeLauncher option
* e5d4a56 Added MinimizeLauncher option to INI by request
* 9ef5368 Fixed LauncherURI not being used to launch games via URI
* dd40c86 Fix CS in camel typed method
* 2c2cc0e OSOL now displays an INI settings overview when run with /help cli arg
* daff13b Refactoring and exposed more options via INI Added ReLaunch, DoNotClose, and ProxyTimeout options to the INI to expose more customizable behavior wrt the launcher
* c7b0cf2 Fixed incorrect default value in ProcessAcquisitionTimeout option
* f0ceae9 Exposed process acquisition timeout in the INI
* 29d2d72 Added a single global mutex
* c90b426 Update README.md
* 46f1b67 README grammar fix
* 79f2e1d Update README.md
* 967cf1b Updated README with links for bug reporting and wiki
* 6302177 Updated Battle.net launcher uri string table
* 47a8f5b Updated Battle.net launcher strings for Destiny 2
* 83b4e37 (tag: v1.05c) Fixed non-launcher game execution and timing
* 72d4038 Push git log output into Changelog.md before builds
* 00a1dda Add our Changelog.md output to our project build package
* a735a80 Make INI loading smarter when using old configs
* 386d3e0 Refactoring of code base and more tuneables Major code cleanup, user tuneable wait times, loosened search timing of launcher process, launcher process is now optional, and pre-launcher event support in URI mode.
* ce19cde Remove duplicated README and replace with a file link
* 8311da5 Fix our localized README
* 6afffb0 Added customizable post-game wait time to INI
* 378ceb4 Included a donation link for interested parties
* e59f857 (tag: v1.04) Fixed null path validation bug in external process delegate
* fe58b63 Fixed a path validation bug causing a persistent error on startup
* 0912fe0 Added support for pre-launcher post-game executables
* 8171ec9 Typo fix since we now support more than Origin
* e9e5831 More improvements to process detection
* 420db38 (tag: v1.03) Fix urgent thread wait bug causing performance issues
* 072e7c1 Fix the README getting out of sync
* 3525763 Bug fixes and improvements to sanity checking paths
* 5eeeacb Preliminary support for launcher URIs
* 26ef103 Fix our gitignore syntax
* 3736e97 Improved launcher process detection and config support for URIs
* 3f817ca (tag: 1.02) LauncherMode support for launching Origin by itself
* be07453 Config support code for LauncherMode option
* c400e86 Create README.md
* bf93026 (tag: 1.01) Small change for extra sanity in case validator fails
* 752ce03 Refactored process detection
* 8f1465a More refactoring and code cleanups
* 7f9525e Added logging and some code cleanup
* 2bd6ed4 Create README.md
* 6beacee Create README.md
* 8261978 Create README.md
* a57b403 (tag: 1.0) Add files via upload
* 7d8a1a9 Update README.md
* 9ab4ad7 Create README.md
* 7b66d50 Initial commit
