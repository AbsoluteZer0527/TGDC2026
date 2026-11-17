using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PigeonDelivery : MonoBehaviour
{
    [SerializeField] Transform flyingPigeon;
    [SerializeField] Transform perchedPigeon;
    [SerializeField] Transform pigeonStand;
    [SerializeField] SpriteRenderer letter;
    [SerializeField] Image openLetterView;
    [Min(0.1f), SerializeField] float flightSeconds = 2.6f;
    [Min(1), SerializeField] int flaps = 5;
    [Min(0f), SerializeField] float flapHeight = 0.36f;
    [Range(0f, 45f), SerializeField] float tiltDegrees = 14f;

    [SerializeField] Sprite letterFront;
    [SerializeField] GameObject bountyHeading;
    [SerializeField] GameObject bountyScroll;

    Sprite letterBack;
    HandRaiseAnimation hand;
    bool letterRaised;
    bool showingBack;

    Vector3 flightStart;
    Camera kitchenCamera;
    Vector3 authoredPerch, authoredStand, authoredLetter;
    float lastAspect;
    Quaternion flightRotation;
    Action resumeDialogue;
    GameManager GM => GameManager.I;

    public void Initialize(Camera camera, Action resume)
    {
        letterBack = openLetterView.sprite;
        ShowLetterFace(GM.LetterBackRevealed);
        hand = new HandRaiseAnimation(openLetterView.rectTransform);
        resumeDialogue = resume;
        kitchenCamera = camera;
        authoredPerch = perchedPigeon.position;
        authoredStand = pigeonStand.position;
        authoredLetter = letter.transform.TransformPoint(letter.sprite.bounds.center);
        FitPerchToView();
        flightStart = flyingPigeon.position;
        flightRotation = flyingPigeon.rotation;
        if (camera)
        {
            Vector3 start = camera.WorldToViewportPoint(flightStart);
            start.x = Mathf.Min(start.x, -0.15f);
            flightStart = camera.ViewportToWorldPoint(start);
            if (!camera.TryGetComponent<Physics2DRaycaster>(out _))
                camera.gameObject.AddComponent<Physics2DRaycaster>();
        }
        var target = letter.gameObject.AddComponent<PigeonLetter>();
        var hitbox = target.GetComponent<BoxCollider2D>();
        hitbox.offset = letter.sprite.bounds.center;
        hitbox.size = letter.sprite.bounds.size;
        target.Clicked += OpenLetter;
        Refresh();
    }

    void FitPerchToView()
    {
        if (!kitchenCamera || Mathf.Approximately(lastAspect, kitchenCamera.aspect)) return;
        lastAspect = kitchenCamera.aspect;
        Vector3 viewport = kitchenCamera.WorldToViewportPoint(authoredLetter);
        viewport.x = Mathf.Min(viewport.x, 0.90f);
        Vector3 shift = kitchenCamera.ViewportToWorldPoint(viewport) - authoredLetter;
        pigeonStand.position = authoredStand + shift;
        perchedPigeon.position = authoredPerch + shift;
    }

    public void Refresh()
    {
        var stage = GM.DeliveryStage;
        flyingPigeon.gameObject.SetActive(stage == PigeonDeliveryStage.Flying);
        perchedPigeon.gameObject.SetActive(stage == PigeonDeliveryStage.WaitingForLetter ||
            stage == PigeonDeliveryStage.Reading || stage == PigeonDeliveryStage.Complete);
        letter.gameObject.SetActive(stage == PigeonDeliveryStage.WaitingForLetter);
        if (stage == PigeonDeliveryStage.Reading)
            letterRaised = true;
        if (stage == PigeonDeliveryStage.Flying) PoseFlight(GM.PigeonFlightProgress);
    }

    public void CloseLetterForPlaytest()
    {
        letterRaised = false;
        Refresh();
    }

    void ShowLetterFace(bool back)
    {
        showingBack = back;
        openLetterView.sprite = back ? letterBack : letterFront;
        bountyHeading.SetActive(back);
        bountyScroll.SetActive(back);
    }

    void AnimateLetter()
    {
        bool changingFace = GM.LetterBackRevealed && !showingBack;
        if (changingFace && hand.IsLowered)
        {
            ShowLetterFace(true);
            changingFace = false;
        }
        hand.Animate(letterRaised && !changingFace);
    }

    void PoseFlight(float progress)
    {
        float beat = Mathf.Repeat(progress * flaps, 1f);
        float envelope = Mathf.SmoothStep(0f, 1f, (1f - progress) * flaps);
        Vector3 path = Vector3.Lerp(flightStart, perchedPigeon.position, progress);
        flyingPigeon.position = path + Vector3.up * (4f * beat * (1f - beat) * flapHeight * envelope);
        float pitch = (1f - 2f * beat) * tiltDegrees * envelope;
        flyingPigeon.rotation = flightRotation * Quaternion.Euler(0f, 0f, pitch);
    }

    void OpenLetter()
    {
        if (GM.OpenDeliveredLetter()) resumeDialogue();
    }

    void Update()
    {
        if (resumeDialogue == null) return;
        AnimateLetter();
        if (GM.DayOver) return;
        FitPerchToView();
        if (GM.DeliveryStage == PigeonDeliveryStage.Flying)
        {
            GM.AdvancePigeonFlight(Time.deltaTime / Mathf.Max(0.1f, flightSeconds));
            Refresh();
        }
        if (CurtainTransition.IsPlaying || Keyboard.current == null || !Keyboard.current.qKey.wasPressedThisFrame) return;
        if (GM.FinishReadingLetter())
        {
            letterRaised = false;
            resumeDialogue();
        }
        else if (GM.DeliveryStage == PigeonDeliveryStage.Complete)
        {
            letterRaised = !letterRaised;
        }
    }
}
