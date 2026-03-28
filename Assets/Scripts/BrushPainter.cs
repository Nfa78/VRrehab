using UnityEngine;

public class BrushPainter : MonoBehaviour
{
    public RenderTexture paintTexture;
    public Material drawMaterial;
    public float brushSize = 0.05f;
    public Color brushColor = Color.red;
    public LayerMask wallLayer; // Assign this in Inspector to only hit the wall
    public Texture2D brushStamp;

    void Start()
    {
        RenderTexture.active = paintTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0)); // RGBA = transparent black
        RenderTexture.active = null;
    }

    private void OnTriggerStay(Collider other)
    {
        // Optional: use tag if LayerMask isn't used
        if (!other.CompareTag("PaintableWall")) return;

        // Try a raycast from the center of the brush into the wall
        Vector3 origin = transform.position;
        Vector3 direction = transform.up;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 0.05f, wallLayer))
        {
            PaintAtUV(hit.textureCoord);
        }
    }

    void PaintAtUV(Vector2 uv)
    {
        drawMaterial.SetColor("_Color", brushColor);

        RenderTexture.active = paintTexture;
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, paintTexture.width, paintTexture.height, 0);

        float px = uv.x * paintTexture.width;
        float py = (1 - uv.y) * paintTexture.height;
        float size = brushSize * paintTexture.width;

        Rect rect = new Rect(px - size / 2, py - size / 2, size, size);
        Graphics.DrawTexture(rect, brushStamp, drawMaterial);

        GL.PopMatrix();
        RenderTexture.active = null;

        Debug.Log($"Painted at UV {uv}");
    }

}
