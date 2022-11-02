using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework.Graphics;

using StardewModdingAPI;
using StardewModdingAPI.Events;

using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using Newtonsoft.Json;

namespace AdoptSkinMpBeta.Framework
{
    class SaveLoadHandler
    {
        /// <summary>The file extensions recognised by the mod.</summary>
        private static readonly HashSet<string> ValidExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            ".png",
            ".xnb"
        };

        // Accessors from ModEntry, so things look cleaner
        private static readonly IModHelper SHelper = ModEntry.SHelper;
        private static readonly IMonitor SMonitor = ModEntry.SMonitor;

        // Specific ModEntry instance
        private static ModEntry Entry;

        // Lists to hold potential assets errors at load time so that they can be reported to the user
        private static List<string> InvalidExt = new List<string>();
        private static List<string> InvalidType = new List<string>();
        private static List<string> InvalidID = new List<string>();
        private static List<string> InvalidNum = new List<string>();
        private static List<string> InvalidRange = new List<string>();



        internal SaveLoadHandler(ModEntry entry)
        {
            Entry = entry;
        }



        /**************************
        ** Setup + Load/Save Logic
        ***************************/

        /// <summary>Sets up initial values needed for A&S.</summary>
        internal static void Setup(object sender, SaveLoadedEventArgs e)
        {
            // Register FarmAnimal Types
            Dictionary<string, string> farmAnimalData = ModEntry.SHelper.Content.Load<Dictionary<string, string>>("Data/FarmAnimals", ContentSource.GameContent);
            foreach (KeyValuePair<string, string> pair in farmAnimalData)
            {
                // Ignore unused FarmAnimal type in SDV code
                if (pair.Key.ToLower() == "hog" || pair.Key.ToLower() == "babyhog")
                    continue;

                string[] animalInfo = pair.Value.Split(new[] { '/' });
                string harvestTool = animalInfo[22];
                int maturedDay = int.Parse(animalInfo[1]);

                ModApi.RegisterType(pair.Key, typeof(FarmAnimal), maturedDay > 0, ModEntry.Sanitize(harvestTool) == "shears");
            }

            // Register default supported pet types
            ModApi.RegisterType("cat", typeof(Cat));
            ModApi.RegisterType("dog", typeof(Dog));

            // Register horse type
            ModApi.RegisterType("horse", typeof(Horse));

            LoadAssets();

            // Alert player that there are creatures with no skins loaded for them
            List<string> skinless = new List<string>();
            foreach (string type in ModEntry.Assets.Keys)
                if (ModEntry.Assets[type].Count == 0)
                    skinless.Add(type);
            if (skinless.Count > 0)
                ModEntry.SMonitor.Log($"NOTICE: The following creature types have no skins located in `/assets/skins`:\n" +
                    $"{string.Join(", ", skinless)}", LogLevel.Debug);

            // Set the current known FarmAnimal count
            Game1.getFarm().modData[ModEntry.FarmAnimalCount] = ModApi.GetFarmAnimals().Count().ToString();
            Game1.getFarm().modData[ModEntry.EditLock] = "";
            Game1.getFarm().modData[ModEntry.HorsesMounted] = JsonConvert.SerializeObject(new List<Horse>());

            // Remove the Setup from the loop, so that it isn't done twice when the player returns to the title screen and loads again
            SHelper.Events.GameLoop.SaveLoaded -= Setup;
        }


        /// <summary>Starts processes that Adopt & Skin checks at every update tick</summary>
        internal void StartUpdateChecks()
        {
            SHelper.Events.GameLoop.UpdateTicked += Entry.OnUpdateTicked;

            if (ModEntry.Config.PetAndHorseNameTags)
                SHelper.Events.Display.RenderingHud += ModEntry.ToolTip.RenderHoverTooltip;
                SHelper.Events.Display.RenderingHud += ModEntry.ToolTip.RenderHoverTooltip;
        }


        /// <summary>Stops Adopt & Skin from updating at each tick</summary>
        internal void StopUpdateChecks(object s, EventArgs e)
        {
            SHelper.Events.GameLoop.UpdateTicked -= Entry.OnUpdateTicked;

            if (ModEntry.Config.PetAndHorseNameTags)
                SHelper.Events.Display.RenderingHud -= ModEntry.ToolTip.RenderHoverTooltip;
        }



        internal void LoadData(object s, EventArgs e)
        {
            // Only allow the host player to load Adopt & Skin data
            if (Context.IsMainPlayer)
            {
                //**TODO: Does anything need to be stored?
            }

            StartUpdateChecks();
        }


