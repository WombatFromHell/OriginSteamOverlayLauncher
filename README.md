# OriginSteamOverlayLauncher
How To Install/Uninstall
========================
This wrapper requires no installation other than copying it into the directory of the game executable you wish to run through Steam BPM/Overlay. It also can simply be deleted if you wish to uninstall it at any time.


How To Use
==========
1) Place the "OriginSteamOverlayLauncher.exe" file in the directory with the game executable you wish to launch through Origin.
2) Go into Steam and add the OriginSteamOverlayLauncher.exe as a Non-Steam Game, selecting it from the directory of the game executable you wish to run through Steam.
3) Rename the shortcut in Steam to the name of the game in Origin so you can retrieve Steam Community Configurator Profiles.
4) Launch the shortcut you've created and you'll be prompted to select the paths of the Origin Launcher (by default: "C:\Program Files (x86)\Origin\Origin.exe"), and the game executable you wish to use.
5) This wrapper should run Origin (so Steam Overlay can hook into it) and then the game, and if everything went okay the Steam Overlay will appear in-game.


Notes
=====
If you wish to use additional arguments with your game executable you should edit the "OriginSteamOverlayLauncher.ini" that is created after running the wrapper once.

IMPORTANT: For many Origin titles you'll still have to disable the Origin in-game overlay so that Steam's overlay can function. Only the Steam developers can permanently address the conflict between their overlay and Origin's overlay.


Credits
=======
Thanks to Dafzor and his bnetlauncher wrapper (http://madalien.com/stuff/bnetlauncher/) for giving me the idea to make this.
