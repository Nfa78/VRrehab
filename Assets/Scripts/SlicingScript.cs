using EzySlice;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Samples;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Linq;


public class SlicingScript : MonoBehaviour
{
    public Transform startSlicePoint;
    public Transform endSlicePoint;
    public VelocityEstimator velocityEstimator; // To estimate the velocity of a GameObject on a certain number of frames
    public LayerMask sliceableLayer;
    public Material crossSectionMaterial; // Pretty self-explanatory
    public float cutForce = 50f;
    public AudioSource success;

    private int pieces = 1; // number of pieces the carrot has been sliced into
    private bool hasPlayed = false;
    public Image checkbox;
    public Sprite checkedBox;

    public AudioClip[] slicingSounds; // all the different possible slicing sfx
    private AudioSource audioSource;

    private PlayLocalizedAudio localizedAudio;
    public GameObject task02Text;
    public GameObject task03Text;

    // Time tracking
    private float sliceStartTime = 0f;
    private float sliceEndTime = 0f;
    private bool slicingInProgress = false;

    // Making it so that the cut doesn't happen as soon as the knife comes in contact with the carrot
    private Coroutine slicingCoroutine = null;
    private GameObject currentTarget = null;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        localizedAudio = FindAnyObjectByType<PlayLocalizedAudio>();
    }

    void FixedUpdate()
    {
        bool hasHit = Physics.Linecast(startSlicePoint.position, endSlicePoint.position, out RaycastHit hit, sliceableLayer);

        if (hasHit)
        {
            if (currentTarget != hit.transform.gameObject)
            {
                currentTarget = hit.transform.gameObject;

                // Start slicing coroutine with a delay
                if (slicingCoroutine == null)
                {
                    slicingCoroutine = StartCoroutine(DelayedSlice(currentTarget, hit));
                }
            }
        }
        else
        {
            currentTarget = null;

            if (slicingCoroutine != null)
            {
                StopCoroutine(slicingCoroutine);
                slicingCoroutine = null;
            }
        }
    }

    IEnumerator DelayedSlice(GameObject target, RaycastHit hitInfo)
    {
        float delay = 0.01f; // Time to wait before slicing to simulate resistance
        float elapsedTime = 0.0f; 

        while (elapsedTime < delay)
        {
            // Ensure we're still touching the same object
            if (!Physics.Linecast(startSlicePoint.position, endSlicePoint.position, out RaycastHit newHit, sliceableLayer) || newHit.transform.gameObject != target)
            {
                yield break; // Abort slicing if knife moved away
            }

            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Optional: Require knife to be moving fast enough to slice
        /*Vector3 velocity = velocityEstimator.GetVelocityEstimate();
        if (velocity.magnitude < 0.0f) // Minimum knife speed to slice
        {
            yield break; // Not moving fast enough
        }*/

        Slice(target);
        pieces++;

        if (!slicingInProgress)
        {
            sliceStartTime = Time.time;
            slicingInProgress = true;
        }

        if (pieces >= 15 && !hasPlayed)
        {
            sliceEndTime = Time.time;
            success.Play();
            checkbox.sprite = checkedBox;
            task02Text.SetActive(false);
            task03Text.SetActive(true);
            TallyPoints();
            StartCoroutine(PlayAudioWithDelay(1f));
            hasPlayed = true;
        }

        slicingCoroutine = null;
    }

    IEnumerator PlayAudioWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        localizedAudio?.PlayLocalizedClip(3);
    }

    // Function to slice the object
    public void Slice(GameObject target)
    {
        Vector3 velocity = velocityEstimator.GetVelocityEstimate();
        Vector3 planeNormal = Vector3.Cross(endSlicePoint.position - startSlicePoint.position, velocity);
        planeNormal.Normalize();
        SlicedHull hull = target.Slice(endSlicePoint.position, planeNormal);

        if (hull != null)
        {
            // Get grabbing interactor (if any)
            HandGrabInteractor grabbingHand = null;
            HandGrabInteractable originalInteractable = target.GetComponent<HandGrabInteractable>();

            if (originalInteractable != null)
            {
                grabbingHand = originalInteractable.Interactors.FirstOrDefault() as HandGrabInteractor;
            }

            GameObject upperHull = hull.CreateUpperHull(target, crossSectionMaterial);
            SetUpSlicedComponent(upperHull);
            upperHull.layer = target.layer;

            GameObject lowerHull = hull.CreateLowerHull(target, crossSectionMaterial);
            SetUpSlicedComponent(lowerHull);
            lowerHull.layer = target.layer;

            // Force release from all interactors before destroying the object
            if(grabbingHand != null)
                grabbingHand.ForceRelease();


            Destroy(target);

            // Reassign grab to lower hull
            /*if (grabbingHand != null)
            {
                HandGrabInteractable newInteractable = lowerHull.GetComponent<HandGrabInteractable>();
                if (newInteractable != null)
                {
                    grabbingHand.ForceSelect(newInteractable);
                }
            }*/
            /*if (grabbingHand != null)
            {
                Transform handTransform = grabbingHand.transform;

                float distToUpper = Vector3.Distance(upperHull.transform.position, handTransform.position);
                float distToLower = Vector3.Distance(lowerHull.transform.position, handTransform.position);

                GameObject closestPiece = distToUpper < distToLower ? upperHull : lowerHull;

                if (grabbingHand != null && closestPiece != null)
                {
                    HandGrabInteractable newInteractable = closestPiece.GetComponent<HandGrabInteractable>();
                    if (newInteractable != null)
                    {
                        StartCoroutine(ForceSelectNextFrame(grabbingHand, newInteractable));
                    }
                }

            }*/


            if (slicingSounds.Length > 0 && audioSource != null)
            {
                PlayRandomAudio();
            }
            else
            {
                Debug.LogWarning("AudioClips or AudioSource is missing!");
            }
        }
    }

    // Function to add a RigidBody and a collider to the pieces obtained from the slicing
    public void SetUpSlicedComponent(GameObject slicedObject)
    {
        Rigidbody rb = slicedObject.AddComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        rb.mass = 0.1f;
        rb.linearDamping = 1f;
        rb.angularDamping = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        MeshCollider col = slicedObject.AddComponent<MeshCollider>();
        col.convex = true;

        Grabbable grab = slicedObject.AddComponent<Grabbable>();
        grab.enabled = true;
        var gfield = typeof(Grabbable).GetField("_rigidbody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (gfield != null)
        {
            gfield.SetValue(grab, rb);
        }
        //grab.Initialize(rb);

        HandGrabInteractable handGrab = slicedObject.AddComponent<HandGrabInteractable>();
        handGrab.InjectRigidbody(rb);
        handGrab.InjectOptionalPointableElement(grab);


        slicedObject.tag = "Carrot";
        //slicedObject.AddComponent<ParticleSystemTracker>();
        slicedObject.layer = LayerMask.NameToLayer("CarrotPieces");

        RespawnOnDrop resp = slicedObject.AddComponent<RespawnOnDrop>();
        var field = typeof(RespawnOnDrop).GetField("_yThresholdForRespawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(resp, 0.3f);
        }
        else
        {
            Debug.LogWarning("Failed to set _yThresholdForRespawn via reflection.");
        }

        //SimpleHandDetection shd = slicedObject.AddComponent<SimpleHandDetection>();

        // Optional: add a force to the parts so that they move away from the knife after the slicing
        //rb.AddExplosionForce(cutForce, slicedObject.transform.position, 1);
    }

    private IEnumerator SetupRigidAfterFrame(GameObject go)
    {
        yield return null; // wait one frame so physics doesn't go boom

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        rb.mass = 0.1f;
        rb.linearDamping = 1f;
        rb.angularDamping = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        MeshCollider col = go.AddComponent<MeshCollider>();
        col.convex = true;

        Grabbable grab = go.AddComponent<Grabbable>();
        grab.enabled = true;
        var gfield = typeof(Grabbable).GetField("_rigidbody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (gfield != null)
        {
            gfield.SetValue(grab, rb);
        }
        //grab.Initialize(rb);

        HandGrabInteractable handGrab = go.AddComponent<HandGrabInteractable>();
        handGrab.InjectRigidbody(rb);
        handGrab.InjectOptionalPointableElement(grab);
    }


    public void PlayRandomAudio()
    {
        int randomIndex = Random.Range(0, slicingSounds.Length); // Choose a random index
        audioSource.clip = slicingSounds[randomIndex]; // Set the chosen clip
        audioSource.Play(); // Play the clip
    }

    public void TallyPoints()
    {
        float totTime = sliceEndTime - sliceStartTime;
        if (totTime < 10)
            PointsManager.Instance.Points += 2;
        else if (totTime < 20)
            PointsManager.Instance.Points += 1;
    }

    public void MoveToPositionInstant(Transform targetPosition)
    {
        transform.position = targetPosition.position;
    }

    private IEnumerator ForceSelectNextFrame(HandGrabInteractor hand, HandGrabInteractable interactable)
    {
        yield return null; // wait one frame
        if (hand != null && interactable != null)
        {
            hand.ForceSelect(interactable);
        }
    }


}
