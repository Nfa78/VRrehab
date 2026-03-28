using UnityEngine;

public class VacuumCleaningHandler : MonoBehaviour
{
    private VacuumCleanerHandler vch;
    private int hitCount = 0;
    private Vector3 originalScale;

    public GameObject arrow;
    private float timer = 0f; // to make the arrow reappear after some time passes without removing the dust
    private bool arrowStillActive = true;

    void Start()
    {
        vch = FindAnyObjectByType<VacuumCleanerHandler>();

        originalScale = transform.localScale; // Store original scale
    }

    void Update()
    {
        if(!arrowStillActive)
        {
            timer += Time.deltaTime;
            if (timer >= 10f)
            {
                arrow.SetActive(true);
                timer = 0f;
            }
        }

        if (hitCount >= 3)
        {
            gameObject.SetActive(false);
            vch.UpdateDustRemovedVariable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Vacuum") && vch.VacuumIsOn())
        {
            timer = 0f;
            arrow.SetActive(false);
            arrowStillActive = false;
            hitCount++;
            Debug.Log($"Dust hit by vacuum! Hit count: {hitCount}");

            // Shrink the object proportionally
            float shrinkFactor = Mathf.Clamp01(1f - (hitCount / 3f));
            transform.localScale = originalScale * shrinkFactor;
        }
    }
}
