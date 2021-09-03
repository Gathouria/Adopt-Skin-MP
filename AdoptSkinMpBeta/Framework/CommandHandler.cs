using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StardewValley;
using StardewValley.Locations;
using StardewValley.Characters;
using StardewModdingAPI;

namespace AdoptSkinMpBeta.Framework
{
    class CommandHandler
    {
        /// <summary>Allowable custom creature group denotations for use in commands</summary>
        internal static readonly List<string> CreatureGroups = new List<string>() { "all", "animal", "coop", "barn", "chicken", "cow", "pet", "horse" };

        internal ModEntry Entry;

        internal CommandHandler(ModEntry modEntry)
        {
            Entry = modEntry;
        }





        /*****************************
        ** Console Command Handlers
        ******************************/
        internal void OnCommandReceived(string command, string[] args)
        {
            switch(command)
            {
                case "asdebug_ranch_refresh":
                    ModEntry.RanchRefresh();
                    return;
                case "asdebug_force_ids":
                    // ** TODO: existing save does not seem to reassign short IDs for those that were missing them. This is attempting to fix that while I figure out
                    // how to deal with it.
                    foreach (Character creature in ModApi.GetAllCreatures())
                        creature.modData[ModEntry.ShortID] = ModEntry.GetUnusedShortID().ToString();
                    return;

                case "asdebug_force_add_all":
                    foreach (Character creature in ModApi.GetAllCreatures())
                    {
                        ModApi.ClearCreatureProperties(creature);
                        // ** TODO: This will need to be changed when Wild Horses and Strays are added, as they will not always be owned
                        Entry.GiveModFields(creature, true);
                    }
                    return;

                case "asdebug_force_remove_all":
                    foreach (Character creature in ModApi.GetAllCreatures())
                        ModApi.ClearCreatureProperties(creature);
                    return;

                // Expected arguments: <creature type/category/group>
                case "list_creatures":
                    // Enforce argument constraints
                    if (!EnforceArgCount(args, 1) ||
                        !EnforceArgTypeGroup(args[0]))
                        return;

                    PrintRequestedCreatures(ModEntry.Sanitize(args[0]));
                    return;

                // Expected arguments: <creature ID> <string name>
                case "rename":
                    if (!EnforceArgCount(args, 2) ||
                        !EnforceArgTypeInt(args[0], 1))
                        return;

                    int idToName = int.Parse(args[0]);
                    Character creatureToName = EnforceIdAndGetCreature(idToName);

                    if (creatureToName == null)
                        return;

                    string oldName = creatureToName.Name;
                    creatureToName.Name = args[1];
                    creatureToName.displayName = args[1];
                    // Rename the horseName field if the original horse is being renamed.
                    if (creatureToName is Horse horseToName && oldName == Game1.player.horseName.ToString())
                        Game1.player.horseName.Set(args[1]);
                    ModEntry.SMonitor.Log($"{oldName} (ID {idToName}) has been renamed to {args[1]}", LogLevel.Info);

                    return;

                // Expected arguments: <creature group or creature ID>
                case "randomize_skin":
                    // Enforce argument constraints
                    if (!EnforceArgCount(args, 1))
                        return;

                    string call = ModEntry.Sanitize(args[0]);
                    if (CreatureGroups.Contains(call) || ModApi.GetHandledAllTypes().Contains(call))
                    {
                        List<Character> group = GetCreaturesFromGroup(call);
                        foreach (Character creature in group)
                            ModEntry.RandomizeSkin(creature);

                        ModEntry.SMonitor.Log($"All creatures in group `{call}` have been randomized.", LogLevel.Info);
                    }
                    else if (EnforceArgTypeInt(args[0], 1))
                    {
                        // Find associated creature instance
                        int creatureID = int.Parse(args[0]);
                        Character creature = EnforceIdAndGetCreature(creatureID);

                        // The given creature was not able to be located
                        if (creature == null)
                            return;

                        // A creature was able to be located with the given category and ID
                        if (ModEntry.RandomizeSkin(creature) == 0)
                            if (!(creature is Horse) || (creature is Horse horse && horse.rider != null && horse.rider == Game1.player))
                                ModEntry.SMonitor.Log($"No skins are located in `/assets/skins` for {creature.Name}'s type: {ModEntry.Sanitize(creature.GetType().Name)}", LogLevel.Error);
                            else
                                return;
                        else
                            ModEntry.SMonitor.Log($"{creature.Name}'s skin has been randomized.", LogLevel.Info);
                    }
                    return;


                // Expected arguments: <skin ID>, <creature ID>
                case "set_skin":
                    // Enforce argument constraints
                    if (!EnforceArgCount(args, 2) ||
                        !EnforceArgTypeInt(args[0], 1) ||
                        !EnforceArgTypeInt(args[1], 2))
                        return;

                    int skinID = int.Parse(args[0]);
                    int shortID = int.Parse(args[1]);
                    Character creatureToSkin = EnforceIdAndGetCreature(shortID);

                    if (creatureToSkin == null)
                        return;

                    // Enforce argument range to the range of the available skins for this creature's type
                    if (!ModEntry.Assets[ModApi.GetInternalType(creatureToSkin)].ContainsKey(skinID))
                    {
                        ModEntry.SMonitor.Log($"{creatureToSkin.Name}'s type ({ModApi.GetInternalType(creatureToSkin)}) has no skin with ID {skinID}", LogLevel.Error);
                        return;
                    }

                    // Successfully found given creature to set skin for
                    ModEntry.SetSkin(creatureToSkin, skinID);
                    ModEntry.SMonitor.Log($"{creatureToSkin.Name}'s skin has been set to skin {skinID}", LogLevel.Info);
                    return;

                default:
                    return;
            }
        }










