using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(10000)]
public class VelocityAccelDebugRendererURP : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Camera targetCamera;

    [Header("Refs")]
    [SerializeField] private PlayerControls player;

    [Header("Toggle")]
    [SerializeField] private bool enabledDebug = true;
    [SerializeField] private bool requireDebugSettingsEnabled = true;

    [Header("Layout (pixels)")]
    [SerializeField] private Vector2 origin = new Vector2(10f, 10f);
    [SerializeField] private float graphWidth = 120f;
    [SerializeField] private float graphHeight = 25f;
    [SerializeField] private float spacing = 4f;

    [Header("Ranges")]
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float maxAccel = 120f;

    [Header("Colors")]
    [SerializeField] private Color speedMagColor = Color.white;
    [SerializeField] private Color speedXColor = Color.cyan;
    [SerializeField] private Color speedYColor = Color.green;
    [SerializeField] private Color accelColor = Color.yellow;
    [SerializeField] private Color zeroLineColor = new Color(1f, 1f, 1f, 0.15f);
    private Camera cam;
    private Material lineMaterial;

    private void Awake()
    {
        cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning($"{nameof(VelocityAccelDebugRendererURP)}: No target camera set and no Camera.main found.");
            enabled = false;
            return;
        }

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnEnable() => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    private void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (!enabledDebug) return;
        if (renderingCamera != cam) return;

        if (requireDebugSettingsEnabled && !DebugSettings.Enabled)
            return;

        if (player == null) return;

        var samples = player.MovementDebug;
        if (samples == null) return;

        if (lineMaterial == null) return;
        lineMaterial.SetPass(0);

        GL.PushMatrix();

        // Screen-space drawing in pixels (0,0 bottom-left)
        GL.LoadPixelMatrix(0, cam.pixelWidth, 0, cam.pixelHeight);

        // Draw 4 stacked graphs (top to bottom)
        DrawGraphUnsigned(samples.speedMag, samples.WriteIndex,
            origin + new Vector2(0, (graphHeight + spacing) * 3),
            maxSpeed, speedMagColor, drawZeroLine: false);

        DrawGraphSigned(samples.speedX, samples.WriteIndex,
            origin + new Vector2(0, (graphHeight + spacing) * 2),
            maxSpeed, speedXColor, drawZeroLine: true);

        DrawGraphSigned(samples.speedY, samples.WriteIndex,
            origin + new Vector2(0, (graphHeight + spacing) * 1),
            maxSpeed, speedYColor, drawZeroLine: true);

        DrawGraphUnsigned(samples.accel, samples.WriteIndex,
            origin,
            maxAccel, accelColor, drawZeroLine: false);

        GL.PopMatrix();
    }

    private void DrawGraphUnsigned(float[] data, int writeIndex, Vector2 bottomLeft, float maxValue, Color color, bool drawZeroLine)
    {
        float dx = graphWidth / (MovementDebugSamples.SampleCount - 1);

        // Optional baseline (y=0)
        if (drawZeroLine)
        {
            GL.Begin(GL.LINES);
            GL.Color(zeroLineColor);
            GL.Vertex3(bottomLeft.x, bottomLeft.y, 0f);
            GL.Vertex3(bottomLeft.x + graphWidth, bottomLeft.y, 0f);
            GL.End();
        }

        GL.Begin(GL.LINES);
        GL.Color(color);

        for (int i = 0; i < MovementDebugSamples.SampleCount - 1; i++)
        {
            int a = (writeIndex + i) % MovementDebugSamples.SampleCount;
            int b = (writeIndex + i + 1) % MovementDebugSamples.SampleCount;

            float va01 = Mathf.Clamp01(data[a] / maxValue);
            float vb01 = Mathf.Clamp01(data[b] / maxValue);

            float x0 = bottomLeft.x + i * dx;
            float x1 = bottomLeft.x + (i + 1) * dx;

            float y0 = bottomLeft.y + va01 * graphHeight;
            float y1 = bottomLeft.y + vb01 * graphHeight;

            GL.Vertex3(x0, y0, 0f);
            GL.Vertex3(x1, y1, 0f);
        }

        GL.End();
    }

    private void DrawGraphSigned(float[] data, int writeIndex, Vector2 bottomLeft, float maxAbsValue, Color color, bool drawZeroLine)
    {
        float dx = graphWidth / (MovementDebugSamples.SampleCount - 1);
        float midY = bottomLeft.y + graphHeight * 0.5f;

        // Midline (0)
        if (drawZeroLine)
        {
            GL.Begin(GL.LINES);
            GL.Color(zeroLineColor);
            GL.Vertex3(bottomLeft.x, midY, 0f);
            GL.Vertex3(bottomLeft.x + graphWidth, midY, 0f);
            GL.End();
        }

        GL.Begin(GL.LINES);
        GL.Color(color);

        for (int i = 0; i < MovementDebugSamples.SampleCount - 1; i++)
        {
            int a = (writeIndex + i) % MovementDebugSamples.SampleCount;
            int b = (writeIndex + i + 1) % MovementDebugSamples.SampleCount;

            float va = Mathf.Clamp(data[a] / maxAbsValue, -1f, 1f);
            float vb = Mathf.Clamp(data[b] / maxAbsValue, -1f, 1f);

            float x0 = bottomLeft.x + i * dx;
            float x1 = bottomLeft.x + (i + 1) * dx;

            float y0 = midY + va * (graphHeight * 0.5f);
            float y1 = midY + vb * (graphHeight * 0.5f);

            GL.Vertex3(x0, y0, 0f);
            GL.Vertex3(x1, y1, 0f);
        }

        GL.End();
    }
}
