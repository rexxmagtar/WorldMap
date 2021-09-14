using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.pigsels.tools;
//using TMPro;
using UnityEngine;
//using UnityEngine.Assertions;
using UnityEngine.UI;

namespace com.pigsels.BubbleTrouble
{
    /// <summary>
    /// Is used as a view of world map MVC.
    /// Handles all visual representation of map
    /// Controls user's scroll on the map
    /// </summary>
    public class MapView
    {
#region events

        /// <summary>
        /// This event is fired when visual biome changes.
        /// </summary>
        public event BiomeChangedHandler OnBiomeChanged;

        public delegate void BiomeChangedHandler(BiomeChunk biomeChunk);

        /// <summary>
        /// This event is fired when reload request was sent by the user
        /// </summary>
        public event ReloadRequestHandler OnReloadRequest;

        public delegate void ReloadRequestHandler(WorldMapController.Direction direction);

#endregion events


#region class fields

        private Canvas canvas;

        private GameObject PreloaderLoading;
        private WorldMapPreloader WorldMapPreloader;

        private Vector2 preloaderStartAnchoredPosition;
        private Vector2 preloaderFinishAnchoredPosition;

        private GameObject PreloaderComplete;
        private AnimationCurve PreloaderCompleteAnimationCurve;
        private Coroutine LoadCompleteCoroutine;
        private bool preloaderInitialized = false;

        private int BiomesLoadBehindCount;
        private int BiomesLoadAheadCount;
        private int StartLevelId;

        private ScrollRect ScrollRect;
        private GameObject ScrollContent;
        private RectTransform ContentTransform;
        private RectTransform canvasRectTransform;
        private Camera CachedCamera;
        private float CameraHeight;
        private float CameraWidth;

        public List<BiomeChunk> BiomeList;
        private BiomeChunk CurrentBiomeChunk;

        private AnimationCurve mapScrollSwipeCurve;

        private bool isPaused = false;

        /// <summary>
        /// Space in pixels from nearest screen borders.
        /// </summary>
        private const float preloaderEdgeSpacing = 36;

        /// <summary>
        /// Distance user needs to swipe in one touch to trigger reloading of failed to load map fragment.
        /// </summary>
        private float DistanceToTriggerReload;

#endregion class fields


        public MapView(WorldMapSceneManager worldMapSceneManager)
        {
            canvas = worldMapSceneManager.canvas;
            BiomesLoadBehindCount = worldMapSceneManager.BiomesLoadBehindCount;
            BiomesLoadAheadCount = worldMapSceneManager.BiomesLoadAheadCount;

            PreloaderLoading = worldMapSceneManager.Preloader;
            PreloaderComplete = worldMapSceneManager.PreloaderComplete;
            PreloaderCompleteAnimationCurve = worldMapSceneManager.preloaderCompleteCurve;

            mapScrollSwipeCurve = worldMapSceneManager.mapScrollSwipeCurve;

            LevelIndex currentCursorPosition = MapModelController.GetCurrentCursorPosition();
            StartLevelId = currentCursorPosition.levelId;

            CachedCamera = Camera.main;
            CameraHeight = 2f * CachedCamera.orthographicSize;
            CameraWidth = CameraHeight * CachedCamera.aspect;
            DistanceToTriggerReload = CameraWidth / 100;

            ScrollRect = canvas.GetComponentInChildren<ScrollRect>();
            ScrollContent = ScrollRect.content.gameObject;
            ContentTransform = ScrollContent.GetComponent<RectTransform>();
            canvasRectTransform = canvas.GetComponent<RectTransform>();

            PreloaderComplete.GetComponentInChildren<Button>().onClick.AddListener(CompleteButtonPressed);

            WorldMapPreloader = PreloaderLoading.GetComponent<WorldMapPreloader>();

            Vector2 preloaderSize = PreloaderLoading.GetComponent<RectTransform>().sizeDelta;
            float halfCanvasWidth = canvasRectTransform.sizeDelta.x * .5f;
            float halfCanvasHeight = canvasRectTransform.sizeDelta.y * .5f;

            preloaderStartAnchoredPosition = new Vector2(-1 * (-halfCanvasWidth - preloaderSize.x * .5f + (preloaderSize.x + preloaderEdgeSpacing) * 0), -halfCanvasHeight + preloaderEdgeSpacing + preloaderSize.y / 2);
            preloaderFinishAnchoredPosition = new Vector2(1 * (halfCanvasWidth + preloaderSize.x * .5f - (preloaderSize.x + preloaderEdgeSpacing) * 1), -halfCanvasHeight + preloaderEdgeSpacing + preloaderSize.y / 2);

        }