        /*************************
        ** Miscellaneous Helpers
        *************************/

        /// <summary>Returns a List of Characters of all creatures of the specified creature type or custom grouping</summary>
        internal List<Character> GetCreaturesFromGroup(string group)
        {
            group = ModEntry.Sanitize(group);
            List<Character> calledGroup = new List<Character>();

            if (!CreatureGroups.Contains(group) && !ModApi.GetHandledAllTypes().Contains(group))
            {
                ModEntry.SMonitor.Log($"Specified grouping is not handled by Adopt & Skin: {group}", LogLevel.Error);
                return calledGroup;
            }

            // Add FarmAnimal types to the return list
            if (group == "all" || group == "animal")
                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                {
                    calledGroup.Add(animal);
                }
            else if (group == "coop")
                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                {
                    if (animal.isCoopDweller())
                        calledGroup.Add(animal);
                }
            else if (group == "barn")
                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                {
                    if (!animal.isCoopDweller())
                        calledGroup.Add(animal);
                }
            else if (group == "chicken")
                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                {
                    if (ModApi.IsChicken(animal))
                        calledGroup.Add(animal);
                }
            else if (group == "cow")
                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                {
                    if (ModApi.IsCow(animal))
                        calledGroup.Add(animal);
                }
            else if (ModApi.GetHandledAnimalTypes().Contains(group))
                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                {
                    if (ModApi.GetInternalType(animal) == group)
                        calledGroup.Add(animal);
                }


            // Add Pet types to the return list
            if (group == "all" || group == "pet")
                foreach (Pet pet in ModApi.GetPets())
                    calledGroup.Add(pet);
            else if (ModApi.GetHandledPetTypes().Contains(group))
                foreach (Pet pet in ModApi.GetPets())
                    if (ModApi.GetInternalType(pet) == group)
                        calledGroup.Add(pet);


            // Add Horse types to the return list
            if (group == "all" || ModApi.GetHandledHorseTypes().Contains(group))
                foreach (Horse horse in ModApi.GetHorses())
                    calledGroup.Add(horse);


            return calledGroup;
        }

        /// <summary>Checks that the correct number of arguments are given for a console command.</summary>
        /// <param name="args">Arguments given to the command</param>
        /// <param name="number">Correct number of arguments to give to the command</param>
        /// <returns>Returns true if the correct number of arguments was given. Otherwise gives a console error report and returns false.</returns>
        internal static bool EnforceArgCount(string[] args, int number)
        {
            if (args.Length == number)
            {
                return true;
            }
            ModEntry.SMonitor.Log($"Incorrect number of arguments given. The command requires {number} arguments, {args.Length} were given.", LogLevel.Error);
            return false;
        }


        /// <summary>Checks that the argument given is able to be parsed into an integer.</summary>
        /// <param name="arg">The argument to be checked</param>
        /// <param name="argNumber">The numbered order of the argument for the command (e.g. the first argument would be argNumber = 1)</param>
        /// <returns>Returns true if the given argument can be parsed as an int. Otherwise gives a console error report and returns false.</returns>
        internal static bool EnforceArgTypeInt(string arg, int argNumber)
        {
            if (!int.TryParse(arg, out int parsedArg))
            {
                ModEntry.SMonitor.Log($"Incorrect argument type given for argument {argNumber}. Expected type: int", LogLevel.Error);
                return false;
            }
            return true;
        }


        /// <summary>Checks that the argument given is a registered creature ID, and then returns any associated creature.</summary>
        /// <param name="id">The short ID to check</param>
        /// <returns>Returns the Character creature of the creature associated with the short ID, if one exists. Otherwise, returns null.</returns>
        internal static Character EnforceIdAndGetCreature(int id)
        {
            Character creature = ModEntry.GetCreatureFromShortID(id);

            if (creature == null)
            {
                ModEntry.SMonitor.Log($"No creature is registered with the given ID: {id}", LogLevel.Error);
                return null;
            }
            return creature;
        }


        /// <summary>Checks that the given argument is of a recognized creature group or recognized creature type.</summary>
        /// <param name="arg">The argument to be checked</param>
        /// <returns>Returns true if the given argument is stored in CreatureGroups or is a known FarmAnimal, Pet, or Horse type. Otherwise gives a console error report and returns false.</returns>
        internal static bool EnforceArgTypeGroup(string arg)
        {
            string type = ModEntry.Sanitize(arg);
            List<string> handledTypes = ModApi.GetHandledAllTypes();

            if (!CreatureGroups.Contains(type) && !handledTypes.Contains(type))
            {
                ModEntry.SMonitor.Log($"Argument given isn't one of {string.Join(", ", CreatureGroups)}, or a handled creature type. Handled types:\n{string.Join(", ", handledTypes)}", LogLevel.Error);
                return false;
            }

            return true;
        }










