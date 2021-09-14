using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animator for the rabbit of the world map preloader.
/// Translates and resizes the rabbit.
/// </summary>
public class WorldMapPreloaderAnimator : MonoBehaviour
{
    private Image preloaderImage;
    private float t = 0f; // timer
    private Vector2 baseScale;
    public AnimationCurve curveStress; // squeezing dynamics curve
    public AnimationCurve jumpCurve;
    private Vector3 basePos;
    private Camera mainCam;
    private float screenHeight;
    private Vector2 baseAnchoredPosition;

    void Awake()
    {
        basePos = transform.position;
        preloaderImage = GetComponent<Image>();
        mainCam = Camera.main;
        screenHeight = 2 * mainCam.orthographicSize;
    }


    void OnEnable()
    {
        //Debug.Log("On enable called");
        baseScale = gameObject.transform.localScale;
        basePos = gameObject.transform.position;
        baseAnchoredPosition = ((RectTransform)gameObject.transform).anchoredPosition;
        t = 0;
    }

    void OnDisable()
    {
        //Debug.Log("On disable called");
        gameObject.transform.localScale = baseScale;
        ((RectTransform)gameObject.transform).anchoredPosition = baseAnchoredPosition;
        t = 0;
    }

    void Update()
    {
        t += Time.deltaTime;

        if (Mathf.Abs(t) >= 1f)
        {
            t = 0;
        }

        var stress = curveStress.Evaluate(t);
        preloaderImage.transform.localScale = new Vector3(baseScale.x * (1.07f - stress * 0.07f), baseScale.y * (0.9f + (stress * 0.1f)), 1f);

        float jumpScale = screenHeight / 10.8f;
        preloaderImage.transform.position = new Vector3(preloaderImage.transform.position.x, basePos.y + jumpCurve.Evaluate(t) * jumpScale, 0);

    }
}