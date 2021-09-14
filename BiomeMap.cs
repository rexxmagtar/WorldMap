using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace com.pigsels.BubbleTrouble
{
    /// <summary>
    /// Implements Biome Map fragment behaviour including biome map buttons and animations.
    /// Every biome map fragment has an GameObject with this component added and is controlled by it.
    /// </summary>
    public class BiomeMap : MonoBehaviour
    {
        private List<LevelButton> LevelButtons;
        public string BiomeId;
        private int StarWallHeight;
        private MapView mapView;
        private bool IsActive;
        private bool IsCurrentBiome;
        private int GainedStarsCount = 0;

#region Controller's logic

        /// <summary>
        /// Initializes this BiomeMap fragment and links buttons with actual levels.
        /// Called by WorldMapController when this BiomeMap fragment loads.
        /// Init should be done here, not in Awake() or Start()
        /// </summary>
        public void Init(MapView mapView, string biomeId)
        {
            this.mapView = mapView;
            BiomeId = biomeId;

            StarWallHeight = GameManager.Settings.biomes.Find(bm => bm.biomeId == biomeId).starWallHeight;

            FillLevelButtonsList();
            InitLevels();
            IsCurrentBiome = CheckIsCurrentBiome();
            GainedStarsCount = GetGainedStarsCount();
            DrawButtons();
        }


        //TODO: this is temp method till we don't have any comfortable editor
        /// <summary>
        /// Fills the LevelButtons list with LevelButtons placed on BiomeMap
        /// </summary>
        private void FillLevelButtonsList()
        {
            GameObject canvasObj = gameObject.GetComponentInChildren<Canvas>().gameObject;
            LevelButton[] buttons = canvasObj.GetComponentsInChildren<LevelButton>();
            LevelButtons = new List<LevelButton>();
            for (int i = 0; i < buttons.Length; i++)
            {
                LevelButtons.Add(buttons[i]);
            }
        }

        /// <summary>
        /// Init all Buttons
        /// </summary>
        private void InitLevels()
        {
            List<LevelButton> buttonsToRemove = new List<LevelButton>();

            foreach (var levelButton in LevelButtons)
            {
                LevelIndex levelIndex = new LevelIndex(BiomeId, levelButton.levelIndex);

                LevelSettings levelStructure = GameManager.Settings.GetLevelSettings(levelIndex);

                if (levelStructure == null || levelStructure.isDisabled)
                {
                    Debug.LogWarning($"The level [{levelButton.levelIndex}] of the biome '{BiomeId}" +
                                     $" is either nonexistent or disabled. It won't be displayed on the map.");

                    buttonsToRemove.Add(levelButton);

                    continue;
                }

                levelButton.LevelStructure = levelStructure;

                levelButton.LevelProgress = GetLevelProgress(levelIndex);
                levelButton.CurrentState = GetButtonState(levelButton);
                levelButton.GetComponentInChildren<Button>().onClick.AddListener(delegate
                {
                    OnLevelClicked(levelButton);
                });
            }

            foreach (var levelButton in buttonsToRemove)
            {
                levelButton.gameObject.SetActive(false);

                LevelButtons.Remove(levelButton);
            }
        }

        //Handles user's click on button
        public void OnLevelClicked(LevelButton levelButton)
        {
            //Debug.Log("Clicked" + levelButton.LevelId);
            if (levelButton.CurrentState == LevelButton.ButtonState.Current ||
                levelButton.CurrentState == LevelButton.ButtonState.Completed)
            {
                int remainingStarsCount = StarWallHeight - GainedStarsCount;

                if (!IsLevelLastOnBiome(levelButton.LevelStructure) || remainingStarsCount <= 0)
                {
                    mapView.OnLevelButtonClicked(levelButton.LevelStructure.levelIndex, levelButton.LevelProgress.StarsAcquired);
                }
                else
                {
                    //TODO: this message must be replaced with custom dialog
                    GameManager.UIManager.ShowOkDialog("Not enough stars",
                        "You need to achieve more stars to play this level. Try to complete better previous levels on this biome");
                }
            }
        }

        /// <summary>
        /// Gets the distance (measured in levels) between specified button and nearest completed level's button
        /// Gets ButtonState based on this distance
        /// </summary>
        /// <param name="levelButton"></param>
        /// <returns></returns>
        private LevelButton.ButtonState GetButtonState(LevelButton levelButton)
        {
            //Gets state of button based on its distance from the nearest completed level
            LevelButton.ButtonState _getButtonStateByLength(int length)
            {
                switch (length)
                {
                    case 1:
                    {
                        return LevelButton.ButtonState.Current;
                    }
                    case 2:
                    {
                        return LevelButton.ButtonState.Next;
                    }
                    case 3:
                    {
                        return LevelButton.ButtonState.NextToNext;
                    }
                    default:
                    {
                        return LevelButton.ButtonState.FarAway;
                    }
                }
            }

            if (GameManager.ProfileManager.IsLevelCompleted(levelButton.LevelStructure.levelIndex))
            {
                return LevelButton.ButtonState.Completed;
            }


            LevelIndex currentLevelIndex = levelButton.LevelStructure.levelIndex;

            for (int currentLength = 1; currentLength < 4; currentLength++)
            {
                LevelIndex parentLevelIndex = GetParentLevelIndex(currentLevelIndex);

                if (parentLevelIndex == null)
                {
                    if (GameManager.Settings.GetLevelSettings(currentLevelIndex) == null)
                    {
                        throw new Exception($"Invalid levelIndex is specified for level button {levelButton.gameObject.name} in BiomeMap for biome {BiomeId} : {currentLevelIndex.levelId}");
                    }

                    // this happens only in case where user has 0 levels completed
                    return _getButtonStateByLength(currentLength);
                }

                currentLevelIndex = parentLevelIndex;

                if (GameManager.ProfileManager.IsLevelCompleted(currentLevelIndex))
                {
                    return _getButtonStateByLength(currentLength);
                }
            }

            return LevelButton.ButtonState.FarAway;
        }

        //Gets amount of stars users already achieved on this biome
        private int GetGainedStarsCount()
        {
            int starsCount = 0;

            for (int i = 0; i < LevelButtons.Count; i++)
            {
                starsCount += LevelButtons[i].LevelProgress.StarsAcquired;
            }
            return starsCount;
        }

#endregion

#region Level info wrappers

        //Get's parent of specified level. Iterates through all levels in game, so that parent of first level of a biome will be last level of previous biome,
        //vise versa for the last level of biome...
        private LevelIndex GetParentLevelIndex(LevelIndex levelIndex)
        {
            return GameManager.Settings.GetPrevLevelIndex(levelIndex);
        }

        /// <summary>
        /// Returns LevelProgress for a specified game level.
        /// </summary>
        /// <param name="levelIndex">LevelIndex of a level to return progress for.</param>
        /// <returns></returns>
        private LevelProgress GetLevelProgress(LevelIndex levelIndex)
        {
            var levelSaveData = GameManager.ProfileManager.GetLevelSaveDataCopy(levelIndex);

            return new LevelProgress
            {
                IsCompleted = GameManager.ProfileManager.IsLevelCompleted(levelIndex),
                StarsAcquired = (levelSaveData == null) ? 0 : levelSaveData.maxStarsEarned,
                levelIndex = levelIndex
            };
        }

        /// <summary>
        /// Checks if level is the last level of current biome.
        /// </summary>
        /// <param name="levelStructure"></param>
        /// <returns></returns>
        private bool IsLevelLastOnBiome(LevelSettings levelStructure)
        {
            var nextLevel = GameManager.Settings.GetNextBasicLevelIndex(levelStructure.levelIndex);

            return !levelStructure.isExtra && (nextLevel == null || nextLevel.biomeId != levelStructure.levelIndex.biomeId);
        }

#endregion

#region Buttons drawing logic

        //TODO: change loading textures method (will be decided after Spine investigation).
        /// <summary>
        /// Gets sprite for button
        /// </summary>
        /// <param name="levelButton"></param>
        /// <returns></returns>
        private Sprite GetButtonSprite(LevelButton levelButton)
        {
            switch (levelButton.CurrentState)
            {
                case LevelButton.ButtonState.Completed:
                {
                    if (levelButton.LevelStructure.isExtra)
                    {
                        if (levelButton.LevelProgress.StarsAcquired > 0 && IsCurrentBiome)
                        {
                            return Resources.Load<Sprite>($"Textures/MapButtons/CompletedExtraStars{levelButton.LevelProgress.StarsAcquired}");
                        }
                        return Resources.Load<Sprite>($"Textures/MapButtons/CompletedExtra");
                    }
                    else
                    {
                        if (levelButton.LevelProgress.StarsAcquired > 0 && IsCurrentBiome)
                        {
                            return Resources.Load<Sprite>($"Textures/MapButtons/CompletedNormalStars{levelButton.LevelProgress.StarsAcquired}");
                        }
                        return Resources.Load<Sprite>("Textures/MapButtons/CompletedNormal");
                    }
                }
                case LevelButton.ButtonState.Current:
                {
                    if (levelButton.LevelStructure.isExtra)
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/CurrentExtra");
                    }
                    else
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/CurrentNormal");
                    }
                }
                case LevelButton.ButtonState.Next:
                {
                    if (levelButton.LevelStructure.isExtra)
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/NextExtra");
                    }
                    else
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/NextNormal");
                    }
                }
                case LevelButton.ButtonState.NextToNext:
                {
                    if (levelButton.LevelStructure.isExtra)
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/NextToNextExtra");
                    }
                    else
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/NextToNextNormal");
                    }
                }
                case LevelButton.ButtonState.FarAway:
                {
                    if (levelButton.LevelStructure.isExtra)
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/FarAwayExtra");
                    }
                    else
                    {
                        return Resources.Load<Sprite>("Textures/MapButtons/FarAwayNormal");
                    }
                }
                default:
                {
                    throw new Exception("Unknown levelButton state! " + levelButton.CurrentState + " of " + levelButton);
                }
            }
        }

        /// <summary>
        /// Checks if this biome is current biome the user plays on.
        /// It means that this biome contains non extra level that user is able to play on but did not complete this level yet.
        /// </summary>
        /// <returns>True is this Biome is current (contains the level that should be played next).</returns>
        private bool CheckIsCurrentBiome()
        {
            if (LevelButtons.Count == 0)
            {
                return false;
            }

            int minimumLevelId = LevelButtons.ToList().FindAll(btn => !btn.LevelStructure.isExtra).Min(btn => btn.levelIndex);

            LevelButton firstBiomesLevel = LevelButtons.Find(button => button.LevelStructure.levelId == minimumLevelId);

            int lastLevelIndex = LevelButtons.FindAll(button => button.LevelStructure.isExtra == false).Max(button => button.LevelStructure.levelId);

            LevelButton lastBiomesLevel = LevelButtons.Find(levelButton => levelButton.LevelStructure.levelId == lastLevelIndex);

            return (firstBiomesLevel.CurrentState == LevelButton.ButtonState.Current || firstBiomesLevel.CurrentState == LevelButton.ButtonState.Completed) &&
                   lastBiomesLevel.CurrentState != LevelButton.ButtonState.Completed;
        }

        /// <summary>
        /// Draws buttons on Map (Sets up animations,sprites and e.t.c)
        /// </summary>
        private void DrawButtons()
        {
            for (int i = 0; i < LevelButtons.Count; i++)
            {
                DrawButton(LevelButtons[i]);
            }
        }

        /// <summary>
        /// Gets the position of specified level on Map (in world coordinates)
        /// </summary>
        /// <param name="levelId"></param>
        /// <returns></returns>
        public Vector3 GetLevelButtonPosition(int levelId)
        {
            LevelButton foundLevelButton = LevelButtons.Find((LevelButton levelButton) => levelButton.LevelStructure.levelId == levelId);
            if (foundLevelButton == null)
            {
                throw new Exception($"Could not find level's {levelId} button in {BiomeId} map prefab");
            }
            Vector3 position = foundLevelButton.gameObject.transform.position;
            return position;
        }

        /// <summary>
        /// Draws button (sets animation info, sprites and e.t.c)
        /// </summary>
        /// <param name="levelButton"></param>
        private void DrawButton(LevelButton levelButton)
        {
            levelButton.gameObject.GetComponentInChildren<Button>().gameObject.GetComponent<Image>().sprite = GetButtonSprite(levelButton);

            bool isLastLevel = IsLevelLastOnBiome(levelButton.LevelStructure);

            if (!isLastLevel || StarWallHeight == 0)
            {
                levelButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "";
                levelButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().gameObject.transform.parent.GetComponentInChildren<Image>().gameObject.SetActive(false);
            }
            else
            {
                levelButton.gameObject.transform.localScale *= 1.25f;

                int remainedStars = StarWallHeight - GainedStarsCount;

                if (remainedStars > 0)
                {
                    levelButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().text = (remainedStars).ToString();
                }
                else
                {
                    Destroy(levelButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().gameObject);
                }
            }
        }

        /// <summary>
        /// Called by WorldMapController when this BiomeMap fragment becomes visible to user and
        /// should start (continue) animations and become interactive.
        /// </summary>
        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;

            for (int i = 0; i < LevelButtons.Count; i++)
            {
                ActivateButton(LevelButtons[i]);
            }
        }

        /// <summary>
        /// Activates levelButton (starts animations, changes sprite if needed  and  e.t.c ...)
        /// </summary>
        /// <param name="levelButton"></param>
        private void ActivateButton(LevelButton levelButton)
        {
            levelButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Deactivates levelButton (stops animations , and e.t.c ...)
        /// </summary>
        /// <param name="levelButton"></param>
        private void DeactivateButton(LevelButton levelButton)
        {
            levelButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Called by WorldMapController when this BiomeMap fragment becomes invisible to user
        /// (is scrolled out of the viewport) and should pause animations and become inactive.
        /// </summary>
        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;

            for (int i = 0; i < LevelButtons.Count; i++)
            {
                DeactivateButton(LevelButtons[i]);
            }
        }

#endregion
    }
}