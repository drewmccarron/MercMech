using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class GroundProbeDebugRendererURP : MonoBehaviour
{
    [SerializeField] private Color groundedColor = new Color(0f, 1f, 0f, 1f);
    [SerializeField] private Color notGroundedColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 0f, 1f);

    [SerializeField] private float normalLength = 0.35f;

    private Camera cam;
    private Material lineMaterial;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnEnable() => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    private void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

    private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera renderingCamera)
    {
        if (renderingCamera != cam) return;
        if (!DebugSettings.Enabled) return;
        if (lineMaterial == null) return;

        var players = Object.FindObjectsByType<PlayerControls>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0) return;

        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.modelview = cam.worldToCameraMatrix;
        GL.LoadProjectionMatrix(cam.projectionMatrix);

        foreach (var p in players)
        {
            if (!p.GroundProbe.TryGetGroundProbeDebug(out var info))
                continue;

            // Probe box
            GL.Begin(GL.LINES);
            GL.Color(info.grounded ? groundedColor : notGroundedColor);
            DebugGL2D.WireBox(info.centerWorld, info.sizeWorld, info.angleDeg);
            GL.End();

            // Contact normals
            if (info.contactCount > 0 && info.contacts != null)
            {
                GL.Begin(GL.LINES);
                GL.Color(normalColor);

                for (int i = 0; i < info.contactCount; i++)
                {
                    var c = info.contacts[i];
                    DebugGL2D.Arrow(c.point, c.normal, normalLength);
                }

                GL.End();
            }
        }

        GL.PopMatrix();
    }
}
