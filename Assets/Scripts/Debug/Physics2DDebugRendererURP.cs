using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(10000)]
public class Physics2DDebugRendererURP : MonoBehaviour
{
    [Header("Toggle")]
    [SerializeField] private bool requireDebugSettingsEnabled = true;

    [Header("Colors")]
    [SerializeField] private Color colliderColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField] private Color projectileVelocityColor = new Color(1f, 0.8f, 0f, 1f);

    [Header("Drawing")]
    [SerializeField] private int circleSegments = 20;
    [SerializeField] private float projectileVelocityScale = 0.15f;

    private Camera cam;
    private Material lineMaterial;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != cam) return;

        if (requireDebugSettingsEnabled && !DebugSettings.Enabled)
            return;

        if (lineMaterial == null) return;

        lineMaterial.SetPass(0);

        GL.PushMatrix();

        // IMPORTANT: set matrices so your vertices are world-space
        GL.modelview = cam.worldToCameraMatrix;
        GL.LoadProjectionMatrix(cam.projectionMatrix);

        DrawAllColliders2D();
        DrawProjectileVelocities();

        GL.PopMatrix();
    }

    private void DrawAllColliders2D()
    {
        var colliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        GL.Begin(GL.LINES);
        GL.Color(colliderColor);

        foreach (var col in colliders)
        {
            if (!col.enabled) continue;

            switch (col)
            {
                case BoxCollider2D box: DrawBox(box); break;
                case CircleCollider2D circle: DrawCircle(circle); break;
                case PolygonCollider2D poly: DrawPolygon(poly); break;
                case EdgeCollider2D edge: DrawEdge(edge); break;
            }
        }

        GL.End();
    }

    private void DrawProjectileVelocities()
    {
        FindObjectsSortMode sortMode = FindObjectsSortMode.None;
        var projectiles = Object.FindObjectsByType<Projectile2D>(FindObjectsSortMode.None);

        GL.Begin(GL.LINES);
        GL.Color(projectileVelocityColor);

        foreach (var p in projectiles)
        {
            var rb = p.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            Vector2 start = rb.position;
            Vector2 end = start + rb.linearVelocity * projectileVelocityScale;

            Line(start, end);

            // Arrow head
            Vector2 dir = (end - start);
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                Vector2 right = new Vector2(-dir.y, dir.x);

                float headLen = 0.08f;
                Vector2 a = end - dir * headLen + right * headLen * 0.6f;
                Vector2 b = end - dir * headLen - right * headLen * 0.6f;

                Line(end, a);
                Line(end, b);
            }
        }

        GL.End();
    }

    // --- helpers ---
    private void DrawBox(BoxCollider2D box)
    {
        Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
        Vector2 center = (Vector2)box.transform.TransformPoint(box.offset);

        float angle = box.transform.eulerAngles.z * Mathf.Deg2Rad;
        Vector2 ex = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (size.x * 0.5f);
        Vector2 ey = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * (size.y * 0.5f);

        Vector2 p0 = center - ex - ey;
        Vector2 p1 = center + ex - ey;
        Vector2 p2 = center + ex + ey;
        Vector2 p3 = center - ex + ey;

        Line(p0, p1); Line(p1, p2); Line(p2, p3); Line(p3, p0);
    }

    private void DrawCircle(CircleCollider2D circle)
    {
        Vector2 center = (Vector2)circle.transform.TransformPoint(circle.offset);
        float radius = circle.radius * Mathf.Max(circle.transform.lossyScale.x, circle.transform.lossyScale.y);

        int seg = Mathf.Max(6, circleSegments);
        float step = (Mathf.PI * 2f) / seg;

        Vector2 prev = center + new Vector2(radius, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float t = i * step;
            Vector2 next = center + new Vector2(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
            Line(prev, next);
            prev = next;
        }
    }

    private void DrawPolygon(PolygonCollider2D poly)
    {
        for (int p = 0; p < poly.pathCount; p++)
        {
            Vector2[] pts = poly.GetPath(p);
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = poly.transform.TransformPoint(pts[i]);
                Vector2 b = poly.transform.TransformPoint(pts[(i + 1) % pts.Length]);
                Line(a, b);
            }
        }
    }

    private void DrawEdge(EdgeCollider2D edge)
    {
        var pts = edge.points;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = edge.transform.TransformPoint(pts[i]);
            Vector2 b = edge.transform.TransformPoint(pts[i + 1]);
            Line(a, b);
        }
    }

    private void Line(Vector2 a, Vector2 b)
    {
        GL.Vertex3(a.x, a.y, 0f);
        GL.Vertex3(b.x, b.y, 0f);
    }
}
