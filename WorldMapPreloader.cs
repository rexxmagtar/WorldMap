using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.pigsels.BubbleTrouble;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dispays loading size and progress info.
/// Can show and hide (interpolates from start to finish point showing).
/// </summary>
public class WorldMapPreloader : MonoBehaviour
{
    /// <summary>
    /// The state of the preloader. Describes the current animation of the preloader.
    /// </summary>
    enum State
    {
        Visible,
        Hidden,
        Hiding
    }


    private State currentState = State.Hidden;

    /// <summary>
    /// Start anchored position of preloader
    /// </summary>
    private Vector2 startAnchoredPosition;

    /// <summary>
    /// Finish anchored position of preloader
    /// </summary>
    private Vector2 finishAnchoredPosition;

    private Slider preloaderSlider;
    private TextMeshProUGUI loadingSizeText;

    /// <summary>
    /// Curve controls speed of transition of preloader from start to finish point. 
    /// </summary>
    public AnimationCurve speedCurve;

    private float time = 0;

    private float distanceToTravel = 0;

    private WorldMapPreloaderAnimator mapPreloaderAnimator;


    private void Awake()
    {
        preloaderSlider = gameObject.GetComponentInChildren<Slider>();
        loadingSizeText = gameObject.transform.Find("SliderLoading").GetComponentInChildren<TextMeshProUGUI>();
        time = 0;
    }

    private void Update()
    {
        bool needToTravel = false; 

        //Debug.Log("anchored position: " + ((RectTransform)gameObject.transform).anchoredPosition);
        switch (currentState)
        {
            case State.Visible:
                if (time < 1)
                {
                    needToTravel = true;
                    time += Time.deltaTime;
                }

                break;

            case State.Hiding:
                if (time > 0)
                {
                    needToTravel = true;
                    time -= Time.deltaTime;
                }
                else
                {
                    Hide(false);
                }

                break;
        }

        if (needToTravel)
        {
            Vector2 travelVector = Vector2.right * distanceToTravel * speedCurve.Evaluate(time) * Math.Sign(finishAnchoredPosition.x - startAnchoredPosition.x);

            ((RectTransform)gameObject.transform).anchoredPosition = startAnchoredPosition + travelVector;
        }
    }

    /// <summary>
    /// Shows preoader. Placing it on start position, and interpolating to finish point. Interpolate speed is described by animation curve.
    /// </summary>
    /// <param name="startAnchoredPosition"></param>
    /// <param name="finishAnchoredPosition"></param>
    /// <param name="loadingSize"></param>
    public void Show(Vector2 startAnchoredPosition, Vector2 finishAnchoredPosition, float loadingSize = 0)
    {
        //Debug.Log("Show preloader");

        currentState = State.Visible;

        this.startAnchoredPosition = startAnchoredPosition;
        this.finishAnchoredPosition = finishAnchoredPosition;

        distanceToTravel = Math.Abs(startAnchoredPosition.x - finishAnchoredPosition.x);
        time = 0;
        //Debug.Log("Distance = " + distanceToTravel);

        ((RectTransform)gameObject.transform).anchoredPosition = startAnchoredPosition;

        gameObject.SetActive(true);
        loadingSizeText.text = (loadingSize).ToString("0.00") + "Mb";

    }

    public void SetLoadingProgress(float value)
    {
        preloaderSlider.value = value;
    }

    public void SetLoadingSize(float bytes)
    {
        loadingSizeText.text = (bytes / (1024 * 1024)).ToString("0.00") + "Mb";
    }

    /// <summary>
    /// Hides preloader by setting it to inactive state.
    /// </summary>
    /// <param name="transition">If true, preloader will move out of users view range first, and only then disappear.</param>
    public void Hide(bool transition)
    {
        //Debug.Log("Hide preloader");

        if (transition)
        {
            currentState = State.Hiding;
        }
        else
        {
            gameObject.SetActive(false);
            currentState = State.Hidden;
        }
    }

    public bool IsVisible()
    {
        return currentState == State.Visible;
    }
}