        /******************
        ** Print Strings
        ******************/

        /// <summary>Prints the the requested creature information from the list_animals console command.</summary>
        internal static void PrintRequestedCreatures(string arg)
        {
            string type = ModEntry.Sanitize(arg);

            // -- Handle FarmAnimal type arguments --
            if (type == "all" || type == "animal")
            {
                List<string> animalInfo = new List<string>();

                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                    animalInfo.Add(GetPrintString(animal));

                ModEntry.SMonitor.Log("Animals:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", animalInfo)}\n", LogLevel.Info);
            }
            // Handle coop animal types only
            else if (type == "coop")
            {
                List<string> coopInfo = new List<string>();

                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                    if (animal.isCoopDweller())
                        coopInfo.Add(GetPrintString(animal));

                ModEntry.SMonitor.Log("Coop Animals:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", coopInfo)}\n", LogLevel.Info);
            }
            // Handle barn animal types only
            else if (type == "barn")
            {
                List<string> barnInfo = new List<string>();

                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                    if (!animal.isCoopDweller())
                        barnInfo.Add(GetPrintString(animal));

                ModEntry.SMonitor.Log("Barn Animals:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", barnInfo)}\n", LogLevel.Info);
            }
            // Handle chicken type arguments
            else if (type == "chicken")
            {
                List<string> chickenInfo = new List<string>();

                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                    if (ModApi.IsChicken(ModApi.GetInternalType(animal)))
                        chickenInfo.Add(GetPrintString(animal));

                ModEntry.SMonitor.Log("Chickens:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", chickenInfo)}\n", LogLevel.Info);
            }
            // Handle cow type arguments
            else if (type == "cow")
            {
                List<string> cowInfo = new List<string>();

                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                    if (ModApi.IsCow(ModApi.GetInternalType(animal)))
                        cowInfo.Add(GetPrintString(animal));

                ModEntry.SMonitor.Log("Cows:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", cowInfo)}\n", LogLevel.Info);
            }
            // Handle other animal type arguments
            else if (ModApi.GetHandledAnimalTypes().Contains(type))
            {
                List<string> animalInfo = new List<string>();

                foreach (FarmAnimal animal in ModApi.GetFarmAnimals())
                    if (type == ModEntry.Sanitize(animal.type.Value))
                        animalInfo.Add(GetPrintString(animal));

                ModEntry.SMonitor.Log($"{arg}s:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", animalInfo)}\n", LogLevel.Info);
            }


            // -- Handle Pet type arguments --
            if (type == "all" || type == "pet")
            {
                List<string> petInfo = new List<string>();

                foreach (Pet pet in ModApi.GetPets())
                    petInfo.Add(GetPrintString(pet));

                ModEntry.SMonitor.Log("Pets:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", petInfo)}\n", LogLevel.Info);

            }
            else if (ModApi.GetHandledPetTypes().Contains(type))
            {
                List<string> petInfo = new List<string>();

                foreach (Pet pet in ModApi.GetPets())
                    if (type == ModEntry.Sanitize(pet.GetType().Name))
                        petInfo.Add(GetPrintString(pet));

                ModEntry.SMonitor.Log($"{arg}s:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", petInfo)}\n", LogLevel.Info);
            }


            // -- Handle Horse type arguments --
            if (type == "all" || ModApi.GetHandledHorseTypes().Contains(type))
            {
                List<string> horseInfo = new List<string>();

                foreach (Horse horse in ModApi.GetHorses())
                    horseInfo.Add(GetPrintString(horse));

                ModEntry.SMonitor.Log("Horses:", LogLevel.Debug);
                ModEntry.SMonitor.Log($"{string.Join(", ", horseInfo)}\n", LogLevel.Info);
            }
        }

        /// <summary>Return the information on a pet or horse that the list_animals console command uses.
        internal static string GetPrintString(Character creature)
        {
            string name = creature.Name;
            string type = ModApi.GetInternalType(creature);
            string shortID = creature.modData.ContainsKey(ModEntry.ShortID) ? creature.modData[ModEntry.ShortID] : "";
            string skinID = creature.modData.ContainsKey(ModEntry.SkinID) ? creature.modData[ModEntry.SkinID] : "";


            if (creature is Horse)
            {
                string horseRidden = "";

                if (ModApi.GetMountedHorses().Contains(creature))
                    horseRidden = $"(MOUNTED | Rider: {(creature as Horse).rider.displayName})";
                else
                    horseRidden = "(NOT MOUNTED)";

                return $"\n # {name}:  Type - {type} {horseRidden}\n" +
                    $"Short ID:   {shortID}\n" +
                    $"Skin ID:    {skinID}";
            }
            else
            {
                return $"\n # {name}:  Type - {type}\n" +
                    $"Short ID:   {shortID}\n" +
                    $"Skin ID:    {skinID}";
            }
        }
    }
}
