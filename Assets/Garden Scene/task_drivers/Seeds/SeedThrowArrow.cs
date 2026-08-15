using UnityEngine;

[DisallowMultipleComponent]
public class SeedThrowArrow : MonoBehaviour
{
    [Header("Arrow")]
    [SerializeField] private bool showThrowDirectionArrow = true;
    [SerializeField] private bool rightHandOnlyArrow = false;
    [SerializeField] private float arrowLength = 0.25f;
    [SerializeField] private float arrowHeadLength = 0.06f;
    [SerializeField] private float arrowHeadAngleDeg = 30f;
    [SerializeField] private float arrowWidth = 0.008f;
    [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.2f, 1f);

    private LineRenderer _arrow;

    public void Render(Transform hand, bool seedsLoaded, bool isRightHandLikely)
    {
        bool shouldShow = showThrowDirectionArrow && seedsLoaded && hand != null;
        if (shouldShow && rightHandOnlyArrow)
        {
            shouldShow = isRightHandLikely;
        }

        if (!shouldShow)
        {
            SetVisible(false);
            return;
        }

        EnsureRenderer();

        Vector3 start = hand.position;
        Vector3 direction = hand.forward.sqrMagnitude > 0.0001f ? hand.forward.normalized : transform.forward;
        Vector3 end = start + direction * Mathf.Max(0.01f, arrowLength);

        Vector3 wingAxis = Vector3.Cross(direction, Vector3.up);
        if (wingAxis.sqrMagnitude < 0.0001f)
        {
            wingAxis = Vector3.Cross(direction, Vector3.right);
        }
        wingAxis.Normalize();

        float headLen = Mathf.Clamp(arrowHeadLength, 0.01f, arrowLength);
        float wingOffset = Mathf.Tan(Mathf.Clamp(arrowHeadAngleDeg, 5f, 80f) * Mathf.Deg2Rad) * headLen;
        Vector3 headBase = end - direction * headLen;
        Vector3 headLeft = headBase + wingAxis * wingOffset;
        Vector3 headRight = headBase - wingAxis * wingOffset;

        _arrow.positionCount = 5;
        _arrow.SetPosition(0, start);
        _arrow.SetPosition(1, end);
        _arrow.SetPosition(2, headLeft);
        _arrow.SetPosition(3, end);
        _arrow.SetPosition(4, headRight);
        SetVisible(true);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void EnsureRenderer()
    {
        if (_arrow != null)
        {
            _arrow.startWidth = arrowWidth;
            _arrow.endWidth = arrowWidth;
            _arrow.startColor = arrowColor;
            _arrow.endColor = arrowColor;
            return;
        }

        GameObject arrowObject = new GameObject("ThrowDirectionArrow");
        arrowObject.transform.SetParent(transform, false);
        _arrow = arrowObject.AddComponent<LineRenderer>();
        _arrow.useWorldSpace = true;
        _arrow.material = new Material(Shader.Find("Sprites/Default"));
        _arrow.numCapVertices = 4;
        _arrow.numCornerVertices = 4;
        _arrow.alignment = LineAlignment.View;
        _arrow.startWidth = arrowWidth;
        _arrow.endWidth = arrowWidth;
        _arrow.startColor = arrowColor;
        _arrow.endColor = arrowColor;
        _arrow.enabled = false;
    }

    private void SetVisible(bool visible)
    {
        if (_arrow != null)
        {
            _arrow.enabled = visible;
        }
    }
}
