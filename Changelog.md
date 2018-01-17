* 754b8de (HEAD -> master, origin/master) Support for CommandlineProxy and lots of code cleanup
* 0688cff (tag: v1.06e) Further improved process detection when parsing process tree with children
* 6511b24 Implemented utility function to cleanup system tray area after exit
* c6ae077 Improved process detection so that OSOL can track more launchers
* a4bfcb2 (tag: v1.06b) Updated assembly version
* 0b9b55a Fixed hard coded INI read buffer limit of 255 characters
* 0b19679 (tag: v1.06a) Added MonitorPath and fixed GameArgs not being read into StartInfo OSOL can now use MonitorPath to monitor a remote executable instead of GamePath reducing process acquisition desyncs
* a0200b4 README cleanups
* f24aa9a OSOL can now be renamed arbitrarily in prep for mediating launcher support
* 0f762e9 INI overview changes to reflect MinimizeLauncher option
* 648d4ad Added MinimizeLauncher option to INI by request
* c631222 Fixed LauncherURI not being used to launch games via URI
* bc39dc5 Fix CS in camel typed method
* 37e9a44 OSOL now displays an INI settings overview when run with /help cli arg
* 667251e Refactoring and exposed more options via INI Added ReLaunch, DoNotClose, and ProxyTimeout options to the INI to expose more customizable behavior wrt the launcher
* 03fd684 Fixed incorrect default value in ProcessAcquisitionTimeout option
* 2771118 Exposed process acquisition timeout in the INI
* 29d2d72 Added a single global mutex
* c90b426 Update README.md
* 46f1b67 README grammar fix
* 79f2e1d Update README.md
* 967cf1b Updated README with links for bug reporting and wiki
* 6302177 Updated Battle.net launcher uri string table
* 47a8f5b Updated Battle.net launcher strings for Destiny 2
* 83b4e37 (tag: v1.05c) Fixed non-launcher game execution and timing
* 72d4038 (tag: v1.05b) Push git log output into Changelog.md before builds
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
