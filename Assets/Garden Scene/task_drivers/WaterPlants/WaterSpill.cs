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
    [Tooltip("Minimum watering-can movement speed in metres per second required to keep pouring.")]
    [SerializeField] [Min(0f)] private float movementThreshold = 0.01f;
    [SerializeField] private float tiltThreshold = 35f;
    [Tooltip("How long the can must remain tilted before water starts. This filters brief accidental movements.")]
    [SerializeField] [Min(0f)] private float pourStartDelay = 0.15f;
    [Tooltip("The can stops pouring below the start angle minus this amount, preventing flicker from hand tremor.")]
    [SerializeField] [Range(0f, 30f)] private float tiltReleaseBuffer = 8f;
    [SerializeField] private bool completePickObjectiveOnGrab = true;
    [SerializeField] private bool keepPickObjectiveSyncedToHoldState = true;

    [Header("Debug")]
    [SerializeField] private bool logSpillState;
    [SerializeField] private bool logPickObjective;
    [SerializeField] private bool isGrabbed;
    [SerializeField] private bool isMoving;
    [SerializeField] private bool isTilted;
    [SerializeField] private bool isSpilling;

    public bool IsSpilling => isSpilling;

    private Vector3 previousPosition;
    private bool wasGrabbedLastFrame;
    private float tiltHeldSeconds;


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
    }

    private void Update()
    {
        UpdateGrabState();
        UpdatePourState();
    }

    private void LateUpdate()
    {
        if (!isSpilling || spillSetup.WaterParticles == null)
        {
            return;
        }

        spillSetup.AlignParticlesToExitPoint();
    }

    private void UpdatePourState()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, previousPosition);
        float movementSpeed = Time.deltaTime > 0.0001f ? distanceMoved / Time.deltaTime : 0f;
        isMoving = movementSpeed > movementThreshold;
        previousPosition = currentPosition;

        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);
        float releaseThreshold = Mathf.Max(0f, tiltThreshold - tiltReleaseBuffer);
        float requiredTilt = isSpilling ? releaseThreshold : tiltThreshold;
        isTilted = tiltAngle >= requiredTilt;

        if (!isGrabbed || !isTilted || !isMoving)
        {
            tiltHeldSeconds = 0f;
            StopSpilling();
            return;
        }

        if (isSpilling)
        {
            return;
        }

        tiltHeldSeconds += Time.deltaTime;
        if (tiltHeldSeconds >= pourStartDelay)
        {
            StartSpilling();
        }
    }

    private void StartSpilling()
    {
        isSpilling = true;

        ParticleSystem waterParticles = spillSetup.WaterParticles;
        if (waterParticles == null)
        {
            return;
        }

        spillSetup.AlignParticlesToExitPoint();
        if (logSpillState)
        {
            Debug.Log($"[WaterSpill] Started pouring from {waterParticles.name}.", this);
        }

        if (!waterParticles.isPlaying)
        {
            waterParticles.Clear();
            waterParticles.Play();
        }
    }

    private void StopSpilling()
    {
        if (!isSpilling)
        {
            return;
        }

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
    }

    public void ApplyDifficulty(float newPourStartDelay, float newTiltThreshold)
    {
        pourStartDelay = Mathf.Max(0f, newPourStartDelay);
        tiltThreshold = Mathf.Clamp(newTiltThreshold, 0f, 180f);
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