        public void SetPause(bool pause)
        {
            isPaused = pause;
            ScrollRect.horizontal = !pause;
        }

        public void Update()
        {
            if (isPaused)
            {
                return;
            }

            if (!IsContentDragged && VisualBiomeChanged())
            {

                CurrentBiomeChunk = GetCurrentBiomeChunk();
                //Debug.Log($"Biome changed, current biome: {CurrentBiomeChunk.Biome}");
                OnBiomeChanged?.Invoke(CurrentBiomeChunk);
            }

            UpdateMapsActiveStatuses();

            // TODO: comment here what this code block does and what for (what problem it solves?)
            if (BiomeList[BiomesLoadBehindCount - 1].currentState == BiomeChunk.State.Loading ||
                BiomeList[BiomesLoadBehindCount + 1].currentState == BiomeChunk.State.Loading)
            {
                ScrollRect.movementType = ScrollRect.MovementType.Clamped;
            }
            else
            {
                ScrollRect.movementType = ScrollRect.MovementType.Elastic;
            }

            WorldMapController.Direction reloadRequestDirection = GetReloadRequestSwipe();

            //Debug.Log("Content right border: " + GetBorderPosition(WorldMapController.Direction.Right));
            //Debug.Log("Camera right border: " + canvasRectTransform.sizeDelta.x * 0.5f);
            //Debug.Log("Diff: " +Math.Abs(GetBorderPosition(WorldMapController.Direction.Right) - canvasRectTransform.sizeDelta.x * 0.5f));

            // Check if user has reached scroll area border and a next biome is still loading.
            // In this case don't save scroll velocity after border is expanded we stop the scroll completely.
            if (WorldMapPreloader.IsVisible() &&
                (Math.Abs(canvasRectTransform.sizeDelta.x * 0.5f - GetBorderPosition(WorldMapController.Direction.Right)) < 0.01f ||
                 Math.Abs(-canvasRectTransform.sizeDelta.x * 0.5f - GetBorderPosition(WorldMapController.Direction.Left)) < 0.01f))
            {
                //Debug.Log("Stopping scroll");
                ScrollRect.velocity = Vector2.zero;
            }

            if (!IsContentDragged && reloadRequestDirection != WorldMapController.Direction.None)
            {
                OnReloadRequest?.Invoke(reloadRequestDirection);
            }

            ActualizePreloaderLoading();
        }

        /// <summary>
        /// Handles clicks on level buttons of biomes.
        /// Displays level start dialog and loads game level if confirmed.
        /// </summary>
        /// <param name="levelIndex"></param>
        /// <param name="starsEarned">Number of stars already earned on this level (0 if not played yet).</param>
        public void OnLevelButtonClicked(LevelIndex levelIndex, int starsEarned)
        {
            DialogHandle dlgHandle = GameManager.UIManager.ShowLevelStartDialog(levelIndex, starsEarned);

            dlgHandle.OnButtonPressed += (buttonId, dialogHandle) =>
            {

                dialogHandle.CloseDialog();

                if (buttonId == UIManager.ButtonOkId)
                {
                    GameManager.Instance.LoadLevelScene(levelIndex, (success, index) =>
                    {
                        // TODO: handle failed loads
                    });
                }
            };
        }

        /// <summary>
        /// Checks for each Biome if it's in users view range, and depending on this activate/deactivate the biome.
        /// </summary>
        private void UpdateMapsActiveStatuses()
        {
            for (int i = 0; i < BiomeList.Count; i++)
            {
                if (BiomeList[i].currentState == BiomeChunk.State.Ready)
                {
                    if (Math.Abs(BiomeList[i].biome.transform.position.x -
                                 CachedCamera.gameObject.transform.position.x) <=
                        BiomeList[i].bounds.size.x / 2 + CameraWidth / 2)
                    {
                        BiomeList[i].biomeMap.Activate();
                    }
                    else
                    {
                        BiomeList[i].biomeMap.Deactivate();
                    }
                }
            }
        }

        private bool IsContentDragged
        {
            get
            {
                return Input.GetMouseButton(0);
            }
        }

