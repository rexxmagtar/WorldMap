using System.Collections;
using System.Collections.Generic;
using com.pigsels.BubbleTrouble;
using UnityEngine;

namespace com.pigsels.BubbleTrouble
{

    /// <summary>
    /// Is used by model of worldMapController MVC.
    /// Stores info about map biomes
    /// </summary>
    public class BiomeChunk
    {
        public enum State
        {
            Empty,
            Loading,
            Failed,
            Initializing,
            Ready
        }

        /// <summary>
        /// Bound of the biome.
        /// </summary>
        public Bounds bounds;

        /// <summary>
        /// GameObject that represent loaded biome.
        /// </summary>
        public GameObject biome;

        /// <summary>
        /// Current state of the BiomeChunk.
        /// </summary>
        public State currentState;

        /// <summary>
        /// Unique biome id as defined in AppSettings.
        /// </summary>
        public string biomeId;

        /// <summary>
        /// Entire map fragment of a biome that handles all map functions of a biome (background, animations, buttons, sounds etc).
        /// </summary>
        public BiomeMap biomeMap;

        /// <summary>
        /// Size of the biome data in bytes.
        /// </summary>
        public float loadSize = -1;


        public BiomeChunk(GameObject _biome = null, State _currentState = State.Empty, string _biomeId = null, Bounds _bounds = default)
        {
            biome = _biome;
            currentState = _currentState;
            biomeId = _biomeId;
            bounds = _bounds;
        }

        public override string ToString()
        {
            return biomeId;
        }
    }
}