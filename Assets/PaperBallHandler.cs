using UnityEngine;

public class PaperBallHandler : MonoBehaviour
{
    private PaperInBinObjective pibo;
    private SimpleHandDetection shd;
    private AudioSource paperSfx;
    private bool firstCollisionDone = false;
    private bool hasUsedWrongHand = false;

    void Start()
    {
        pibo = FindAnyObjectByType<PaperInBinObjective>();
        shd = GetComponentInChildren<SimpleHandDetection>();
        paperSfx = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (shd.hasUsedWrongHand)
            hasUsedWrongHand = true;
    }

    public void PlayPaperSound()
    {
        if(!firstCollisionDone)
        {
            firstCollisionDone = true;
        }
        else
        {
            paperSfx.Play();
        }
    }

    public bool HasUsedWrongHand()
    {
        return hasUsedWrongHand;
    }
}
