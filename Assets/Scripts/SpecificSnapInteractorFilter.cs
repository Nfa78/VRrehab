using Oculus.Interaction;
using UnityEngine;

public class SpecificSnapInteractorFilter : MonoBehaviour, IGameObjectFilter
{
    [SerializeField]
    private SnapInteractor allowedSnapInteractor;

    public bool Filter(GameObject obj)
    {
        return obj == allowedSnapInteractor?.gameObject;
    }
}
