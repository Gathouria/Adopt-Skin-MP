using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StardewModdingAPI;

using StardewValley;
using StardewValley.Characters;
using StardewValley.Buildings;

using AdoptSkinMpBeta.Framework;

namespace AdoptSkinMpBeta
{
    public class ModApi
    {
        /************************
        ** Fields
        *************************/

        private static List<string> HandledPetTypes = new List<string>();
        private static List<string> HandledHorseTypes = new List<string>();
        private static List<string> HandledFarmAnimalTypes = new List<string>();

        /// <summary>Registers a creature type to handle skin support for. Must inherit from one of the classes: Pet, Horse, FarmAnimal.</summary>
        /// <param name="id">The filename for the animal. This will also be its internal ID within Adopt & Skin.</param>
        /// <param name="hasBaby">If this animal type has a baby skin.</param>
        /// <param name="canShear">If this animal type has a sheared skin.</param>
        public static void RegisterType(string id, Type type, bool hasBaby = true, bool canShear = false)
        {
            id = ModEntry.Sanitize(id);
            if (ModEntry.Assets.ContainsKey(id))
            {
                ModEntry.SMonitor.Log("Unable to register type, type already registered: " + id, LogLevel.Debug);
                return;
            }

            // Ensure inherits from one of the accepted classes and register class-specific information
            if (typeof(Pet).IsAssignableFrom(type))
                HandledPetTypes.Add(id);

            else if (typeof(Horse).IsAssignableFrom(type))
                HandledHorseTypes.Add(id);

            else if (typeof(FarmAnimal).IsAssignableFrom(type))
            {
                HandledFarmAnimalTypes.Add(id);

                // Registers the Baby and Sheared sprites, if given
                if (hasBaby)
                    RegisterType("Baby" + id, typeof(FarmAnimal), false, false);
                if (canShear)
                    RegisterType("Sheared" + id, typeof(FarmAnimal), false, false);
            }
            else
            {
                ModEntry.SMonitor.Log("Unable to register type, type does not inherit from any of the classes Pet, Horse, or FarmAnimal: " + id, LogLevel.Debug);
                return;
            }

            // Create an entry for assets to be added in ModEntry
            ModEntry.Assets.Add(id, new Dictionary<int, CreatureSkin>());
        }


        /// <summary>Returns all handled types Adopt & Skin is currently handling.</summary>
        public static List<string> GetHandledAllTypes()
        {
            List<string> defaultTypes = new List<string>();
            defaultTypes.AddRange(GetHandledAnimalTypes());
            defaultTypes.AddRange(GetHandledPetTypes());
            defaultTypes.AddRange(GetHandledHorseTypes());

            return defaultTypes;
        }

        /// <summary>Returns all pet types Adopt & Skin is currently handling.</summary>
        public static List<string> GetHandledPetTypes() { return new List<string>(HandledPetTypes); }

        /// <summary>Returns all horse types Adopt & Skin is currently handling.</summary>
        public static List<string> GetHandledHorseTypes() { return new List<string>(HandledHorseTypes); }

        /// <summary>Returns all animal types Adopt & Skin is currently handling.</summary>
        public static List<string> GetHandledAnimalTypes() { return new List<string>(HandledFarmAnimalTypes); }

        /// <summary>Returns true if the given creature subtype (i.e. Dog, Cat, WhiteChicken) is being handled by A&S</summary>
        public static bool IsRegisteredType(string type) { return ModEntry.Assets.ContainsKey(ModEntry.Sanitize(type)); }

        /// <summary>Returns the string used to reference the given creature's type within A&S</summary>
        public static string GetInternalType(Character creature)
        {
            if (creature is Pet || creature is Horse)
                return ModEntry.Sanitize(creature.GetType().Name);
            else if (creature is FarmAnimal animal)
                return ModEntry.Sanitize(animal.type.Value);
            return "";
        }

        /// <summary>Returns true if the given creature subtype (i.e. Dog, Cat, WhiteChicken) has at least one custom skin loaded for it in A&S.</summary>
        public static bool HasSkins(string type) { return (ModEntry.Assets.ContainsKey(ModEntry.Sanitize(type)) && (ModEntry.Assets[ModEntry.Sanitize(type)]).Count > 0); }

        /// <summary>Returns true if the given creature has a custom skin assigned to it.</summary>
        public static bool HasSkin(Character creature) { return creature.modData[ModEntry.SkinID] != "0"; }

        /// <summary>Returns true if the given creature subtype (i.e. Dog, Cat, WhiteChicken) the given skinID value loaded.</summary>
        public static bool HasSpecificSkin(string type, int skinID) { return (ModEntry.Assets.ContainsKey(ModEntry.Sanitize(type)) && (ModEntry.Assets[ModEntry.Sanitize(type)].ContainsKey(skinID))); }

        /// <summary>Returns whether the given type contains the word "chicken"</summary>
        public static bool IsChicken(string type) { return ModEntry.Sanitize(type).Contains("chicken"); }
        public static bool IsChicken(FarmAnimal animal) { return IsChicken(animal.type.Value); }

