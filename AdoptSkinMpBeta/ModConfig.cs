using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdoptSkinMpBeta
{
    class ModConfig
    {
        /// <summary>Whether or not to allow horses being ridden to fit through any area that the player can normally walk through. Default: TRUE</summary>
        public bool OneTileHorse { get; set; } = true;
        /// <summary>Whether or not to display hovering tooltips for Horse and Pet names. Default: TRUE</summary>
        public bool PetAndHorseNameTags { get; set; } = true;



        /// <summary>Determines whether stray pets will appear at Marnie's after the player obtains a pet</summary>
        public bool StraySpawn { get; set; } = true;
    }
}