        /// <summary>
        /// When biomes list changes, initializes uninitialized biomes, and refresh biomes position.
        /// </summary>
        /// <param name="isInitPhase"></param>
        public void OnBiomeChunksListChangedListener(bool isInitPhase)
        {
            // initializing biomes and calculating positions
            for (int i = 0; i < BiomeList.Count; i++)
            {
                if (BiomeList[i].currentState == BiomeChunk.State.Initializing)
                {
                    InitializeBiomeChunk(BiomeList[i]);
                    BiomeList[i].currentState = BiomeChunk.State.Ready;
                }
            }

            if (isInitPhase)
            {
                CurrentBiomeChunk = BiomeList[BiomesLoadBehindCount];

                var pos = CurrentBiomeChunk.biome.transform.position;
                pos.x = pos.x + CachedCamera.transform.position.x - CurrentBiomeChunk.biomeMap.GetLevelButtonPosition(StartLevelId).x;
                CurrentBiomeChunk.biome.transform.position = pos;
            }

            for (int i = 0; i < BiomeList.Count; i++)
            {
                if (BiomeList[i].currentState == BiomeChunk.State.Ready)
                {
                    // TODO: @3eka: this line sometimes throwns an exception when pressing EXIT button (exit to splash):
                    /*
                        MissingReferenceException: The object of type 'GameObject' has been destroyed but you are still trying to access it.
                        Your script should either check if it is null or you should not destroy the object.
                        com.pigsels.BubbleTrouble.OnBiomeChunksListChangedListener (System.Boolean isInitPhase) (at Assets/Scripts/WorldMap/cs:659)
                        com.pigsels.BubbleTrouble.UnloadBiome (com.pigsels.BubbleTrouble.WorldMapController+Direction direction, System.Boolean changeBorders) (at Assets/Scripts/WorldMap/cs:399)
                        com.pigsels.BubbleTrouble.UnloadBiomes (System.Int32 leftPart, System.Int32 rightPart, System.Boolean changeBorders) (at Assets/Scripts/WorldMap/cs:353)
                        com.pigsels.BubbleTrouble.OnCurrentBiomeChangedListener () (at Assets/Scripts/WorldMap/cs:590)
                        com.pigsels.BubbleTrouble.Update () (at Assets/Scripts/WorldMap/cs:201)
                        com.pigsels.BubbleTrouble.WorldMapSceneManager.Update () (at Assets/Scripts/SceneManagers/WorldMapSceneManager.cs:62)
                     */
                    BiomeList[i].biome.transform.position = GetBiomePosition(i);
                }
            }

            //check if need to show "complete to load" message. Also checks that this listener was invoked by load complete event (not by biome unload operation).
            if (WorldMapPreloader.IsVisible() && BiomeList.Count == BiomesLoadAheadCount + BiomesLoadBehindCount + 1)
            {
                if (BiomeList[BiomesLoadBehindCount + 1].currentState == BiomeChunk.State.Ready && GetPreloaderSide() == WorldMapController.Direction.Right)
                {
                    if (LoadCompleteCoroutine != null)
                    {
                        GameManager.StopSceneCoroutine(LoadCompleteCoroutine);
                    }

                    LoadCompleteCoroutine = GameManager.StartSceneCoroutine(ShowPreloaderComplete(WorldMapController.Direction.Right));
                }
                if (BiomeList[BiomesLoadBehindCount - 1].currentState == BiomeChunk.State.Ready && GetPreloaderSide() == WorldMapController.Direction.Left)
                {
                    if (LoadCompleteCoroutine != null)
                    {
                        GameManager.StopSceneCoroutine(LoadCompleteCoroutine);
                    }

                    LoadCompleteCoroutine = GameManager.StartSceneCoroutine(ShowPreloaderComplete(WorldMapController.Direction.Left));
                }
            }

            // ScrollRect ScrollRect = ScrollContent.transform.parent.parent.gameObject.GetComponent<ScrollRect>();

            //Debug.Log(">>>" + ScrollRect.velocity);
            //Vector2 prevVelocity = ScrollRect.velocity;
            //ScrollRect.inertia = false;
            //float elasticity = ScrollRect.elasticity;
            //ScrollRect.enabled = false;

            ActualizeContentBorders();
        }

