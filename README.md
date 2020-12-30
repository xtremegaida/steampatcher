# Steam Patcher
Patches Steam games to remove the need for the Steam client to be running to play the game.

Download here: https://github.com/xtremegaida/steampatcher/raw/master/dist/SteamPatcher.exe

```
steampatcher [search_dir] [-a] <-l | -p | -u | -i>
   search_dir: Root directory to search for Steam games (default: current directory)
           -a: Search in any directory (default: only "steamapps" directories)
           -l: List Steam games found
           -p: Patch Steam games found
           -u: Unpatch Steam games found
           -i: Interactive mode (ask action for each Steam game found; default mode)
```

Currently built for Windows only, and supports both 32-bit and 64-bit games.
The patch is based on Goldberg Lan Steam Emu: https://gitlab.com/Mr_Goldberg/goldberg_emulator
