using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.pigsels.BubbleTrouble;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class MapModelController
{
#region class fields

    private Canvas canvas;

    private int BiomesLoadBehindCount;
    private int BiomesLoadAheadCount;
    private string StartBiomeId;

    private ScrollRect ScrollRect;
    private GameObject ScrollContent;
    private RectTransform ContentTransform;

    private List<BiomeChunk> BiomeList;
    private BiomeChunk CurrentBiomeChunk;

    //TODO: only for debug. Remove later.
    private bool CanLoad = true;

    private bool isPaused = false;

    private WorldMapSceneManager sceneManager;

    private MapView mapView;

    private bool showedDialog = false;

#endregion


    private bool IsContentDragged
    {
        get
        {
            return Input.GetMouseButton(0);
        }
    }

    public IEnumerator Init()
    {
        return InitBiomeList();
    }

    public void Deinit()
    {
        UnloadBiomes(BiomesLoadBehindCount + 1, BiomesLoadAheadCount, false);

        mapView.OnBiomeChanged -= OnCurrentBiomeChangedListener;
        mapView.OnReloadRequest -= HandleReloadRequest;
    }

    public void SetPause(bool pause)
    {
        isPaused = pause;
    }

    public void Update()
    {
        if (isPaused)
        {
            return;
        }

#if UNITY_EDITOR
        //TODO: debug only, remove.
        if (Input.GetKeyDown(KeyCode.Z))
        {
            CanLoad = !CanLoad;
        }
#endif

    }

    public MapModelController(WorldMapSceneManager worldMapSceneManager, MapView mapView)
    {
        this.mapView = mapView;

        BiomeList = new List<BiomeChunk>();
        this.mapView.BiomeList = BiomeList;

        canvas = worldMapSceneManager.canvas;
        BiomesLoadBehindCount = worldMapSceneManager.BiomesLoadBehindCount;
        BiomesLoadAheadCount = worldMapSceneManager.BiomesLoadAheadCount;
        this.sceneManager = worldMapSceneManager;

        LevelIndex currentCursorPosition = GetCurrentCursorPosition();

        Debug.Log($"Cursor position: {currentCursorPosition}");

        StartBiomeId = currentCursorPosition.biomeId;

        this.mapView.OnBiomeChanged += OnCurrentBiomeChangedListener;
        this.mapView.OnReloadRequest += HandleReloadRequest;

        ScrollRect = canvas.GetComponentInChildren<ScrollRect>();
        ScrollContent = ScrollRect.content.gameObject;
        ContentTransform = ScrollContent.GetComponent<RectTransform>();
    }

    private IEnumerator InitBiomeList()
    {
        bool abortFlag = false;
        //Debug.Log(">>>InitBiomeList 1");
        BiomeList?.Clear();

        LoadBiomes(BiomesLoadBehindCount + 1, BiomesLoadAheadCount, true);

        // Waiting for biomes to load successfully
        while (true)
        {
            // One ore more biome chunks failed to load and user decided not to retry loading them.
            // Aborting WorldMapController Init.
            if (abortFlag)
            {
                abortFlag = false;

                if (BiomeList[BiomesLoadBehindCount].currentState != BiomeChunk.State.Failed)
                {
                    CurrentBiomeChunk = BiomeList[BiomesLoadBehindCount];
                    mapView.OnBiomeChunksListChangedListener(true);
                    yield break;
                }
                else
                {
                    sceneManager.AbortInit("Could not load map fragment");
                }
            }

            // Waiting for all biome chunks to complete load or fail
            while (BiomeList.Count(chunk => chunk.currentState != BiomeChunk.State.Loading) < BiomeList.Count)
            {
                yield return null;
            }

            // If there are failed to load biomes
            if (BiomeList.Count(chunk => chunk.currentState == BiomeChunk.State.Failed) > 0)
            {
                if (!showedDialog)
                {

                    showedDialog = true;
                    GameManager.UIManager.ShowYesNoDialog("Failed to load map. Try again?",
                        string.Join("\n", BiomeList.Where(chunk => chunk.currentState == BiomeChunk.State.Failed)), false).OnButtonPressed += (buttonId, dialogHandle) =>
                    {
                        showedDialog = false;
                        switch (buttonId)
                        {
                            case "yes":
                            {
                                for (int i = 0; i < BiomeList.Count; i++)
                                {
                                    if (BiomeList[i].currentState == BiomeChunk.State.Failed)
                                    {
                                        LoadBiome(i, BiomeList[i].biomeId, true);
                                    }
                                }

                                break;
                            }
                            case "no":
                            {
                                // abort will be called from inside InitBiomeList context.
                                // (the exception would not be caught if it would have been thrown here.)
                                abortFlag = true;
                                break;
                            }
                            default:
                            {
                                throw new Exception("Unknown button id" + buttonId);
                            }
                        }
                    };
                }
            }
            // init phase complete (all initial biomes are loaded)
            else
            {
                CurrentBiomeChunk = BiomeList[BiomesLoadBehindCount];
                mapView.OnBiomeChunksListChangedListener(true);
                yield break;
            }


            yield return null;

        }

    }

    /// <summary>
    /// Unload biomes from left/right side from BiomeList.
    /// </summary>
    /// <param name="leftPart"></param>
    /// <param name="rightPart"></param>
    /// <param name="changeBorders">if true - borders of scrollable content wil be resized </param>
    private void UnloadBiomes(int leftPart, int rightPart, bool changeBorders = true)
    {
        for (int i = 0; i < leftPart; i++)
        {
            UnloadBiome(WorldMapController.Direction.Left, changeBorders);
        }

        for (int i = 0; i < rightPart; i++)
        {
            UnloadBiome(WorldMapController.Direction.Right, changeBorders);
        }
    }

    /// <summary>
    ///Unloads single biome from specified side
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="changeBorders">if true - borders of scrollable content wil be resized</param>
    private void UnloadBiome(WorldMapController.Direction direction, bool changeBorders)
    {
        int indexToRemoveAt = -1;

        switch (direction)
        {
            case WorldMapController.Direction.Left:
                indexToRemoveAt = 0;
                break;

            case WorldMapController.Direction.Right:
                indexToRemoveAt = BiomeList.Count - 1;
                break;
        }

        if (BiomeList[indexToRemoveAt].biome != null)
        {
            //DestroyImmediate(LoadedBiomeChunks[indexToRemoveAt].Biome);
            GameObject.Destroy(BiomeList[indexToRemoveAt].biome);
        }


        if (BiomeList[indexToRemoveAt].currentState != BiomeChunk.State.Empty)
        {
            GameManager.ResourceLoader.UnloadAsset<GameObject>(ResourceLoader.GetBiomeMapAssetName(BiomeList[indexToRemoveAt].biomeId));
        }

        BiomeList[indexToRemoveAt].currentState = BiomeChunk.State.Empty;
        BiomeList.RemoveAt(indexToRemoveAt);
        if (changeBorders)
        {

            mapView.OnBiomeChunksListChangedListener(false);
        }
    }

    /// <summary>
    /// Loads new BiomeChunks to BiomeList from both left and right parts.
    /// </summary>
    /// <param name="leftPart"></param>
    /// <param name="rightPart"></param>
    /// <param name="init"></param>
    private void LoadBiomes(int leftPart, int rightPart, bool init = false)
    {
        for (int i = 0; i < leftPart; i++)
        {
            LoadOuterBiome(WorldMapController.Direction.Left, init);
        }

        for (int i = 0; i < rightPart; i++)
        {
            LoadOuterBiome(WorldMapController.Direction.Right, init);
        }
    }

    /// <summary>
    /// Handles user's request to reload failed to load map fragment
    /// </summary>
    /// <param name="direction"></param>
    private void HandleReloadRequest(WorldMapController.Direction direction)
    {
        switch (direction)
        {
            case WorldMapController.Direction.Right:
            {
                if (BiomeList[BiomesLoadBehindCount + 1].currentState == BiomeChunk.State.Failed)
                {
                    //Debug.Log("Reloading");
                    LoadBiome(BiomesLoadBehindCount + 1, BiomeList[BiomesLoadBehindCount + 1].biomeId, false);
                }
                break;
            }
            case WorldMapController.Direction.Left:
            {
                if (BiomeList[BiomesLoadBehindCount - 1].currentState == BiomeChunk.State.Failed)
                {
                    //Debug.Log("Reloading");
                    LoadBiome(BiomesLoadBehindCount - 1, BiomeList[BiomesLoadBehindCount - 1].biomeId, false);
                }
                break;
            }
            default:
            {
                throw new Exception($"Unknown direction {direction}");
            }
        }
    }

    /// <summary>
    /// Loads biome to specified chunk rewriting all info about previous chunk at this index.
    /// </summary>
    /// <param name="indexToPlaceAt"></param>
    /// <param name="biomeId"></param>
    /// <param name="isInitPhase"></param>
    private void LoadBiome(int indexToPlaceAt, string biomeId, bool isInitPhase)
    {
        BiomeChunk biomeChunk = BiomeList[indexToPlaceAt];
        biomeChunk.biomeId = biomeId;
        GameManager.ResourceLoader.LoadAssetAsync<GameObject>(ResourceLoader.GetBiomeMapAssetName(biomeId), _onLoadComplete);
        biomeChunk.currentState = BiomeChunk.State.Loading;

        GameManager.ResourceLoader.GetAssetLoadSizeAsync(ResourceLoader.GetBiomeMapAssetName(biomeId), result => biomeChunk.loadSize = result);

        void _onLoadComplete(bool success, GameObject biomeData)
        {

            //#if UNITY_EDITOR
            //                // TODO: remove this (This is for debug only)
            //success = Random.value > 0.5f;
            //#endif
            if (success)
            {
                GameObject loadedBiome = biomeData;
                GameManager.StartSceneCoroutine(_waitForAbilityToInitialize(loadedBiome));
            }
            else
            {
                Debug.Log("Failed to load " + Random.value);
                //#if UNITY_EDITOR
                //                //TODO: remove unload. Added just for testing
                //GameManager.ResourceLoader.UnloadAsset<GameObject>(ResourceLoader.GetBiomeMapAssetName(biomeId));
                //#endif

                biomeChunk.currentState = BiomeChunk.State.Failed;

                if (!isInitPhase)
                {
                    mapView.OnFailedToLoadBiome(biomeChunk);
                }
            }
        }

        IEnumerator _waitForAbilityToInitialize(GameObject loadedBiome)
        {
            //yield return new WaitForEndOfFrame();

            // Make sure that new content won't be added while user is dragging scroll.
            // Scroll view can't handle this case correctly.
            while (IsContentDragged || isPaused || !CanLoad)
            {
                yield return new WaitForEndOfFrame();
            }

            // Make sure that loadedBiome is still valid ( was not unloaded).
            if (BiomeList.Find((chunk => chunk == biomeChunk)) == null)
            {
                yield break;
            }

            loadedBiome = GameObject.Instantiate(loadedBiome, ContentTransform);

            biomeChunk.biome = loadedBiome;
            biomeChunk.currentState = BiomeChunk.State.Initializing;

            // During Init phase  BiomeListChanged event will be called in InitBiomeList when all biomes will be loaded.
            if (!isInitPhase)
            {
                mapView.OnBiomeChunksListChangedListener(false);
            }

        }
    }

    /// <summary>
    /// Asynchronously loads new biome and adds it to BiomeList.
    /// Triggers BiomeListChanged event on completion.
    /// </summary>
    /// <param name="direction">The direction to which load a biome.</param>
    /// <param name="isInitPhase">Indicates map controller init phase. In initPhase the BiomeListChanged event is triggered only once.</param>
    private void LoadOuterBiome(WorldMapController.Direction direction, bool isInitPhase = false)
    {
        int indexToPlaceAt = -1;
        string biomeId = null;
        BiomeChunk biomeChunk = new BiomeChunk();

        if (isInitPhase && BiomeList.Count == 0)
        {
            indexToPlaceAt = 0;
            biomeId = StartBiomeId;
        }
        else
        {
            switch (direction)
            {
                case WorldMapController.Direction.Left:
                    indexToPlaceAt = 0;
                    biomeId = GameManager.Settings.GetPrevBiomeId((BiomeList[0].biomeId));
                    break;
                case WorldMapController.Direction.Right:
                    indexToPlaceAt = BiomeList.Count;
                    biomeId = GameManager.Settings.GetNextBiomeId(BiomeList[BiomeList.Count - 1].biomeId);
                    break;
            }
        }

        BiomeList.Insert(indexToPlaceAt, biomeChunk);

        if (biomeId == null)
        {
            return;
        }

        LoadBiome(indexToPlaceAt, biomeId, isInitPhase);
    }


    /// <summary>
    /// When current biome changes loads/unloads biomes to maintain forward and backward biomes count from current biome
    /// </summary>
    private void OnCurrentBiomeChangedListener(BiomeChunk currentBiomeChunk)
    {
        CurrentBiomeChunk = currentBiomeChunk;

        int currentBiomeIndex = -1;
        for (int i = 0; i < BiomeList.Count; i++)
        {
            if (BiomeList[i].biome != null && BiomeList[i].biome == CurrentBiomeChunk.biome)
            {
                currentBiomeIndex = i;
            }
        }

        int leftLoadCount = BiomesLoadBehindCount - (currentBiomeIndex);
        int rightLoadCount = BiomesLoadAheadCount - (BiomeList.Count - 1 - currentBiomeIndex);
        UnloadBiomes(rightLoadCount, leftLoadCount, true);
        LoadBiomes(leftLoadCount, rightLoadCount);
    }

#region Cursor logic

    /// <summary>
    /// Gets current position of the cursor.
    /// </summary>
    /// <returns>Last level played during current session.
    /// If no level was played returns next level after last completed basic level.
    /// If no levels were completed returns first game level.
    /// If user completed all levels returns last game level.</returns>
    public static LevelIndex GetCurrentCursorPosition()
    {
        var currentCursorLevel = GameManager.Instance.LastLevelPlayed;

        if (currentCursorLevel == null)
        {
            currentCursorLevel = PlayerProfileHelperTools.GetLastCompletedLevel(GameManager.ProfileManager.profile);

            if (currentCursorLevel != null)
            {
                var nextLevel = GameManager.Settings.GetNextBasicLevelIndex(currentCursorLevel);

                if (nextLevel != null)
                {
                    currentCursorLevel = nextLevel;
                }
            }
        }

        if (currentCursorLevel == null)
        {
            currentCursorLevel = new LevelIndex(GameManager.Settings.GetBiomeId(0), 1);
        }

        return currentCursorLevel;

    }

#endregion
}