        /// <summary>
        /// Tries to get user's reload request. Returns None if no request had been received.
        /// </summary>
        /// <returns></returns>
        private WorldMapController.Direction GetReloadRequestSwipe()
        {
            if ((CachedCamera.transform.position.x + CameraWidth / 2) - (CurrentBiomeChunk.biome.transform.position.x + CurrentBiomeChunk.bounds.size.x / 2) > DistanceToTriggerReload)
            {
                return WorldMapController.Direction.Right;
            }

            if ((CurrentBiomeChunk.biome.transform.position.x - CurrentBiomeChunk.bounds.size.x / 2) - (CachedCamera.transform.position.x - CameraWidth / 2) > DistanceToTriggerReload)
            {
                return WorldMapController.Direction.Left;
            }

            return WorldMapController.Direction.None;

        }

        /// <summary>
        /// Changes scroll's content borders to fit loaded biomes chain
        /// </summary>
        private void ActualizeContentBorders()
        {
            int minWidthIndex = BiomesLoadBehindCount;
            int maxWidthIndex = BiomesLoadBehindCount;

            while (minWidthIndex > 0)
            {
                if (BiomeList[minWidthIndex - 1].currentState != BiomeChunk.State.Ready) break;
                minWidthIndex--;
            }

            while (maxWidthIndex < BiomeList.Count - 1)
            {
                if (BiomeList[maxWidthIndex + 1].currentState != BiomeChunk.State.Ready) break;
                maxWidthIndex++;
            }

            SetContentBorder(BiomeList[maxWidthIndex].biome.transform.position.x + BiomeList[maxWidthIndex].bounds.size.x / 2, WorldMapController.Direction.Right);
            SetContentBorder(BiomeList[minWidthIndex].biome.transform.position.x - BiomeList[minWidthIndex].bounds.size.x / 2, WorldMapController.Direction.Left);

            //Debug.Log($"Time to transform.parent: {finishTime}");
        }

        /// <summary>
        /// Checks if current biome is to far away from users view point (camera center)
        /// </summary>
        /// <returns></returns>
        private bool VisualBiomeChanged()
        {
            Vector3 worldBiomePosition = CurrentBiomeChunk.biome.transform.position;
            float cameraX = CachedCamera.transform.position.x;
            return (worldBiomePosition.x + CurrentBiomeChunk.bounds.size.x / 2 < cameraX ||
                    worldBiomePosition.x - CurrentBiomeChunk.bounds.size.x / 2 > cameraX);
        }

        /// <summary>
        /// Sets content specified border to specified position on X-Axis.
        /// </summary>
        /// <param name="x">X-Axis coordinate to set to border</param>
        /// <param name="side">Border to change position of</param>
        public void SetContentBorder(float x, WorldMapController.Direction side)
        {
            foreach (var t in BiomeList)
            {
                if (t.biome != null)
                {
                    t.biome.transform.SetParent(null, true);
                }
            }
            Vector2 newOffset = WorldToUISpace(ScrollContent.transform.parent.GetComponent<RectTransform>(), new Vector2(x, 0));

            if (side == WorldMapController.Direction.Left)
            {
                ContentTransform.offsetMin = new Vector2(newOffset.x, ContentTransform.offsetMin.y);
            }
            else
            {
                ContentTransform.offsetMax = new Vector2(newOffset.x, ContentTransform.offsetMax.y);
            }

            foreach (var t in BiomeList)
            {
                if (t.biome != null)
                {
                    t.biome.transform.SetParent(ContentTransform, true);
                }
            }
        }

        /// <summary>
        /// Return actual position of right or left border of content container.
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        private float GetBorderPosition(WorldMapController.Direction direction)
        {
            return (direction == WorldMapController.Direction.Left) ? ContentTransform.offsetMin.x : ContentTransform.offsetMax.x;
        }

        /// <summary>
        ///Initializes biomeChunk (Sets properties, initializes nested objects).
        /// </summary>
        /// <param name="biomeChunk"></param>
        private void InitializeBiomeChunk(BiomeChunk biomeChunk)
        {
            ResizeBiome(biomeChunk.biome);

            biomeChunk.bounds = biomeChunk.biome.GetBounds();

            //Check if it's the first biome. First biome has special bounds calculations cause of left stab
            if (GameManager.Settings.GetBiomeIndex(biomeChunk.biomeId) == 0)
            {
                Bounds biomeBounds = biomeChunk.bounds;

                float excludeWidth = biomeChunk.biome.gameObject.transform.Find("stub").gameObject.GetBounds().size.x;

                biomeBounds.SetMinMax(biomeBounds.min + Vector3.right * excludeWidth, biomeBounds.max);

                biomeChunk.bounds = biomeBounds;
            }

            SuperPivot.API.SetPivot(biomeChunk.biome.transform, biomeChunk.bounds.center);
            biomeChunk.biomeMap = biomeChunk.biome.GetComponent<BiomeMap>();
            biomeChunk.biomeMap.Init(this, biomeChunk.biomeId);
        }

