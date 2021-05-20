using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace AdoptSkinMpBeta.Framework
{
    class CreatureSkin
    {
        /************************
       ** Accessors
       *************************/

        /// <summary>The animal, pet, or horse type for the skin.</summary>
        public string CreatureType { get; }

        /// <summary>The unique ID assigned to the skin. This is used code-internally and for user reference.</summary>
        public int ID { get; }

        /// <summary>The internal asset key of the skin, associated with the sprite file within the directory.</summary>
        public string AssetKey { get; }

        /// <summary>The Texture2D used by the animal as a sprite</summary>
        public Texture2D Texture { get; }





        /************************
        ** Public methods
        *************************/

        // <summary>Creates an instance in which to store a loaded skin asset's information for later use.
        public CreatureSkin(string creatureType, int id, string assetKey, Texture2D texture)
        {
            CreatureType = creatureType;
            ID = id;
            AssetKey = assetKey;
            Texture = texture;
        }


        // <summary>Allows comparison of two CreatureSkins, in which two skins are deemed identical if and only if both their creature types and skin IDs match.
        public class Comparer : IComparer<CreatureSkin>
        {
            public int Compare(CreatureSkin skin1, CreatureSkin skin2)
            {
                if (skin1.CreatureType.ToString().CompareTo(skin2.CreatureType.ToString()) == 0)
                    return skin1.ID.CompareTo(skin2.ID);
                return -1;
            }
        }
    }
}
