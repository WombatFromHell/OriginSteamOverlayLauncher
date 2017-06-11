# OriginSteamOverlayLauncher
How To Install/Uninstall
========================
This wrapper requires no installation other than copying it into the directory of the game executable you wish to run through Steam BPM/Overlay. It also can simply be deleted if you wish to uninstall it at any time. If you have trouble running it, or are running Windows 8 or earlier, you may need to download and install the [.NET Framework Redistributable v4.5](https://www.microsoft.com/en-us/download/details.aspx?id=40779).


How To Use
==========
* Place the "OriginSteamOverlayLauncher.exe" file in the directory with the game executable you wish to launch through Origin.
* Go into Steam and add the OriginSteamOverlayLauncher.exe as a Non-Steam Game, selecting it from the directory of the game executable you wish to run through Steam.
* Rename the shortcut in Steam to the name of the game in Origin so you can retrieve Steam Community Configurator Profiles.
* Launch the shortcut you've created and you'll be prompted to select the paths of the Origin Launcher (by default: "C:\Program Files (x86)\Origin\Origin.exe"), and the game executable you wish to use.
* This wrapper should run Origin (so Steam Overlay can hook into it) and then the game, and if everything went okay the Steam Overlay will appear in-game.


Notes
=====
**IMPORTANT: For many Origin titles you'll still have to disable the Origin in-game overlay so that Steam's overlay can function. Having both enabled at the same time WILL break functionality of either Origin, Steam, or in the worst cases both. Only the Steam developers can permanently address the conflict between their overlay and Origin's overlay.**

If you wish to use additional arguments with your game executable you should edit the "OriginSteamOverlayLauncher.ini" that is created after running the wrapper once.


Known Issues
============
There may be a few games that launch through Origin that require an Origin URL instead of an executable. Currently, this wrapper only supports titles that have a game executable that can open Origin.

If you have issues with games not launching with the Steam Overlay, and are using a recent AMD graphics device you may need to disable the "AMD External Events" service. NOTE: This will break FreeSync functionality, but allow the Steam Overlay to hook into Origin games:

* Run "services.msc".
* Browse down to the "AMD External Events" service.
* Double-click it, change the startup type to "Disabled", and click "Stop" to disable the service.


How To Compile
==============
If you wish to compile this project from github source, you'll need Visual Studio v14+ or Visual Studio Express and target the .NET Framework v4.5 for C#. There are no external libraries required. The source code can be modified freely under the MIT license as long as the contributers and creator are given credit.


Credits
=======
Thanks to Dafzor and his bnetlauncher wrapper (http://madalien.com/stuff/bnetlauncher/) for giving me the idea to make this.
