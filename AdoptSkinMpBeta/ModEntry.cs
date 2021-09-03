using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

using StardewModdingAPI;
using StardewModdingAPI.Events;

using AdoptSkinMpBeta.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using Newtonsoft.Json;

namespace AdoptSkinMpBeta
{
    public class ModEntry : Mod, IAssetEditor, IAssetLoader
    {
        /************************
        ** Fields
        *************************/

        private static readonly Random Randomizer = new Random();
        public enum CreatureCategory { Horse, Pet, Animal, Null };
        internal static ModConfig Config;

        internal static readonly int PetSpriteStartFrame = 28;
        internal static readonly int HorseSpriteStartFrame = 7;
        internal static readonly int AnimalSpriteStartFrame = 0;

        /* String values for use in storing modData */
        /// <summary>This A&S folder directory, which is set upon ModEntry instantiation.</summary>
        internal static string ASDirectory;
        /// <summary>The skin ID number given to a creature. If not given a custom skin, this value will be set to 0 when the creature is added to the A&S database.</summary>
        internal static string SkinID;
        /// <summary>The unique, user-friendly number given to a creature in order for a player to reference that specific creature in the SMAPI console.</summary>
        internal static string ShortID;
        /// <summary>Whether the given creature is unowned. Wild Horses and Stray pets are unowned, all other creatures are owned.</summary>
        internal static string IsUnowned;
        /// <summary>If set to a non-empty string, this value will prevent multiple farmers from editing a creature's properties at once.</summary>
        internal static string EditLock;
        /// <summary>Last known number of FarmAnimals on the farm. Used for checking if a new FarmAnimal has been purchased by a player.</summary>
        internal static string FarmAnimalCount;


        internal static string HorsesMounted;

        // Ridden horse holder
        internal static Horse HorseMounted;






        /************************
       ** Accessors
       *************************/

        // SMAPI Modding helpers
        internal static IModHelper SHelper;
        internal static IMonitor SMonitor;

        // Internal helpers
        internal static CommandHandler Commander;
        internal static SaveLoadHandler SaveLoader;
        internal static HoverBox ToolTip = new HoverBox();

        // Skin assets
        /// <summary>This dictionary stores all loaded skin assets as CreatureSkin instances, organized first by A&S-internal creature type then by skin ID number.</summary>
        internal static Dictionary<string, Dictionary<int, CreatureSkin>> Assets = new Dictionary<string, Dictionary<int, CreatureSkin>>();

        // Whether A&S has finished loading custom sprites into its system
        internal static bool AssetsLoaded = false;





        /************************
        ** Public methods
        *************************/

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // SMAPI helpers
            ModEntry.SHelper = helper;
            ModEntry.SMonitor = this.Monitor;

            // Config settings
            Config = this.Helper.ReadConfig<ModConfig>();

            // Internal helpers
            Commander = new CommandHandler(this);
            SaveLoader = new SaveLoadHandler(this);

            // I cannot remember what this does but it lives here now so we might as well welcome it home
            var loaders = Helper.Content.AssetLoaders;
            loaders.Add(this);

            // Give me a shorthand for this mod sync stuff
            ASDirectory = $"{this.ModManifest.UniqueID}/";

            ShortID = ASDirectory + "short-id";
            SkinID = ASDirectory + "skin-id";
            IsUnowned = ASDirectory + "is-owned";
            EditLock = ASDirectory + "edit-lock";

            FarmAnimalCount = ASDirectory + "farm-animal-count";
            HorsesMounted = ASDirectory + "horses-mounted";

            // Event Listeners
            Helper.Events.GameLoop.SaveLoaded += SaveLoadHandler.Setup;
            Helper.Events.GameLoop.SaveLoaded += SaveLoader.LoadData;
            //Helper.Events.GameLoop.Saving += SaveLoadHandler.SaveData;
            Helper.Events.GameLoop.ReturnedToTitle += SaveLoader.StopUpdateChecks;
            Helper.Events.World.NpcListChanged += HandleNewPetsAndHorses;
            Helper.Events.GameLoop.DayStarted += OnDayStart;