        /// <summary>Returns whether the given type contains the word "cow"</summary>
        public static bool IsCow(string type) { return ModEntry.Sanitize(type).Contains("cow"); }
        public static bool IsCow(FarmAnimal animal) { return IsCow(animal.type.Value); }

        public static bool HasBabySprite(string type) { return ModEntry.Assets.ContainsKey("baby" + ModEntry.Sanitize(type)); }

        public static bool HasShearedSprite(string type) { return ModEntry.Assets.ContainsKey("sheared" + ModEntry.Sanitize(type)); }
        /// <summary>Returns whether the given instance of Horse is an instance of a tractor from the tractors mod.</summary>
        public static bool IsNotATractor(Horse horse) { return !horse.Name.StartsWith("tractor/"); }

        /// <summary>Returns an enumerable list of all Horse instances. This excludes tractors.</summary>
        public static IEnumerable<Horse> GetHorses()
        {
            foreach (NPC npc in Utility.getAllCharacters())
                if (npc is Horse horse && ModApi.IsNotATractor(horse))
                    yield return horse;
            foreach (Horse horse in GetMountedHorses())
                yield return horse;

            // TODO: Exclude wild horses
            // TODO: Add horses that are currently being ridden
        }

        /// <summary>Returns an enumerable list of all Horse instances currently being ridden. This excludes tractors.</summary>
        public static IEnumerable<Horse> GetMountedHorses()
        {
            foreach (Farmer farmer in Game1.getAllFarmers())
                if (farmer.mount != null && IsNotATractor(farmer.mount))
                    yield return farmer.mount;
        }

        /// <summary>Returns an enumerable list of all Pet instances.</summary>
        public static IEnumerable<Pet> GetPets()
        {
            foreach (NPC npc in Utility.getAllCharacters())
                if (npc is Pet pet)
                    yield return pet;

            // TODO: Exclude stray pets
        }

        /// <summary>Returns an enumerable list of all FarmAnimal instances on the Farm.</summary>
        public static IEnumerable<FarmAnimal> GetFarmAnimals()
        {
            Farm farm = Game1.getFarm();

            if (farm == null)
                yield break;

            foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                yield return animal;
        }

        /// <summary>Returns an enumerable list of all existing Horses (excluding tractors), Pets, and FarmAnimals</summary>
        public static IEnumerable<Character> GetAllCreatures() { return GetHorses().Concat<Character>(GetPets()).Concat<Character>(GetFarmAnimals()); }

        public static ModEntry.CreatureCategory GetCreatureCategory(Character creature) { return GetCreatureCategory(GetInternalType(creature)); }

        public static ModEntry.CreatureCategory GetCreatureCategory(string type)
        {
            type = ModEntry.Sanitize(type);
            if (HandledPetTypes.Contains(type))
                return ModEntry.CreatureCategory.Pet;
            else if (HandledHorseTypes.Contains(type))
                return ModEntry.CreatureCategory.Horse;
            else if (HandledFarmAnimalTypes.Contains(type))
                return ModEntry.CreatureCategory.Animal;
            else
                return ModEntry.CreatureCategory.Null;
        }

        public static void ClearCreatureProperties(Character creature)
        {
            if (ModEntry.IsLocked(creature))
            {
                ModEntry.SMonitor.Log($"Creature {creature.Name} ({GetInternalType(creature)}) is being edited and cannot have its properties cleared.");
                return;
            }


            creature.modData[ModEntry.SkinID] = ModEntry.RandomizeSkin(creature).ToString();

            if (creature.modData.ContainsKey(ModEntry.ShortID) && creature.modData[ModEntry.ShortID].Length != 0)
                creature.modData[ModEntry.ShortID] = ModEntry.GetUnusedShortID().ToString();
            else
                creature.modData[ModEntry.ShortID] = "";

            ModEntry.SMonitor.Log($"Properties cleared for {creature.GetType()} {creature.Name}", LogLevel.Warn);

            return;
        }

        /// <summary>Returns an array of the given creature's sprite's information: [StartFrame, Width, Height].</summary>
        public static int[] GetSpriteInfo(Character creature)
        {
            int[] info = new int[3];
            switch (GetCreatureCategory(creature))
            {
                case ModEntry.CreatureCategory.Pet:
                    info[0] = ModEntry.PetSpriteStartFrame;
                    info[1] = 32;
                    info[2] = 32;
                    return info;
                case ModEntry.CreatureCategory.Horse:
                    info[0] = ModEntry.HorseSpriteStartFrame;
                    info[1] = 32;
                    info[2] = 32;
                    return info;
                case ModEntry.CreatureCategory.Animal:
                    info[0] = ModEntry.AnimalSpriteStartFrame;
                    info[1] = (creature as FarmAnimal).frontBackSourceRect.Width;
                    info[2] = (creature as FarmAnimal).frontBackSourceRect.Height;
                    return info;
                default:
                    return info;
            }
        }

    }
}