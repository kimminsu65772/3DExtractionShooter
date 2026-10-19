using UnityEditor;
using UnityEngine;

// 실제 인지 설정을 읽어 그리기만 한다. 감지 갱신이나 AI 판단은 실행하지 않는다.
public static class EnemyPerceptionGizmos
{
    private static readonly Color ConfirmedColor = new Color(1f, 0.3f, 0.25f);
    private static readonly Color SuspiciousColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color SharedColor = new Color(0.85f, 0.4f, 1f);

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawPerception(EnemyPerception perception, GizmoType gizmoType)
    {
        if (SceneView.currentDrawingSceneView == null || !perception.isActiveAndEnabled)
            return;

        Color previousColor = Handles.color;
        try
        {
            Vector3 origin = perception.ViewOriginPosition;
            Vector3 forward = Vector3.ProjectOnPlane(perception.ViewForward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
                DrawSector(origin, forward, perception.ViewAngle, perception.SuspiciousDistance, SuspiciousColor);
                DrawSector(origin, forward, perception.ViewAngle, perception.ConfirmedDistance, ConfirmedColor);
            }

            Handles.color = Color.white;
            string sightState = perception.CurrentLevel == EnemyPerceptionLevel.Confirmed ? "확정" :
                perception.CurrentLevel == EnemyPerceptionLevel.Suspicious ? "의심" : "없음";
            Handles.Label(origin + Vector3.up * 0.35f,
                $"시야: 확정 {perception.ConfirmedDistance:0.#}m / 의심 {perception.SuspiciousDistance:0.#}m / {perception.ViewAngle:0.#}°" +
                (Application.isPlaying ? $"\n직접 시야 판정: {sightState}" : ""));
        }
        finally
        {
            Handles.color = previousColor;
        }
    }

    private static void DrawSector(Vector3 origin, Vector3 forward, float angle, float radius, Color color)
    {
        // 거리와 각도는 수평으로 검사하므로 같은 평면에 표시한다. 벽 차폐 결과는 이 선에 반영하지 않는다.
        Handles.color = color;
        Vector3 start = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
        Handles.DrawWireArc(origin, Vector3.up, start, angle, radius);
        if (angle < 360f)
        {
            Handles.DrawLine(origin, origin + start * radius);
            if (angle > 0f)
                Handles.DrawLine(origin, origin + Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward * radius);
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawSharedObservation(SquadBlackboard blackboard, GizmoType gizmoType)
    {
        if (SceneView.currentDrawingSceneView == null || !Application.isPlaying ||
            !blackboard.isActiveAndEnabled || !blackboard.HasValidObservation)
            return;

        Color previousColor = Handles.color;
        try
        {
            // 공유 위치는 플레이어 Transform이 아니라 마지막 보고의 스냅샷으로 표시한다.
            SquadObservation observation = blackboard.LatestObservation;
            Vector3 position = observation.Position;
            Handles.color = SharedColor;
            Handles.DrawWireDisc(position, Vector3.up, 0.4f);
            Handles.DrawLine(position, position + Vector3.up);
            Handles.Label(position + Vector3.up,
                $"공유된 관측 위치 ({Mathf.Max(0f, Time.time - observation.ObservedTime):0.0}초 전)");
        }
        finally
        {
            Handles.color = previousColor;
        }
    }
}
