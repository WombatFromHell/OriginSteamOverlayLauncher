# OSOL (O.rigin S.team O.verlay L.auncher)

Why should I use OSOL
=====================
If you've tried using the Steam Controller (or any other Steam Input supported device) from a couch with a third-party non-Steam game (like with games on Battle.net, Origin, or UPlay) then you know how annoying this combination can be. OSOL was created to make this process significantly more user-friendly while also providing additional functionality not typically available using other tools (CPU affinity, process priority, and pre/post launch command execution).

Aside from the most common mainstream launchers mentioned previously OSOL is also compatible with the vast majority of emulators and even some MMORPG/MMOFPS launchers. See the [OSOL project wiki](https://github.com/WombatFromHell/OriginSteamOverlayLauncher/wiki) for more details about this and other application specific notes.

**OSOL currently supports the following launchers:** _Battle.net, UPlay, Origin, GOG Galaxy, Epic Games Launcher, Steam, and most Emulators/Master Launchers that call a game's executable file._


How To Install/Uninstall
========================
This wrapper requires no installation other than copying it into the directory of the game executable you wish to run through Steam BPM/Overlay. It also can simply be deleted if you wish to uninstall it at any time. If you have trouble running it, or are running Windows 8 or earlier, you may need to download and install the [.NET Framework Runtime v4.7.1](https://www.microsoft.com/en-us/download/details.aspx?id=56115).


How To Use
==========
* Unpack the OSOL .exe file from the OSOL archive into the game directory (where the game's executable is located for example).
* Run the OSOL .exe file from this directory and it will prompt you to choose the path to your game executable (required) and the game launcher (which is optional).
* Add the OSOL .exe to Steam as a non-Steam game by clicking the "Add a game" button on the bottom left of the Steam window, clicking "Add a Non-Steam Game" and selecting the OSOL .exe file in the path chooser.
* Name this new non-Steam game shortcut of OSOL (in Steam) whatever you like (such as your game's name).
* Run this non-Steam game shortcut from the Steam library as any other Steam game and the Steam overlay and third-party overlay should show up in-game (if enabled).
* **Optional:** _for advanced functionality or compatibility options for particular launchers see the OSOL project [Wiki](https://github.com/WombatFromHell/OriginSteamOverlayLauncher/wiki)._


Notes
=====
__If you're looking for specific instructions on getting OSOL working with a particular launcher or are having issues with behavior you believe is related to OSOL please read the [project Wiki](https://github.com/WombatFromHell/OriginSteamOverlayLauncher/wiki) before making an issue ticket.__

If you experience crashes when starting OSOL and are running Windows 7, make sure you install the .NET 4.7.1 Redistributable found [here](https://www.microsoft.com/en-us/download/details.aspx?id=56115).

If you have difficulty getting the Steam overlay to hook into your game when launching with OSOL please follow [these instructions](https://support.steampowered.com/kb_article.php?ref=9828-SFLZ-9289), and make sure OSOL and Steam are both running with the appropriate permissions (if Steam is running as Admin, make sure to run OSOL as Admin as well so that all processes spawned from it can be hooked by Steam). **This is important for older games (circa <2007).**

If you have issues with games not launching with the Steam Overlay and are using a recent AMD graphics device you may need to disable the "AMD External Events Utility" service by following the instructions below:

* Run "services.msc".
* Browse down to the "AMD External Events" service.
* Double-click it, change the startup type to "Disabled", and click "Stop" to disable the service, then click "OK" and exit the dialog.

**NOTE:** _This will break FreeSync functionality, but allow the Steam Overlay to hook into Origin games._

If you find a launcher that doesn't work with OSOL please [report it](https://github.com/WombatFromHell/OriginSteamOverlayLauncher/issues/new) so I can consider adding it to the supported launchers list.


How To Compile
==============
If you wish to compile this project from Github source, you'll need Visual Studio v14+ or Visual Studio Community (targetting the .NET Framework v4.7.1 for C#). There are no external libraries required except for the .NET v4.7.1 framework package. The source code can be modified freely under the MIT license as long as the contributers and creator are given credit.

If you'd like to contribute please make sure to comment your code thoroughly and try to split major features up into their own PRs when possible.


Credits
=======
Special thanks to CriticalComposer for his art/icon contribution to the OSOL project.

Thanks to Dafzor and his bnetlauncher wrapper (http://madalien.com/stuff/bnetlauncher/) for giving me the idea to make this.


Donations <a href="https://www.buymeacoffee.com/wombatfromhell" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="21" width="87"></a>
=========
If you find this project useful and you would like to donate toward on-going development you can use the link above. Any and all donations are much appreciated!