        /// <summary>
        /// Gets biomeChunk of closest to user biome
        /// </summary>
        /// <returns></returns>
        private BiomeChunk GetCurrentBiomeChunk()
        {
            float minDistance = float.MaxValue;
            int minIndex = -1;
            for (int i = 0; i < BiomeList.Count; i++)
            {
                if (BiomeList[i].biome != null)
                {
                    float newDistance = ((Vector2)BiomeList[i].biome.transform.position - (Vector2)CachedCamera.transform.position).sqrMagnitude;
                    if (newDistance <= minDistance)
                    {
                        minIndex = i;
                        minDistance = newDistance;
                    }
                }
            }
            //Vector2 distanceVector = (Vector2)BiomeList[minIndex].Biome.transform.position - (Vector2)CachedCamera.transform.position;
            //if ((distanceVector).magnitude >
            //    BiomeList[minIndex].Bounds.size.x / 2 + CameraWidth / 2)
            //{
            //    Debug.Log("Detected no Biome ! " + BiomeList[minIndex].Biome.name);
            //    ContentTransform.position -= (Vector3)distanceVector;
            //}
            return BiomeList[minIndex];

        }

        /// <summary>
        /// Sets biome's size to fit screen height
        /// </summary>
        /// <param name="biome"></param>
        public void ResizeBiome(GameObject biome)
        {
            Vector3 scaleMultiplier = GetScreenFitScale(biome.GetBounds());

            biome.transform.localScale = new Vector3(
                biome.transform.localScale.x * scaleMultiplier.x,
                biome.transform.localScale.y * scaleMultiplier.y,
                biome.transform.localScale.z);
        }

        /// <summary>
        /// Get the scale(in world coordinates) to fit camera borders.
        /// </summary>
        /// <returns></returns>
        public Vector3 GetScreenFitScale(Bounds bounds)
        {
            float height = bounds.size.y;

            return (new Vector3(CameraHeight / height, CameraHeight / height));
        }

        /// <summary>
        /// Calculates the position at which a specific biome should be located
        /// </summary>
        /// <param name="biomeIndex"></param>
        /// <returns></returns>
        private Vector3 GetBiomePosition(int biomeIndex)
        {
            int currentBiomeIndex = BiomeList.FindIndex((BiomeChunk chunk) =>
            {
                return chunk == CurrentBiomeChunk;
            });
            float baseSign = Mathf.Sign(biomeIndex - currentBiomeIndex);
            int i = biomeIndex;
            float distance = 0;
            while ((i != currentBiomeIndex))
            {
                int newSign = (int)Mathf.Sign(i - currentBiomeIndex);
                if (BiomeList[i - newSign].currentState != BiomeChunk.State.Ready)
                {
                    distance += BiomeList[i].bounds.size.x / 2 + CameraWidth / 2;
                }
                distance += BiomeList[i].bounds.size.x / 2 + BiomeList[i - newSign].bounds.size.x / 2;
                i -= newSign;
            }

            return CurrentBiomeChunk.biome.transform.position + Vector3.right * (distance * baseSign);
        }

        /// <summary>
        /// Get's position in specified RectTransform's UI space
        /// </summary>
        /// <param name="parentRect"></param>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        private Vector3 WorldToUISpace(RectTransform parentRect, Vector3 worldPos)
        {
            //Convert the world for screen point so that it can be used with ScreenPointToLocalPointInRectangle function
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);


            //Convert the screenpoint to ui rectangle local point
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, Camera.main, out Vector2 movePos);
            //Convert the local point to world point
            return movePos;
        }


