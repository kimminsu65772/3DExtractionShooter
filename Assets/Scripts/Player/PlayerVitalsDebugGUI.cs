using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HealthComponent), typeof(StaminaComponent))]
public class PlayerVitalsDebugGUI : MonoBehaviour
{
    private const float Margin = 16f;
    private const float PanelWidth = 248f;
    private const float PanelHeight = 92f;

    private HealthComponent health;
    private StaminaComponent stamina;
    private GUIStyle labelStyle;

    private void Awake()
    {
        health = GetComponent<HealthComponent>();
        stamina = GetComponent<StaminaComponent>();
    }

    private void OnGUI()
    {
        if (health == null || stamina == null)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            labelStyle.normal.textColor = Color.white;
        }

        Rect panel = new Rect(Margin, Screen.height - PanelHeight - Margin, PanelWidth, PanelHeight);
        GUI.Box(panel, GUIContent.none);

        DrawGauge(new Rect(panel.x + 12f, panel.y + 10f, PanelWidth - 24f, 30f), "HP", health.CurrentHealth, health.MaxHealth, new Color(0.9f, 0.25f, 0.25f));
        DrawGauge(new Rect(panel.x + 12f, panel.y + 50f, PanelWidth - 24f, 30f), "STA", stamina.CurrentStamina, stamina.MaxStamina, new Color(0.25f, 0.7f, 0.95f));
    }

    private void DrawGauge(Rect area, string title, float current, float maximum, Color fillColor)
    {
        GUI.Label(new Rect(area.x, area.y, area.width, 18f), $"{title}  {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(maximum)}", labelStyle);

        Rect bar = new Rect(area.x, area.y + 20f, area.width, 10f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        float fillRatio = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fillRatio, bar.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