            // SMAPI Commands
            Helper.ConsoleCommands.Add("list_creatures", $"Lists the creature IDs and skin IDs of the given type.\n(Options: '{string.Join("', '", CommandHandler.CreatureGroups)}', or a specific animal type (such as bluechicken))", Commander.OnCommandReceived);
            Helper.ConsoleCommands.Add("randomize_skin", $"Randomizes the skin for the given group of creatures or the creature with the given ID. Call `randomize_skin <creature group or creature ID>`.\nCallable creature groups: {string.Join(",", CommandHandler.CreatureGroups)}, or an adult creature type\nTo find a creature's ID, call `list_creatures`.", Commander.OnCommandReceived);
            Helper.ConsoleCommands.Add("set_skin", "Sets the skin of the given creature to the given skin ID. Call `set_skin <skin ID> <creature ID>`. To find a creature's ID, call `list_creatures`.", Commander.OnCommandReceived);
            Helper.ConsoleCommands.Add("rename", "Renames the creature of the given ID to the given name. Call `rename <creature ID> \"new name\"`.", Commander.OnCommandReceived);

            // Debug commands
            Helper.ConsoleCommands.Add("asdebug_force_ids", "[DEBUG] Assigns all creatures new Short IDs, overriding backend locks.", Commander.OnCommandReceived);
            Helper.ConsoleCommands.Add("asdebug_force_add_all", "[DEBUG] Wipes the A&S properties from all Pets, Horses, and FarmAnimals and then adds them back into A&S all fresh and new. This will reset skins and shirt IDs.", Commander.OnCommandReceived);
            Helper.ConsoleCommands.Add("asdebug_force_remove_all", "[DEBUG] Wipes the A&S properties from all Pets, Horses, and FarmAnimals.", Commander.OnCommandReceived);
            Helper.ConsoleCommands.Add("asdebug_ranch_refresh", "[DEBUG] Refreshes the skins at Marnie's ranch to new ones.", Commander.OnCommandReceived);
        }


        /// <summary>Lets SMAPI allow A&S to read the folder that contains the A&S skin assets</summary>
        public bool CanLoad<T>(IAssetInfo asset)
        {
            if ((asset.AssetName.ToLower()).Contains("assets/skins"))
                return true;
            return false;
        }


        /// <summary>Function for SMAPI to load skin assets for A&S
        public T Load<T>(IAssetInfo asset)
        {
            if ((asset.AssetName.ToLower()).Contains("assets/skins"))
            {
                return SHelper.Content.Load<T>(asset.AssetName, ContentSource.ModFolder);
            }
            throw new InvalidOperationException($"Unexpected asset '{asset.AssetName}'.");
        }


