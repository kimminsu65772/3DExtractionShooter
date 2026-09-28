using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HealthComponent))]
public class TargetDummyHitIndicator : MonoBehaviour
{
    [Header("참조 설정")]
    [SerializeField] private Camera targetCamera;

    [Header("표시 설정")]
    [SerializeField] private float displayDuration = 1.2f;
    [SerializeField] private float markerSize = 22f;
    [SerializeField] private Vector2 labelOffset = new Vector2(18f, -34f);
    [SerializeField] private Color headColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color bodyColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color defaultColor = Color.white;

    private HealthComponent healthComponent;
    private DamageInfo lastDamageInfo;
    private float hideTime;
    private bool hasHitInfo;
    private GUIStyle labelStyle;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
        ResolveCamera();
    }

    private void OnEnable()
    {
        if (healthComponent == null)
        {
            healthComponent = GetComponent<HealthComponent>();
        }

        healthComponent.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        if (healthComponent == null)
            return;

        healthComponent.OnDamageTaken -= HandleDamageTaken;
    }

    private void HandleDamageTaken(DamageInfo damageInfo)
    {
        lastDamageInfo = damageInfo;
        hideTime = Time.time + displayDuration;
        hasHitInfo = true;
    }

    private void OnGUI()
    {
        if (!hasHitInfo || Time.time > hideTime)
            return;

        ResolveCamera();

        if (targetCamera == null)
            return;

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(lastDamageInfo.hitPoint);

        if (screenPoint.z <= 0f)
            return;

        screenPoint.y = Screen.height - screenPoint.y;

        Color markerColor = GetMarkerColor(lastDamageInfo.hitPart);
        float alpha = Mathf.Clamp01((hideTime - Time.time) / displayDuration);
        markerColor.a *= alpha;

        DrawHitMarker(screenPoint, markerColor);
        DrawHitLabel(screenPoint, markerColor);
    }

    private void DrawHitMarker(Vector3 screenPoint, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;

        float halfSize = markerSize * 0.5f;
        float thickness = 2f;

        GUI.DrawTexture(
            new Rect(screenPoint.x - halfSize, screenPoint.y - thickness * 0.5f, markerSize, thickness),
            Texture2D.whiteTexture
        );

        GUI.DrawTexture(
            new Rect(screenPoint.x - thickness * 0.5f, screenPoint.y - halfSize, thickness, markerSize),
            Texture2D.whiteTexture
        );

        GUI.color = previousColor;
    }

    private void DrawHitLabel(Vector3 screenPoint, Color color)
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }

        Color previousColor = GUI.color;
        Color previousTextColor = labelStyle.normal.textColor;

        GUI.color = new Color(1f, 1f, 1f, color.a);
        labelStyle.normal.textColor = color;

        string label = $"{lastDamageInfo.hitPart} / {lastDamageInfo.incomingDamage:0.0}";
        Rect labelRect = new Rect(
            screenPoint.x + labelOffset.x,
            screenPoint.y + labelOffset.y,
            180f,
            24f
        );

        GUI.Label(labelRect, label, labelStyle);

        labelStyle.normal.textColor = previousTextColor;
        GUI.color = previousColor;
    }

    private Color GetMarkerColor(HitPart hitPart)
    {
        return hitPart switch
        {
            HitPart.Head => headColor,
            HitPart.Body => bodyColor,
            _ => defaultColor
        };
    }

    private void ResolveCamera()
    {
        if (targetCamera != null)
            return;

        targetCamera = Camera.main;
    }
}
