# OSOL (O.rigin S.team O.verlay L.auncher) [![Join the chat at https://gitter.im/OriginSteamOverlayLauncher/Lobby](https://badges.gitter.im/OriginSteamOverlayLauncher/Lobby.svg)](https://gitter.im/OriginSteamOverlayLauncher/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

How To Install/Uninstall
========================
This wrapper requires no installation other than copying it into the directory of the game executable you wish to run through Steam BPM/Overlay. It also can simply be deleted if you wish to uninstall it at any time. If you have trouble running it, or are running Windows 8 or earlier, you may need to download and install the [.NET Framework Redistributable v4.5](https://www.microsoft.com/en-us/download/details.aspx?id=40779).


How To Use
==========
* Place the "OriginSteamOverlayLauncher.exe" file in the directory with the game executable you wish to launch through Origin (you may rename it to whatever you wish).
* Run OSOL from this directory once, it will ask for paths to your Game and Launcher executables.
* Go into Steam and add the OriginSteamOverlayLauncher.exe (or whatever you named it) as a Non-Steam Game, selecting it from the directory you chose to place it in.
* Rename the shortcut in Steam to the name of the game in your launcher so you can retrieve Steam Community profiles.
* Launch the shortcut you've created in Steam and OSOL should run the launcher and then the game, and if everything went well Steam Overlay should appear in-game.


Notes
=====
__If you're looking for specific instructions on getting OSOL working with a particular launcher or are having issues with certain behavior you believe is related to OSOL please read the [Project Wiki Page](https://github.com/WombatFromHell/OriginSteamOverlayLauncher/wiki) before making an issue ticket.__

OSOL should work with most launchers that call a regular Windows executable, if you find a launcher that doesn't work with OSOL please [report it](https://github.com/WombatFromHell/OriginSteamOverlayLauncher/issues/new) so I can address it.


Known Issues
============
If you have difficulty getting the Steam overlay to hook into your game when launching with OSOL please follow [these instructions](https://support.steampowered.com/kb_article.php?ref=9828-SFLZ-9289), and make sure OSOL and Steam are both running with the appropriate permissions (if Steam is running as Admin, make sure to run OSOL as Admin as well so that all processes spawned from it can be hooked by Steam). **This is important for older games (circa <2007).**

If you have issues with games not launching with the Steam Overlay and are using a recent AMD graphics device you may need to disable the "AMD External Events Utility" service by following the instructions below:

* Run "services.msc".
* Browse down to the "AMD External Events" service.
* Double-click it, change the startup type to "Disabled", and click "Stop" to disable the service, then click "OK" and exit the dialog.

NOTE: This will break FreeSync functionality, but allow the Steam Overlay to hook into Origin games.


How To Compile
==============
If you wish to compile this project from github source, you'll need Visual Studio v14+ or Visual Studio Express and target the .NET Framework v4.5 for C#. There are no external libraries required. The source code can be modified freely under the MIT license as long as the contributers and creator are given credit.


Credits
=======
Thanks to Dafzor and his bnetlauncher wrapper (http://madalien.com/stuff/bnetlauncher/) for giving me the idea to make this.


Donations [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://paypal.me/JBrown749)
=========
If you find this project useful and you would like to donate toward on-going development you can use the link above. Any and all donations are much appreciated!