#region WorldMapPreloader handling

        /// <summary>
        /// Checks if WorldMapPreloader should be shown and starts to show it if needed
        /// </summary>
        public void ActualizePreloaderLoading()
        {
            if (BiomeList[BiomesLoadBehindCount - 1].currentState == BiomeChunk.State.Loading)
            {
                BiomeChunk watingForchunk = BiomeList[BiomesLoadBehindCount - 1];
                ActivatePreloader(WorldMapController.Direction.Left, watingForchunk);
            }
            else if (BiomeList[BiomesLoadBehindCount + 1].currentState == BiomeChunk.State.Loading)
            {
                BiomeChunk watingForchunk = BiomeList[BiomesLoadBehindCount + 1];
                ActivatePreloader(WorldMapController.Direction.Right, watingForchunk);
            }
            else
            {
                if (WorldMapPreloader.IsVisible())
                {
                    WorldMapPreloader.Hide(false);
                }
                preloaderInitialized = false;
            }
        }

        private void ActivatePreloader(WorldMapController.Direction direction, BiomeChunk waitingChunk)
        {
            float border = Mathf.Clamp(direction == WorldMapController.Direction.Left ? -GetBorderPosition(direction) : GetBorderPosition(direction), 0, float.MaxValue);
            float canvasWidth = canvasRectTransform.sizeDelta.x;
            float preloaderShowDistance = canvasWidth * .5f;

            //Debug.Log("Check borders "+ (border - canvasWidth * .5f));
            if (border - canvasWidth * .5f > preloaderShowDistance)
            {
                if (WorldMapPreloader.IsVisible())
                {
                    //Debug.Log("Hiding WorldMapPreloader");
                    WorldMapPreloader.Hide(waitingChunk.currentState == BiomeChunk.State.Loading);
                    preloaderInitialized = false;
                }

                return;
            }

            float result = waitingChunk.loadSize;

            if (!preloaderInitialized && waitingChunk.currentState == BiomeChunk.State.Loading && result > 0)
            {
                //This is made to insure that preloader showed previously has been stopped and deinited. Can bre refactored later if needed.
                if (PreloaderLoading.activeSelf)
                {
                    PreloaderLoading.SetActive(false);
                }

                preloaderInitialized = true;
                if (direction == WorldMapController.Direction.Left)
                {
                    FlipOnX(WorldMapPreloader.gameObject, true);
                }
                else
                {
                    FlipOnX(WorldMapPreloader.gameObject, false);
                }

                //Debug.Log("Showing WorldMapPreloader");

                if (direction == WorldMapController.Direction.Left)
                {
                    WorldMapPreloader.Show(new Vector2(-preloaderStartAnchoredPosition.x, preloaderStartAnchoredPosition.y), new Vector2(-preloaderFinishAnchoredPosition.x, preloaderFinishAnchoredPosition.y));
                }
                else
                {
                    WorldMapPreloader.Show(preloaderStartAnchoredPosition, preloaderFinishAnchoredPosition);
                }

                WorldMapPreloader.SetLoadingSize((result));

            }

            if (preloaderInitialized)
            {
                WorldMapPreloader.SetLoadingProgress(GameManager.ResourceLoader.GetAssetLoadProgress<GameObject>(ResourceLoader.GetBiomeMapAssetName(waitingChunk.biomeId)));
            }
        }

        /// <summary>
        /// Gets side of current WorldMapPreloader position.
        /// </summary>
        /// <returns></returns>
        private WorldMapController.Direction GetPreloaderSide()
        {
            Debug.Assert(WorldMapPreloader.IsVisible(), "Inactive WorldMapPreloader has no side.");

            return ((RectTransform)PreloaderLoading.transform).anchoredPosition.x < 0 ? WorldMapController.Direction.Left : WorldMapController.Direction.Right;
        }

        private WorldMapController.Direction GetPreloaderCompletedSide()
        {
            Debug.Assert(IsCompletePreloaderVisible(), "Inactive WorldMapPreloader has no side.");

            return ((RectTransform)PreloaderComplete.transform).anchoredPosition.x < 0 ? WorldMapController.Direction.Left : WorldMapController.Direction.Right;
        }

        /// <summary>
        /// Starts to show preloaderComplete
        /// </summary>
        /// <param name="side"></param>
        /// <returns></returns>
        private IEnumerator ShowPreloaderComplete(WorldMapController.Direction side)
        {
            //Debug.Log("Started finish show");
            Vector2 preloaderSize = PreloaderComplete.GetComponent<RectTransform>().sizeDelta;
            float preloaderEdgeSpacing = 36; // px
            float halfCanvasWidth = canvasRectTransform.sizeDelta.x * .5f;
            float halfCanvasHeight = canvasRectTransform.sizeDelta.y * .5f;

            PreloaderComplete.GetComponentInChildren<Button>().interactable = true;

            Vector2 newPos;

            switch (side)
            {
                case WorldMapController.Direction.Left:
                    newPos = new Vector2(-halfCanvasWidth + preloaderSize.x * .5f + preloaderEdgeSpacing, -halfCanvasHeight + preloaderEdgeSpacing + preloaderSize.y / 2);
                    FlipOnX(PreloaderComplete, true, "Arrow");
                    break;
                case WorldMapController.Direction.Right:
                    newPos = new Vector2(halfCanvasWidth - preloaderSize.x * .5f - preloaderEdgeSpacing, -halfCanvasHeight + preloaderEdgeSpacing + preloaderSize.y / 2);
                    FlipOnX(PreloaderComplete, false, "Arrow");
                    break;
                default:
                    throw new Exception("The side should be Left or Right.");
            }

            ((RectTransform)PreloaderComplete.transform).anchoredPosition = newPos;

            // Should be reactivated only after full initialization,
            // because attached animation script needs fresh info already OnAwake().
            SetPreloaderCompleteVisibility(true);

            float distancePassedFromPreloaderAppeared = 0;

            GameObject biomeToTrackDistanceFrom = CurrentBiomeChunk.biome;

            float startX = biomeToTrackDistanceFrom.transform.position.x;

            // Distance for different animation for different sides is opposite,
            // so we want that scrolling to the just loaded biome has always positive speed.
            float disatnceCoef = side == WorldMapController.Direction.Left ? 1 : -1;

            // Calculalting distance from current camera center position to next biome
            // and previous biome according to loading side. So if we have just loaded
            // right biome than it is is "next" biome. And first left biome from cameras
            // current positions is "previous" biome. This distances are needed to normalize
            // passed distance differently according to scrolling side. So if we scroll
            // in opposite from next biome direction, the distance for WorldMapPreloader
            // to disappear will differ from the distance if we scrolled in right direction
            // (towards next biome).

            var nextBiome = BiomeList[BiomesLoadBehindCount + 1];
            var prevBiome = BiomeList[BiomesLoadBehindCount - 1];
            float nextBiomePosX = nextBiome.biome.transform.position.x;
            float prevBiomePosX = prevBiome.biome.transform.position.x;
            float cameraX = CachedCamera.transform.position.x;

            float distanceToNextBiome;
            float distanceToPreviousBiome;

            switch (side)
            {
                case WorldMapController.Direction.Left:
                    distanceToNextBiome = Math.Abs(prevBiomePosX + prevBiome.bounds.size.x / 2 - cameraX);
                    distanceToPreviousBiome = Math.Abs(nextBiomePosX - nextBiome.bounds.size.x / 2 - cameraX);
                    break;

                case WorldMapController.Direction.Right:
                    distanceToNextBiome = Math.Abs(nextBiomePosX - nextBiome.bounds.size.x / 2 - cameraX);
                    distanceToPreviousBiome = Math.Abs(prevBiomePosX + prevBiome.bounds.size.x / 2 - cameraX);
                    break;

                case WorldMapController.Direction.None:
                    distanceToNextBiome = Math.Abs(((side == WorldMapController.Direction.Left) ? prevBiomePosX : nextBiomePosX) - cameraX);
                    distanceToPreviousBiome = Math.Abs(((side != WorldMapController.Direction.Left) ? nextBiomePosX : prevBiomePosX) - cameraX);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }

            while (true)
            {
                yield return new WaitForEndOfFrame();

                distancePassedFromPreloaderAppeared += biomeToTrackDistanceFrom.transform.position.x - startX;
                startX = biomeToTrackDistanceFrom.transform.position.x;

                float distanceToAvaluate = disatnceCoef * distancePassedFromPreloaderAppeared;

                distanceToAvaluate /= (distanceToAvaluate > 0 ? distanceToNextBiome : distanceToPreviousBiome);

                float newAlpha = PreloaderCompleteAnimationCurve.Evaluate(distanceToAvaluate);

                //Debug.Log($"Evaluating s = { distanceToAvaluate}");

                if (newAlpha < 0)
                {
                    //Debug.Log("Finished finish show");
                    SetPreloaderCompleteVisibility(false);
                    yield break;
                }
                else
                {
                    PreloaderComplete.GetComponent<CanvasGroup>().alpha = newAlpha;
                }

                //Check loader gone too far away
                if (Math.Abs(biomeToTrackDistanceFrom.transform.position.x - GetCurrentBiomeChunk().biome.transform.position.x) > CameraWidth)
                {
                    //Debug.Log("Went too far away");

                    SetPreloaderCompleteVisibility(false);
                    yield break;
                }
            }
        }

        private void SetPreloaderCompleteVisibility(bool isVisible)
        {
            PreloaderComplete.gameObject.SetActive(isVisible);
        }

        private bool IsCompletePreloaderVisible()
        {
            return PreloaderComplete.gameObject.activeSelf;
        }

        /// <summary>
        /// Flips Gamobject on X - axis. Flips all child gameobjects.
        /// </summary>
        /// <param name="obj">gameobject to flip</param>
        /// <param name="flip">if true, flips to normal orientation, over-wise flips to inverse orientation </param>
        /// <param name="objectsNotToFlip">names of child object that must not be flipped</param>
        private void FlipOnX(GameObject obj, bool flip, params string[] objectsNotToFlip)
        {
            //Debug.Break();
            float flipCoef = flip ? -1 : 1;

            Vector3 newScale = obj.transform.localScale;

            newScale.x = flipCoef * Math.Abs(newScale.x);

            obj.transform.localScale = newScale;

            foreach (Transform child in obj.transform)
            {
                if (objectsNotToFlip.Count(name => name == child.name) == 0)
                {
                    newScale = child.transform.localScale;

                    newScale.x = flipCoef * Math.Abs(newScale.x);

                    child.localScale = newScale;
                }
            }
        }

        /// <summary>
        /// Handles loading fails
        /// </summary>
        /// <param name="biomeChunk">chunk of failed to load biome</param>
        public void OnFailedToLoadBiome(BiomeChunk biomeChunk)
        {
            if (WorldMapPreloader.IsVisible())
            {
                GameManager.UIManager.ShowOkDialog("Loading error",
                    "Failed to load map fragment" + biomeChunk.biomeId + "\nPlease, check your internet connection and try again");
            }
        }

        /// <summary>
        /// Handles click on preloaderComplete.
        /// Scrolls map to the next loaded biome so that a touchpoint of two biomes is scrolled to the center of the screen.
        /// </summary>
        public void CompleteButtonPressed()
        {
            //Debug.Log("Button clicked");

            BiomeChunk biomeTOTtraveltO = BiomeList[GetPreloaderCompletedSide() == WorldMapController.Direction.Left ? BiomesLoadBehindCount - 1 : BiomesLoadBehindCount + 1];

            PreloaderComplete.GetComponentInChildren<Button>().interactable = false;

            float positionToTravelTo;

            if (GetPreloaderCompletedSide() == WorldMapController.Direction.Left)
            {
                positionToTravelTo = biomeTOTtraveltO.biome.transform.position.x + biomeTOTtraveltO.bounds.size.x / 2;
            }
            else
            {
                positionToTravelTo = biomeTOTtraveltO.biome.transform.position.x - biomeTOTtraveltO.bounds.size.x / 2;
            }

            GameManager.StartSceneCoroutine(ScrollToPointUsingCurve(positionToTravelTo));
        }

        /// <summary>
        /// Scrolls map to the specified point using only physic formulas for calculation.
        /// </summary>
        /// <param name="finishPoint"></param>
        private void ScrollToPointUsingPhysic(float finishPoint)
        {
            float distance = finishPoint - CachedCamera.transform.position.x;
            float decRate = ScrollRect.decelerationRate;
            float v0;
            v0 = distance * (float)Math.Log(decRate, Math.E);

            //Debug.Log($"Velocity = {v0} for distance {distance}");
            ScrollRect.velocity = v0 * Vector2.right / canvasRectTransform.localScale.x;
        }

        /// <summary>
        /// Scrolls map to the specified point using normalized curve for translation.
        /// </summary>
        /// <param name="finishPoint">Point to scroll map to</param>
        /// <returns></returns>
        private IEnumerator ScrollToPointUsingCurve(float finishPoint)
        {
            //Debug.Log("Start to travel to point " + finishPoint);
            float startPosition = ContentTransform.position.x;
            float distanceToTravel = finishPoint - CachedCamera.transform.position.x;

            //Made to disable interactions with scroll.
            ScrollRect.viewport.GetComponent<Image>().raycastTarget = false;

            float t = 0;
            do
            {
                t += Time.deltaTime;
                ContentTransform.position = new Vector3((startPosition - distanceToTravel * mapScrollSwipeCurve.Evaluate(t)), ContentTransform.position.y, ContentTransform.position.z);
                yield return null;
            } while (t < 1f);

            //Debug.Log("Finished to travel at point " + ContentTransform.position.x);

            ScrollRect.viewport.GetComponent<Image>().raycastTarget = true;
        }

#endregion WorldMapPreloader handling
    }
}