        /*
        internal static void SaveData(object s, EventArgs e)
        {
            // Only allow the host player to save Adopt & Skin data
            if (Context.IsMainPlayer)
            {
                //**TODO: Does anything need to be stored?
            }
        }
        */


        internal static void LoadAssets()
        {
            // Gather handled types
            string validTypes = string.Join(", ", ModApi.GetHandledAllTypes());

            // Add custom sprites
            // TODO: Skin content packages. Grab the directory for the /Mods folder from /Mods/AdoptSkin
            foreach (string path in Directory.EnumerateFiles(Path.Combine(SHelper.DirectoryPath, "assets", "skins"), "*", SearchOption.AllDirectories))
                PullSprite(Path.GetRelativePath(SHelper.DirectoryPath, path)); // must be a relative path

            foreach (string path in Directory.EnumerateFiles(Path.Combine(SHelper.DirectoryPath)))

            // Warn for invalid files
            if (InvalidExt.Count > 0)
                ModEntry.SMonitor.Log($"Ignored skins with invalid extension:\n`{string.Join("`, `", InvalidExt)}`\nExtension must be one of type {string.Join(", ", ValidExtensions)}", LogLevel.Warn);
            if (InvalidType.Count > 0)
                ModEntry.SMonitor.Log($"Ignored skins with invalid naming convention:\n`{string.Join("`, `", InvalidType)}`\nCan't parse as an animal, pet, or horse. Expected one of type: {validTypes}", LogLevel.Warn);
            if (InvalidID.Count > 0)
                ModEntry.SMonitor.Log($"Ignored skins with invalid naming convention (no skin ID found):\n`{string.Join("`, `", InvalidID)}`", LogLevel.Warn);
            if (InvalidNum.Count > 0)
                ModEntry.SMonitor.Log($"Ignored skins with invalid ID (can't parse ID number):\n`{string.Join("`, `", InvalidNum)}`", LogLevel.Warn);
            if (InvalidRange.Count > 0)
                ModEntry.SMonitor.Log($"Ignored skins with ID of less than or equal to 0 (Skins must have an ID of at least 1):\n`{string.Join("`, `", InvalidRange)}`", LogLevel.Warn);

            EnforceSpriteSets();

            // Print loaded assets to console
            StringBuilder summary = new StringBuilder();
            summary.AppendLine(
                "Statistics:\n"
                + "\n  Registered types: " + validTypes
                + "\n  Skins:"
            );
            foreach (KeyValuePair<string, Dictionary<int, CreatureSkin>> skinEntry in ModEntry.Assets)
            {
                if (skinEntry.Value.Count > 0)
                    summary.AppendLine($"    {skinEntry.Key}: {skinEntry.Value.Count} skins ({string.Join(", ", skinEntry.Value.Select(p => Path.GetFileName(p.Value.AssetKey)).OrderBy(p => p))})");
            }

            ModEntry.SMonitor.Log(summary.ToString(), LogLevel.Trace);
            ModEntry.AssetsLoaded = true;


        }



        /// <summary>Places the custom sprite at the given path into the A&S system for use.</summary>
        /// <param name="path">The full directory location of the sprite</param>
        private static void PullSprite(string path)
        {
            string extension = Path.GetExtension(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string[] nameParts = fileName.Split(new[] { '_' }, 2);
            string type = ModEntry.Sanitize(nameParts[0]);
            int skinID = 0;

            if (!ValidExtensions.Contains(extension))
                InvalidExt.Add(fileName);
            else if (!ModEntry.Assets.ContainsKey(type))
                InvalidType.Add(fileName);
            else if (nameParts.Length != 2)
                InvalidID.Add(fileName);
            else if (nameParts.Length == 2 && !int.TryParse(nameParts[1], out skinID))
                InvalidNum.Add(fileName);
            else if (skinID <= 0)
                InvalidRange.Add(fileName);
            else
            {
                // File naming is valid, get the asset key
                string assetKey = SHelper.Content.GetActualAssetKey(Path.Combine(Path.GetDirectoryName(path), extension.Equals("xnb") ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path)));

                // User has duplicate skin names. Only keep the first skin found with the identifier and number ID
                if (ModEntry.Assets[type].ContainsKey(skinID))
                    ModEntry.SMonitor.Log($"Ignored skin `{fileName}` with duplicate type and ID (more than one skin named `{fileName}` exists in `/assets/skins`)", LogLevel.Debug);
                // Skin is valid, add into system
                else
                {
                    Texture2D texture = ModEntry.SHelper.Content.Load<Texture2D>(assetKey, ContentSource.ModFolder);
                    ModEntry.Assets[type].Add(skinID, new CreatureSkin(type, skinID, assetKey, texture));
                }
            }
        }



