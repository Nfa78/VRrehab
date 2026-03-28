using Oculus.Interaction;
using UnityEngine;

public class SpecificSnapTargetFilter : MonoBehaviour, IGameObjectFilter
{
    [SerializeField]
    private SnapInteractable allowedSnapTarget;

    public bool Filter(GameObject obj)
    {
        return obj == allowedSnapTarget?.gameObject;
    }
}
