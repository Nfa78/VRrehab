using System.Collections;
using Oculus.Interaction.HandGrab;
using UnityEngine;

[RequireComponent(typeof(WaterSpillSetup))]
public class WaterSpill : MonoBehaviour
{
    [SerializeField] private HandGrabInteractable handGrabInteractable;
    [SerializeField] private WaterSpillSetup spillSetup;
    [SerializeField] private float pollInterval = 1f;
    [SerializeField] private float movementThreshold = 0.01f;
    [SerializeField] private float spillDuration = 1.1f;
    [SerializeField] private float tiltThreshold = 35f;

    [Header("Debug")]
    [SerializeField] private bool isGrabbed;
    [SerializeField] private bool isMoving;
    [SerializeField] private bool isTilted;
    [SerializeField] private bool isSpilling;

    private Vector3 previousPosition;
    private Coroutine spillRoutine;

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
    }

    private void Start()
    {
        spillSetup.EnsureSetup();
        previousPosition = transform.position;
        InvokeRepeating(nameof(MovementPolling), pollInterval, pollInterval);
    }

    private void LateUpdate()
    {
        if (!isSpilling || spillSetup.WaterParticles == null)
        {
            return;
        }

        spillSetup.AlignParticlesToExitPoint();
    }

    private void MovementPolling()
    {
        isGrabbed = handGrabInteractable != null &&
                    handGrabInteractable.Interactors != null &&
                    handGrabInteractable.Interactors.Count > 0;

        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, previousPosition);
        isMoving = distanceMoved > movementThreshold;
        previousPosition = currentPosition;

        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);
        isTilted = tiltAngle > tiltThreshold;

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

        ParticleSystem waterParticles = spillSetup != null ? spillSetup.WaterParticles : null;
        if (waterParticles != null && waterParticles.isPlaying)
        {
            waterParticles.Stop();
        }

        spillRoutine = null;
    }
}
