using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using Random = UnityEngine.Random;


namespace com.pigsels.BubbleTrouble
{
    /// <summary>
    /// Takes care of world map operations (including loading/unloading and scrolling).
    /// Controls MVC model which works with world map.
    /// </summary>
    public class WorldMapController : Controller
    {
        public enum Direction
        {
            Left,
            Right,
            None
        }

        private MapModelController mapModelController;
        private MapView mapView;

        public override IEnumerator Init(params object[] parameters)
        {
            yield return base.Init(parameters);

            WorldMapSceneManager worldMapSceneManager = (WorldMapSceneManager)sceneManager;

            mapView = new MapView(worldMapSceneManager);
            mapModelController = new MapModelController(worldMapSceneManager, mapView);

            yield return mapModelController.Init();
        }

        public override void Deinit()
        {
            mapModelController.Deinit();

            base.Deinit();
        }

        public override void Pause()
        {
            base.Pause();

            mapModelController.SetPause(true);
            mapView.SetPause(true);

        }

        public override void Resume()
        {
            base.Resume();
            mapModelController.SetPause(false);
            mapView.SetPause(false);
        }

        public void Update()
        {
            if (IsPaused)
            {
                return;
            }

            mapModelController.Update();
            mapView.Update();
        }
    }
}