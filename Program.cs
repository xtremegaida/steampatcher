using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SteamPatcher
{
   class Program
   {
      static readonly byte[] Data_steam_api = ExtractResource("SteamPatcher.steam_api.dll.gz");
      static readonly byte[] Data_steam_api64 = ExtractResource("SteamPatcher.steam_api64.dll.gz");

      static int Main(string[] args)
      {
         var path = Directory.GetCurrentDirectory();
         var steamAppsOnly = true;
         var showHelp = true;
         var mode = ' ';

         if (args != null)
         {
            var options = "";
            foreach (var item in args)
            {
               if (item.Length == 0) { continue; }
               if (item[0] == '-') { options += item.Substring(1); }
               else { path = item; }
               showHelp = false;
            }
            foreach (char c in options)
            {
               if (c == 'a') { steamAppsOnly = false; }
               if (c == 'l' || c == 'p' || c == 'u' || c == 'i')
               {
                  if (mode == ' ') { mode = c; }
                  else
                  {
                     Console.WriteLine("ERROR: Mode -{0} already specified.", mode);
                     Console.WriteLine();
                     showHelp = true;
                  }
               }
            }
         }

         if (showHelp)
         {
            Console.WriteLine("steampatcher [search_dir] [-a] <-l | -p | -u | -i>");
            Console.WriteLine("   search_dir: Root directory to search for Steam games (default: current directory)");
            Console.WriteLine("           -a: Search in any directory (default: only \"steamapps\" directories)");
            Console.WriteLine("           -l: List Steam games found");
            Console.WriteLine("           -p: Patch Steam games found");
            Console.WriteLine("           -u: Unpatch Steam games found");
            Console.WriteLine("           -i: Interactive mode (ask action for each Steam game found; default mode)");
            return -1;
         }

         if (!Directory.Exists(path))
         {
            Console.WriteLine("ERROR: Cannot find path \"{0}\"", path);
            return 1;
         }

         if (mode == ' ') { mode = 'i'; }
         if (mode == 'l')
         {
            Console.WriteLine("Listing Steam games ({0})...", steamAppsOnly ? "steamapps directories only" : "any directory");
            var games = FindSteamGames(path, !steamAppsOnly);
            Console.WriteLine("Found {0} games.", games.Length);
            if (games.Length > 0)
            {
               Console.WriteLine("---");
               foreach (var game in games)
               {
                  Console.WriteLine("({0}) {1} at {2} [PATCHED:{3}]",
                     game.AppId ?? "???",
                     game.InstallPath.Name,
                     game.Path.FullName,
                     game.IsPatched ? "YES" : "NO");
               }
               Console.WriteLine("---");
            }
         }
         else if (mode == 'p')
         {
            Console.WriteLine("Mode: Patch All");
            if (!ProcessPath(path, steamAppsOnly, "p")) { return 2; }
         }
         else if (mode == 'u')
         {
            Console.WriteLine("Mode: Unpatch All");
            if (!ProcessPath(path, steamAppsOnly, "u")) { return 2; }
         }
         else if (mode == 'i')
         {
            Console.WriteLine("Mode: Interactive");
            if (!ProcessPath(path, steamAppsOnly)) { return 2; }
         }

         return 0;
      }

      static bool ProcessPath(string path, bool steamAppsOnly, string forceAction = null)
      {
         int errors = 0, patched = 0, unpatched = 0;
         Console.WriteLine("Searching Steam games ({0})...", steamAppsOnly ? "steamapps directories only" : "any directory");
         var games = FindSteamGames(path, !steamAppsOnly);
         Console.WriteLine("Found {0} games.", games.Length);
         if (games.Length > 0)
         {
            Console.WriteLine("---");
            foreach (var game in games)
            {
               Console.WriteLine("({0}) {1} at {2} [PATCHED:{3}]",
                  game.AppId ?? "unk",
                  game.InstallPath.Name,
                  game.Path.FullName,
                  game.IsPatched ? "YES" : "NO");
               var action = forceAction;
               if (action == null)
               {
                  Console.Write("Specify action (p = Patch, u = Unpatch, anything else = Skip): ");
                  action = Console.ReadLine().Trim().ToLower();
               }
               if (action == "p")
               {
                  if (!game.IsPatched)
                  {
                     if (string.IsNullOrWhiteSpace(game.AppId))
                     {
                        Console.WriteLine("ERROR: Cannot find appid - please create steam_appid.txt with the proper id.");
                        errors++;
                     }
                     else
                     {
                        Console.WriteLine("Patching...");
                        try
                        {
                           PatchSteamGame(game.Path.FullName, game.AppId);
                           Console.WriteLine("Patched successfully.");
                           patched++;
                        }
                        catch (Exception error)
                        {
                           Console.WriteLine("ERROR: {0}", error.Message);
                           errors++;
                        }
                     }
                  }
                  else
                  {
                     Console.WriteLine("Already patched.");
                  }
               }
               else if (action == "u")
               {
                  if (game.IsPatched)
                  {
                     Console.WriteLine("Unpatching...");
                     try
                     {
                        UnpatchSteamGame(game.Path.FullName);
                        Console.WriteLine("Unpatched successfully.");
                        unpatched++;
                     }
                     catch (Exception error)
                     {
                        Console.WriteLine("ERROR: {0}", error.Message);
                        errors++;
                     }
                  }
                  else
                  {
                     Console.WriteLine("Not patched.");
                  }
               }
               else
               {
                  Console.WriteLine("Skipping.");
               }
            }
            Console.WriteLine("---");
         }
         Console.WriteLine("Total patched: {0}", patched);
         Console.WriteLine("Total unpatched: {0}", unpatched);
         Console.WriteLine("Total errors: {0}", errors);
         if (errors > 0) { Console.WriteLine("ERROR: One or more errors encountered."); }
         return errors == 0;
      }

      class SteamGameEntry
      {
         public DirectoryInfo Path { get; set; }
         public DirectoryInfo InstallPath { get; set; }
         public string AppId { get; set; }
         public bool IsPatched { get; set; }
      }

      static SteamGameEntry[] FindSteamGames(string path, bool include)
      {
         var result = new List<SteamGameEntry>();
         var dirInfo = new DirectoryInfo(path);
         if (!include)
         {
            try
            {
               var find = dirInfo;
               do
               {
                  if (find.Name == "steamapps") { include = true; break; }
                  find = find.Parent;
               }
               while (find != null);
            }
            catch { }
         }
         FindSteamGames(dirInfo, result, include);
         return result.ToArray();
      }

      static void FindSteamGames(DirectoryInfo path, List<SteamGameEntry> result, bool include)
      {
         try
         {
            if (path.Name == "steamapps") { include = true; }
            if (include)
            {
               var fullPath = path.FullName;
               if (IsSteamGame(fullPath))
               {
                  result.Add(new SteamGameEntry()
                  {
                     Path = path,
                     InstallPath = GetInstallPath(path),
                     AppId = GetSteamAppId(path),
                     IsPatched = IsPatched(path.FullName)
                  });
                  return;
               }
            }
            foreach (var sub in path.GetDirectories())
            {
               FindSteamGames(sub, result, include);
            }
         }
         catch { }
      }

      static DirectoryInfo GetInstallPath(DirectoryInfo path)
      {
         try
         {
            var find = path;
            do
            {
               var up = find.Parent;
               if (up == null) { return path; }
               if (up.Name == "common") { return find; }
               find = up;
            }
            while (true);
         }
         catch { return path; }
      }

      static string GetSteamAppId(DirectoryInfo path)
      {
         string result = null;
         try
         {
            var appIdPath = Path.Combine(path.FullName, "steam_appid.txt");
            if (File.Exists(appIdPath)) { result = File.ReadAllText(appIdPath); }
            if (string.IsNullOrWhiteSpace(result)) { result = null; }
            if (result == null)
            {
               appIdPath = Path.Combine(path.FullName, "steam_settings", "steam_appid.txt");
               if (File.Exists(appIdPath)) { result = File.ReadAllText(appIdPath); }
               if (string.IsNullOrWhiteSpace(result)) { result = null; }
            }
            if (result == null)
            {
               var installPath = GetInstallPath(path);
               var findStr = '"' + installPath.Name + '"';
               foreach (var file in installPath.Parent.Parent.GetFiles())
               {
                  var fileName = file.Name.ToLower();
                  if (!fileName.StartsWith("appmanifest_")) { continue; }
                  if (!fileName.EndsWith(".acf")) { continue; }
                  var fileStr = File.ReadAllText(file.FullName);
                  if (fileStr.Contains(findStr)) { return fileName.Substring(12, fileName.Length - 12 - 4); }
               }
            }
         }
         catch { }
         return result;
      }

      static void PatchSteamGame(string path, string appId)
      {
         if (!IsSteamGame(path)) { return; }

         var appIdPath = Path.Combine(path, "steam_appid.txt");
         if (!File.Exists(appIdPath)) { File.WriteAllText(appIdPath, appId); }

         var settingsPath = Path.Combine(path, "steam_settings");
         if (!Directory.Exists(settingsPath))
         {
            Directory.CreateDirectory(settingsPath);
            File.WriteAllText(Path.Combine(settingsPath, "steam_appid.txt"), appId);
            File.WriteAllText(Path.Combine(settingsPath, "offline.txt"), "1");
            File.WriteAllText(Path.Combine(settingsPath, "disable_overlay.txt"), "1");
            File.WriteAllText(Path.Combine(settingsPath, "disable_networking.txt"), "1");
         }

         var patchData = Data_steam_api;
         var dllPath = Path.Combine(path, "steam_api.dll");
         if (!File.Exists(dllPath))
         {
            patchData = Data_steam_api64;
            dllPath = Path.Combine(path, "steam_api64.dll");
            if (!File.Exists(dllPath)) { return; }
         }
         var existingData = File.ReadAllBytes(dllPath);
         if (!ArraysEqual(existingData, patchData))
         {
            var interfacesPath = Path.Combine(path, "steam_interfaces.txt");
            if (!File.Exists(interfacesPath))
            {
               var dllStr = Encoding.ASCII.GetString(existingData);
               var result = new StringBuilder();
               foreach (var i in interfaces) { AddInterfaceMatch(result, dllStr, i + "\\d{3}"); }
               if (!AddInterfaceMatch(result, dllStr, "STEAMCONTROLLER_INTERFACE_VERSION\\d{3}"))
               {
                  AddInterfaceMatch(result, dllStr, "STEAMCONTROLLER_INTERFACE_VERSION");
               }
               File.WriteAllText(interfacesPath, result.ToString());
            }

            File.Copy(dllPath, dllPath + ".old", true);
            File.WriteAllBytes(dllPath, patchData);
         }
      }
      
      static void UnpatchSteamGame(string path)
      {
         var dllPath = Path.Combine(path, "steam_api.dll");
         if (!File.Exists(dllPath + ".old"))
         {
            dllPath = Path.Combine(path, "steam_api64.dll");
            if (!File.Exists(dllPath + ".old")) { return; }
         }
         File.Move(dllPath + ".old", dllPath, true);
      }

      static bool IsSteamGame(string path)
      {
         return File.Exists(Path.Combine(path, "steam_api.dll")) ||
                File.Exists(Path.Combine(path, "steam_api64.dll"));
      }

      static bool IsPatched(string path)
      {
         try
         {
            var data = Data_steam_api;
            var dll = Path.Combine(path, "steam_api.dll");
            if (!File.Exists(dll))
            {
               data = Data_steam_api64;
               dll = Path.Combine(path, "steam_api64.dll");
               if (!File.Exists(dll)) { return false; }
            }
            return ArraysEqual(File.ReadAllBytes(dll), data);
         }
         catch
         {
            return false;
         }
      }

      static bool ArraysEqual(byte[] a, byte[] b)
      {
         if (a.Length != b.Length) { return false; }
         for (int i = 0, j = a.Length; i < j; i++) { if (a[i] != b[i]) { return false; } }
         return true;
      }

      static readonly string[] interfaces =
      {
         "SteamClient",
         "SteamGameServer",
         "SteamGameServerStats",
         "SteamUser",
         "SteamFriends",
         "SteamUtils",
         "SteamMatchMaking",
         "SteamMatchMakingServers",
         "STEAMUSERSTATS_INTERFACE_VERSION",
         "STEAMAPPS_INTERFACE_VERSION",
         "SteamNetworking",
         "STEAMREMOTESTORAGE_INTERFACE_VERSION",
         "STEAMSCREENSHOTS_INTERFACE_VERSION",
         "STEAMHTTP_INTERFACE_VERSION",
         "STEAMUNIFIEDMESSAGES_INTERFACE_VERSION",
         "STEAMUGC_INTERFACE_VERSION",
         "STEAMAPPLIST_INTERFACE_VERSION",
         "STEAMMUSIC_INTERFACE_VERSION",
         "STEAMMUSICREMOTE_INTERFACE_VERSION",
         "STEAMHTMLSURFACE_INTERFACE_VERSION_",
         "STEAMINVENTORY_INTERFACE_V",
         "SteamController",
         "SteamMasterServerUpdater",
         "STEAMVIDEO_INTERFACE_V"
      };

      static bool AddInterfaceMatch(StringBuilder result, string str, string regex)
      {
         var hasMatch = false;
         foreach (Match match in new Regex(regex).Matches(str))
         {
            result.AppendLine(match.Value);
            hasMatch = true;
         }
         return hasMatch;
      }

      static byte[] ExtractResource(string name)
      {
         using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
         using (var decompress = new GZipStream(stream, CompressionMode.Decompress))
         using (var output = new MemoryStream()) { decompress.CopyTo(output); return output.ToArray(); }
      }
   }
}
