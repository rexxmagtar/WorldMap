using System;
using UnityEngine;

namespace com.pigsels.BubbleTrouble
{

    public class LevelProgress
    {
        public LevelIndex levelIndex;
        public bool IsCompleted;
        public int StarsAcquired;
    }

    /// <summary>
    /// Used for BiomeMap customization.
    /// Caches info about specified level.
    /// Contains all graphic stuff to display button for level.
    /// </summary>
    public class LevelButton : MonoBehaviour
    {
        /// <summary>
        /// Describes button's current state. Depends on buttons distance (measured in levels) from nearest completed level.
        /// </summary>
        public enum ButtonState
        {
            Completed,
            Current,
            Next,
            NextToNext,
            FarAway
        }

        [Tooltip("This is the level index within the biome. It must match the value of the 'index' field in MainConfig (where biomes and levels are configured).")]
        public int levelIndex;

        /// <summary>
        /// Cached structure info of level for which this button is responsible
        /// </summary>
        [NonSerialized]
        public LevelSettings LevelStructure;

        /// <summary>
        /// Cached progress info of level for which this button is responsible
        /// </summary>
        [NonSerialized]
        public LevelProgress LevelProgress;

        /// <summary>
        /// Button's current state
        /// </summary>
        [NonSerialized]
        public ButtonState CurrentState;
    }
}