        /// <summary>Lets SMAPI allow A&S to send letters to the user's mailbox.</summary>
        public bool CanEdit<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Data/mail"))
                return true;
            return false;
        }


        /// <summary>Function for SMAPI to edit the farm mailbox so that a letter can be sent to the user.</summary>
        public void Edit<T>(IAssetData asset)
        {
            // Add the letter Marnie sends regarding the stray animals
            if (asset.AssetNameEquals("Data/mail"))
            {
                IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
                data.Add("MarnieStrays", "Dear @,   ^   Since I came over with the stray that I found, I've been running across a lot more of them! Poor things, I think they're escapees from nearby Weatherwoods, that town that burned down a few weeks back.   ^   Anyway, I'm adopting out the ones that I happen across! Just stop by during my normal hours if you'd like to bring home a friend for your newest companion.   ^   -Marnie");
            }
        }


        /// <summary>Standardize internal types and file names to have no spaces and to be entirely lowercase.</summary>
        public static string Sanitize(string input)
        {
            input = input.ToLower().Replace(" ", "");
            return string.IsInterned(input) ?? input;
        }


        /// <summary>Places a lock tag on the given creature so that only one farmer may edit its properties at once. Returns True if the attempt to lock is successful.</summary>
        public static bool Lock(Character creature)
        {
            // Kick out any extra players trying to gain edit control
            if (IsLocked(creature))
                return false;

            // Lock 'em down
            creature.modData[EditLock] = "locked";

            return true;
        }


        /// <summary>Removes any current edit lock on the given creature. Returns True if the attempt to unlock is successful.</summary>
        public static void Unlock(Character creature)
        {
            // If the creature has no EditLock field, give it one and set it to unlocked
            if (!creature.modData.ContainsKey(EditLock))
            {
                creature.modData[EditLock] = "";
                return;
            }

            // LET THE DOGS OUT
            creature.modData[EditLock] = "";

            return;
        }


        /// <summary>Returns True if there is currently an editing lock tag on the given creature.</summary>
        public static bool IsLocked(Character creature)
        {
            return (creature.modData.ContainsKey(EditLock) && creature.modData[EditLock].Length != 0);
        }










        /************************
        ** Skin Handling
        *************************/
        
        /// <param name="creature">The creature (Pet, Horse, or FarmAnimal) to set the skin for</param>
        /// <param name="skinID">The file ID of the skin to set.</param>
        /// <returns>Returns the ID number of the skin, if successfully set.</returns>
        internal static int SetSkin(Character creature, int skinID)
        {
            string creatureType = ModApi.GetInternalType(creature);
            
            // Ensure that the given skin is available for use
            if (!DoesSkinExist(creatureType, skinID))
                return 0;

            // Ensure that the player can only change the skin of a horse being ridden if they are the one riding it
            if (creature is Horse horse && horse.rider != null && horse.rider != Game1.player)
            {
                SMonitor.Log($"{horse.Name} runs faster than you can paint it.", LogLevel.Warn);
                return 0;
            }

            // Assign skinID
            creature.modData[SkinID] = skinID.ToString();
            UpdateSkin(creature);

            return skinID;
        }

        
        /// <summary>Assigns the given creature a randomized skin from the ones available to it.</summary>
        internal static int RandomizeSkin(Character creature)
        {
            string creatureType = ModApi.GetInternalType(creature);

            // Check that the given creature type has skins available for use
            if (!ModApi.HasSkins(creatureType))
                return 0;

            // Collect a randomized skin ID for the given creature type
            int randomLookup = Randomizer.Next(0, Assets[creatureType].Keys.Count);

            // Set the skin
            return SetSkin(creature, Assets[creatureType].ElementAt(randomLookup).Key);
        }

        /// <summary>Returns the reference number of a random potential skin for the given creature type.</summary>
        internal static int GetRandomSkin(string creatureType)
        {
            creatureType = Sanitize(creatureType);
            if (!ModApi.HasSkins(creatureType))
                return 0;
            int randomLookup = Randomizer.Next(0, Assets[creatureType].Keys.Count);
            return Assets[creatureType].ElementAt(randomLookup).Key;
        }
        

        /// <summary>Returns the CreatureSkin instance for the skin asset that has been assigned to the given Pet, Horse, or FarmAnimal.
        /// If the creature does not have an assigned skin then return null.</summary>
        internal static CreatureSkin GetSkin(Character creature)
        {
            // If the creature does not have a skin, return null
            if (!creature.modData.ContainsKey(SkinID) || creature.modData[SkinID] == "0")
                return null;

            string type = ModApi.GetInternalType(creature);
            int skinID = int.Parse(creature.modData[SkinID]);

            if (creature is FarmAnimal animal)
            {
                if (ModApi.HasBabySprite(type) && animal.age.Value < animal.ageWhenMature.Value)
                    type = "baby" + type;
                else if (ModApi.HasShearedSprite(type) && animal.showDifferentTextureWhenReadyForHarvest.Value && animal.currentProduce.Value <= 0)
                    type = "sheared" + type;
            }

            if (DoesSkinExist(type, skinID))
                return Assets[type][skinID];

            return null;
        }

        internal static CreatureSkin GetSkin(string type, int skinID)
        {
            type = Sanitize(type);
            if (Assets.ContainsKey(type) && Assets[type].ContainsKey(skinID))
                return Assets[Sanitize(type)][skinID];
            else if (Assets.ContainsKey(type))
                return Assets[type][GetRandomSkin(type)];
            else
                return null;
        }
        


        internal static void UpdateSkin(Character creature)
        {
            CreatureSkin skin = GetSkin(creature);

            if (skin != null && creature.Sprite.textureName.Value != skin.AssetKey)
            {
                int[] spriteInfo = ModApi.GetSpriteInfo(creature);
                creature.Sprite = new AnimatedSprite(skin.AssetKey, spriteInfo[0], spriteInfo[1], spriteInfo[2]);
            }
        }


        /// <summary>Checks that the given creature type has skins loaded and that the given skin ID is one of these loaded skins.
        /// Returns False if the creature type has no loaded skins or does not have the specified skin. Otherwise returns True.</summary>
        internal static bool DoesSkinExist(string creatureType, int skinID)
        {
            // If the given creature has no skins for it to use outside of vanilla, report soft error
            if (!ModApi.HasSkins(creatureType))
            {
                ModEntry.SMonitor.Log($"The given creature type ({creatureType}) has no custom skins loaded.", LogLevel.Warn);
                return false;
            }
            // If the given creature has no skin with the specified value, report soft error
            if (!ModApi.HasSpecificSkin(creatureType, skinID))
            {
                ModEntry.SMonitor.Log($"The given creature type ({creatureType}) does not have a skin with ID #{skinID}.", LogLevel.Warn);
                return false;
            }

            return true;
        }

        /// <summary>Refreshes skins for Marnie's cows.</summary>
        internal static void RanchRefresh()
        {
            foreach (GameLocation loc in Game1.locations)
            {
                if (loc is Forest forest)
                    foreach (FarmAnimal animal in forest.marniesLivestock)
                    {
                        string type = ModApi.GetInternalType(animal);

                        // Random chance for cow to be a calf
                        int randomLookup = Randomizer.Next(0, 100);
                        if (randomLookup <= 15)
                            type = ModApi.GetInternalBabyType(type);

                        // Set a skin if A&S has cow skins available to it
                        if (ModApi.HasSkins(type))
                        {
                            int[] spriteInfo = ModApi.GetSpriteInfo(animal);
                            CreatureSkin skin = ModEntry.GetSkin(type, ModEntry.GetRandomSkin(type));
                            animal.Sprite = new AnimatedSprite(skin.AssetKey, spriteInfo[0], spriteInfo[1], spriteInfo[2]);
                        }
                    }
            }
        }
        









        // TODO: Is there a way to have other user's A&S systems alert the other farmers' systems of an updated value? Do we just need to continually check?
        // Have a count for last known pet/horse/farmanimal quantity in OnUpdateTicked, if new then locate new and remember their skin value as set in modData

        /************************
        ** ID Handling
        *************************/

        /// <summary>Returns a Short ID not currently in use within the A&S system.</summary>
        internal static int GetUnusedShortID()
        {
            int newShortID = 1;

            // Gather all Short ID numbers that are currently in use
            List<int> usedIDs = new List<int>();
            foreach (Character creature in ModApi.GetAllCreatures())
                usedIDs.Add(GetShortID(creature));

            // Find an unused Short ID
            while (usedIDs.Contains(newShortID))
                newShortID++;

            return newShortID;
        }


        /// <summary>Returns true if the given creature has been given necessary mod fields for skins and ID control</summary>
        internal bool HasModData(Character creature)
        {
            if (creature.modData.ContainsKey(ShortID) && creature.modData[ShortID] != "" && creature.modData.ContainsKey(SkinID) && creature.modData[SkinID] != "")
                return true;
            return false;
        }
        

        /// <summary>Returns the A&S Short ID of the given Pet, Horse, or FarmAnimal instance.</summary>
        internal static int GetShortID(Character creature) 
        {
            if (creature.modData.ContainsKey(ShortID) && creature.modData[ShortID] != "")
                return int.Parse(creature.modData[ShortID]);

            return 0;
        }


        /// <summary>Returns the Pet, Horse, or FarmAnimal that is assigned to the given Short ID. If none exists, returns null.</summary>
        internal static Character GetCreatureFromShortID(int shortID)
        {
            foreach (Character creature in ModApi.GetAllCreatures())
                if (int.Parse(creature.modData[ShortID]) == shortID)
                    return creature;
            return null;
        }










        /****************************
        ** Additional Functionality
        *****************************/


        









        /************************
        ** Save/Load/Update logic
        *************************/

        /// <summary>Checks changes to the NPC list in order to handle new instances of Pets and Horses appearing.
        /// This includes handling Horses as they are mounted and dismounted, as SDV removes and adds them to the NPC list at these times.</summary>
        internal void HandleNewPetsAndHorses(object sender, NpcListChangedEventArgs e)
        {

            foreach (Character creature in e.Added)
                if (Context.IsMainPlayer)
                {
                    // A pet has just been adopted and must be given A&S data
                    if (creature is Pet pet)
                        // ** TODO: This will need to be changed when Wild Horses and Strays are added, as they will not always be owned. May have to adjust Horse adding below.
                        GiveModFields(creature, false);

                    // A horse has just been adopted and must be given A&S data
                    else if (creature is Horse horse && horse != HorseMounted && !horse.modData.ContainsKey(ShortID))
                        GiveModFields(creature, false);
                }
                // A horse has been dismounted
                else if (creature is Horse horse && horse == HorseMounted)
                    HorseMounted = null;


            foreach (Character creature in e.Removed)
                //A horse has been mounted
                if (creature is Horse horse && horse.rider != null && horse.rider == Game1.player)
                    HorseMounted = horse;
        }


        /// <summary>If the given creature has not yet been given A&S modData, give it default values.</summary>
        internal void GiveModFields(Character creature, bool isUnowned)
        {
            if (!Context.IsMainPlayer)
                return;

            // ** TODO: Whether a creature is owned could be based on whether they are located on the farm > use MakeOwned on these so that pet follower can be compat.
                // Horses won't need to be re-entered after being dismounted, so adding them just once at the adoption phase should be enough
            if (!creature.modData.ContainsKey(EditLock))
                creature.modData[EditLock] = "true";
            else if (!Lock(creature))
                return;


            // ** TODO: This will need to be changed when Wild Horses and Strays are added, as they will not always be owned
            // If the creature is not a Wild Horse or Stray, give it a Short ID
            if (!isUnowned)
                MakeOwned(creature);
            // If the creature is a Wild Horse or Stray, do NOT give it a Short ID
            else
                if (!creature.modData.ContainsKey(ShortID))
                    creature.modData[ShortID] = "";


            // Give this new creature a random skin that is available to it
            if (!creature.modData.ContainsKey(SkinID))
                creature.modData[SkinID] = RandomizeSkin(creature).ToString();
            

            Unlock(creature);
        }


        /// <summary>Marks the given creature as being owned (AKA is not a Wild Horse or Stray pet) and assigns a Short ID if it does not have one.</summary>
        internal void MakeOwned(Character creature)
        {
            if (!creature.modData.ContainsKey(ShortID) || creature.modData[ShortID] == "")
                creature.modData[ShortID] = GetUnusedShortID().ToString();
            creature.modData[IsUnowned] = "";
        }


        internal void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // Display name tooltip if necessary
            if (Config.PetAndHorseNameTags)
                ToolTip.HoverCheck();

            if (Config.OneTileHorse && Game1.player.mount != null)
                Game1.player.mount.squeezeForGate();
                


            // Handle updating FarmAnimal count
            if (Context.IsMainPlayer && Game1.getFarm() != null)
            {
                Farm farm = Game1.getFarm();

                // Check that data is unlocked. Lock if it is, kick out if it's not.
                if (!farm.modData.ContainsKey(EditLock))
                    farm.modData[EditLock] = "lock";
                else if (farm.modData[EditLock] != "")
                    return;
                else
                    farm.modData[EditLock] = "lock";

                // Check that all animals are added and update the known FarmAnimal count
                if (IsAnimalCountChanged(farm))
                    EnsureAddAllFarmAnimals(farm);

                farm.modData[EditLock] = "";
            }

            

            // TODO: All farmers: check last known pet/horse/farmanimal counts?
            // This will need to be a value saved to individual A&S instances in SaveLoadHandler OR a running variable that is updated at loadtime.
            // This is on hold until it is verified in multiplayer that one player refreshing a creature's sprite texture does not in fact refresh it for all players.
        }


        internal static void OnDayStart(object sender, DayStartedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                foreach (Character creature in ModApi.GetAllCreatures())
                    ModEntry.UpdateSkin(creature);

                // Make sure Marnie's cows put some clothes on
                RanchRefresh();
            }
        }


        /// <summary>Returns true if the FarmAnimal count has changed. Otherwise returns false.</summary>
        internal bool IsAnimalCountChanged(Farm farm)
        {
            int count = farm.getAllFarmAnimals().Count;

            if (!farm.modData.ContainsKey(FarmAnimalCount))
            {
                farm.modData[FarmAnimalCount] = count.ToString();
                return true;
            }
            else if (int.Parse(farm.modData[FarmAnimalCount]) != count)
                return true;
            return false;
        }


        /// <summary>Checks every current FarmAnimal instance to ensure that it has mod data. If not, these values are added.</summary>
        internal void EnsureAddAllFarmAnimals(Farm farm)
        {
            foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                GiveModFields(animal, false);
            farm.modData[FarmAnimalCount] = farm.getAllFarmAnimals().Count.ToString();
        }
    }
}
