using System.Collections;
using System.Linq;
using Oculus.Interaction.HandGrab;
using TaskSystem;
using UnityEngine;

[RequireComponent(typeof(WaterSpillSetup))]
public class WaterSpill : MonoBehaviour
{
    [SerializeField] private HandGrabInteractable handGrabInteractable;
    [SerializeField] private SimObjectiveInteraction pickObjectiveInteraction;
    [SerializeField] private WaterPlantsTaskDriver taskDriver;
    [SerializeField] private WaterSpillSetup spillSetup;
    [SerializeField] private float pollInterval = 1f;
    [SerializeField] private float movementThreshold = 0.01f;
    [SerializeField] private float spillDuration = 1.1f;
    [SerializeField] private float tiltThreshold = 35f;
    [SerializeField] private bool completePickObjectiveOnGrab = true;
    [SerializeField] private bool keepPickObjectiveSyncedToHoldState = true;

    [Header("Debug")]
    [SerializeField] private bool logSpillState = true;
    [SerializeField] private bool logPickObjective = true;
    [SerializeField] private bool isGrabbed;
    [SerializeField] private bool isMoving;
    [SerializeField] private bool isTilted;
    [SerializeField] private bool isSpilling;

    private Vector3 previousPosition;
    private Coroutine spillRoutine;
    private bool wasGrabbedLastFrame;


    private void Awake()
    {
        if (handGrabInteractable == null)
        {
            handGrabInteractable = GetComponentInChildren<HandGrabInteractable>(true);
        }

        if (spillSetup == null)
        {
            spillSetup = GetComponent<WaterSpillSetup>();
        }

        if (spillSetup == null)
        {
            spillSetup = gameObject.AddComponent<WaterSpillSetup>();
        }

        if (pickObjectiveInteraction == null)
        {
            pickObjectiveInteraction = GetComponent<SimObjectiveInteraction>();
        }

        if (taskDriver == null)
        {
            taskDriver = FindFirstObjectByType<WaterPlantsTaskDriver>();
        }
    }

    private void Start()
    {
        spillSetup.EnsureSetup();

        if (logSpillState)
        {
            Debug.Log(
                $"[WaterSpill] Setup complete on {name}. Particles={(spillSetup.WaterParticles != null ? spillSetup.WaterParticles.name : "null")}.",
                this);
        }

        previousPosition = transform.position;
        InvokeRepeating(nameof(MovementPolling), pollInterval, pollInterval);
    }

    private void LateUpdate()
    {
        UpdateGrabState();

        if (!isSpilling || spillSetup.WaterParticles == null)
        {
            return;
        }

        spillSetup.AlignParticlesToExitPoint();
    }

    private void MovementPolling()
    {
        isGrabbed = IsCurrentlyGrabbed();

        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, previousPosition);
        isMoving = distanceMoved > movementThreshold;
        previousPosition = currentPosition;

        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);
        isTilted = tiltAngle > tiltThreshold;

        if (logSpillState)
        {
            Debug.Log(
                $"[WaterSpill] Poll {name}: grabbed={isGrabbed}, moving={isMoving}, tilted={isTilted}, distanceMoved={distanceMoved:F3}, tiltAngle={tiltAngle:F1}.",
                this);
        }

        if (isGrabbed && (isMoving || isTilted))
        {
            if (spillRoutine != null)
            {
                StopCoroutine(spillRoutine);
            }

            spillRoutine = StartCoroutine(WaterSpilling());
            return;
        }

        StopSpilling();
    }

    private IEnumerator WaterSpilling()
    {
        isSpilling = true;

        ParticleSystem waterParticles = spillSetup.WaterParticles;
        if (waterParticles != null)
        {
            spillSetup.AlignParticlesToExitPoint();

            if (logSpillState)
            {
                Debug.Log(
                    $"[WaterSpill] Spilling from {waterParticles.name} at {waterParticles.transform.position}.",
                    this);
            }

            if (!waterParticles.isPlaying)
            {
                waterParticles.Clear();
                waterParticles.Play();
            }
        }

        yield return new WaitForSeconds(spillDuration);
        StopSpilling();
    }

    private void StopSpilling()
    {
        isSpilling = false;

        if (logSpillState)
        {
            Debug.Log($"[WaterSpill] Stop spilling on {name}.", this);
        }

        ParticleSystem waterParticles = spillSetup != null ? spillSetup.WaterParticles : null;
        if (waterParticles != null && waterParticles.isPlaying)
        {
            waterParticles.Stop();
        }

        spillRoutine = null;
    }

    private void UpdateGrabState()
    {
        bool currentlyGrabbed = IsCurrentlyGrabbed();
        if (currentlyGrabbed && !wasGrabbedLastFrame)
        {
            HandleGrabStarted();
        }
        else if (!currentlyGrabbed && wasGrabbedLastFrame)
        {
            HandleGrabReleased();
        }

        isGrabbed = currentlyGrabbed;
        wasGrabbedLastFrame = currentlyGrabbed;
    }

    private bool IsCurrentlyGrabbed()
    {
        if (handGrabInteractable == null || handGrabInteractable.Interactors == null)
        {
            return false;
        }

        for (int i = 0; i < handGrabInteractable.Interactors.Count; i++)
        {
            HandGrabInteractor interactor = handGrabInteractable.Interactors.ElementAt(i) as HandGrabInteractor;
            if (interactor != null && interactor.IsGrabbing)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleGrabStarted()
    {
        if (keepPickObjectiveSyncedToHoldState)
        {
            bool setTrue = taskDriver != null
                ? taskDriver.SetWaterCanHeld(true)
                : pickObjectiveInteraction != null && pickObjectiveInteraction.SetObjectiveState(true);
            if (logPickObjective)
            {
                Debug.Log($"[WaterSpill] Grab started on {name}. Pick step sync {(setTrue ? "succeeded" : "was rejected")}.", this);
            }

            return;
        }

        if (!completePickObjectiveOnGrab)
        {
            return;
        }

        bool completed = taskDriver != null
            ? taskDriver.CompletePickStep()
            : pickObjectiveInteraction != null && pickObjectiveInteraction.CompleteObjective();
        if (logPickObjective)
        {
            Debug.Log($"[WaterSpill] Grab started on {name}. Pick step completion {(completed ? "succeeded" : "was rejected")}.", this);
        }
    }

    private void HandleGrabReleased()
    {
        if (!keepPickObjectiveSyncedToHoldState)
        {
            return;
        }

        bool setFalse = taskDriver != null
            ? taskDriver.SetWaterCanHeld(false)
            : pickObjectiveInteraction != null && pickObjectiveInteraction.SetObjectiveState(false);
        if (logPickObjective)
        {
            Debug.Log($"[WaterSpill] Grab released on {name}. Pick step release sync {(setFalse ? "succeeded" : "was rejected")}.", this);
        }
    }
}
