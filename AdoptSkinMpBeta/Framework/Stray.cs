using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StardewValley;
using StardewValley.Characters;

namespace AdoptSkinMpBeta.Framework
{
    class Stray
    {
        /// <summary>RNG for selecting randomized aspects</summary>
        private readonly Random Randomizer = new Random();

        /// <summary>Structures constructors for pet types</summary>
        internal static Dictionary<Type, Func<Pet>> PetConstructors = new Dictionary<Type, Func<Pet>>
        {
            { typeof(Dog), () => new Dog() },
            { typeof(Cat), () => new Cat() }
        };

    }
}