        /// <summary>
        /// Checks the list of loaded assets and removes incomplete skin sets
        /// (i.e. a "sheared" or "baby" skin exists, but not the typical skin, or vice versa where applicable)
        /// </summary>
        private static void EnforceSpriteSets()
        {
            Dictionary<string, List<int>> skinsToRemove = new Dictionary<string, List<int>>();
            // ** Make list of values added, when check is done, see if Key already exists- simply add to values if so

            //Dictionary<string, Dictionary<int, AnimalSkin>> assetCopy = new Dictionary<string, Dictionary<int, AnimalSkin>>(ModEntry.Assets);
            foreach (KeyValuePair<string, Dictionary<int, CreatureSkin>> pair in ModEntry.Assets)
            {
                if (pair.Key.StartsWith("sheared"))
                {
                    // Look at the creature type that comes after "sheared"
                    if (ModEntry.Assets.ContainsKey(pair.Key.Substring(7)))
                    {
                        // Make sure every sheared skin has a normal skin variant for its ID
                        foreach (int id in ModEntry.Assets[pair.Key].Keys)
                            if (!ModEntry.Assets[pair.Key.Substring(7)].ContainsKey(id))
                                skinsToRemove = AddToSortedDict(skinsToRemove, pair.Key, id);

                        // Since the normal skin has a sheared version, make sure all normal versions have sheared skins
                        foreach (int id in ModEntry.Assets[pair.Key.Substring(7)].Keys)
                            if (!ModEntry.Assets[pair.Key].ContainsKey(id))
                                skinsToRemove = AddToSortedDict(skinsToRemove, pair.Key.Substring(7), id);
                    }
                    // This sheared skin has no normal animal type registered to it; remove all sheared variants of this skin
                    else
                        foreach (int id in ModEntry.Assets[pair.Key].Keys)
                            skinsToRemove = AddToSortedDict(skinsToRemove, pair.Key, id);
                }
                else if (pair.Key.StartsWith("baby"))
                {
                    // Look at the creature type that comes after "baby"
                    if (ModEntry.Assets.ContainsKey(pair.Key.Substring(4)))
                    {
                        // Make sure every baby skin has a normal skin variant for its ID
                        foreach (int id in ModEntry.Assets[pair.Key].Keys)
                            if (!ModEntry.Assets[pair.Key.Substring(4)].ContainsKey(id))
                                skinsToRemove = AddToSortedDict(skinsToRemove, pair.Key, id);

                        // Since the normal skin has a baby version, make sure all normal versions have baby skins
                        foreach (int id in ModEntry.Assets[pair.Key.Substring(4)].Keys)
                            if (!ModEntry.Assets[pair.Key].ContainsKey(id))
                                skinsToRemove = AddToSortedDict(skinsToRemove, pair.Key.Substring(4), id);
                    }
                    // This baby skin has no normal skins at all; remove them all
                    else
                        foreach (int id in ModEntry.Assets[pair.Key].Keys)
                            skinsToRemove = AddToSortedDict(skinsToRemove, pair.Key, id);
                }
            }


            // Warn player of any incomplete sets and remove them from the Assets dictionary
            if (skinsToRemove.Count > 0)
            {
                string warnString = "";

                foreach (KeyValuePair<string, List<int>> removing in skinsToRemove)
                {
                    warnString += removing.Key.ToString() + ": IDs " + string.Join(", ", removing.Value) + "\n";
                    foreach (int id in removing.Value)
                        ModEntry.Assets[removing.Key].Remove(id);
                }

                ModEntry.SMonitor.Log($"The following skins are incomplete skin sets, and will not be loaded (missing a paired sheared, baby, or adult skin):\n{warnString}", LogLevel.Warn);
            }


            // ** TODO: Is there a way to check for types, so adults with no baby *or* sheared can be caught? Just make grab adult skin?
            // -- Cycle through FarmAnimal typing list and check while in there
        }

        // <summary>
        // Helper function for loading assets. This adds skins to their appropriate location within the A&S skin assets dictionary.
        // If a skin is given with an ID number that has already been added to the system then only the first skin to be added will be kept.
        // </summary>
        private static Dictionary<string, List<int>> AddToSortedDict(Dictionary<string, List<int>> dict, string type, int id)
        {
            if (!dict.ContainsKey(type))
                dict.Add(type, new List<int> { id });
            else if (!dict[type].Contains(id))
                dict[type].Add(id);

            return dict;
        }
    }
}