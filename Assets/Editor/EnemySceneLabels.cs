using UnityEditor;
using UnityEngine;

// 테스트용 식별 표시는 에디터에서만 그린다. 게임 화면이나 AI 판단에는 관여하지 않는다.
public static class EnemySceneLabels
{
    private static GUIStyle leaderStyle;
    private static GUIStyle memberStyle;
    private static GUIStyle deadStyle;

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawSquadMember(SquadMemberBrain member, GizmoType gizmoType)
    {
        if (SceneView.currentDrawingSceneView == null || !member.gameObject.activeInHierarchy || !member.enabled)
            return;
        EnsureStyles();
        SquadCommander commander = member.GetComponentInParent<SquadCommander>();
        bool isLeader = commander != null && commander.Leader == member;
        string label = isLeader ? "분대장" : GetMemberLabel(member.name);
        bool dead = Application.isPlaying && !member.IsAlive;
        Handles.Label(member.transform.position + Vector3.up * 1.4f,
            dead ? label + " (사망)" : label, dead ? deadStyle : isLeader ? leaderStyle : memberStyle);
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawOrdinaryEnemy(EnemyRangedBrain brain, GizmoType gizmoType)
    {
        if (SceneView.currentDrawingSceneView == null || !brain.gameObject.activeInHierarchy || !brain.enabled)
            return;
        SquadMemberBrain squad = brain.GetComponent<SquadMemberBrain>();
        if (squad != null && squad.enabled)
            return;
        EnsureStyles();
        Handles.Label(brain.transform.position + Vector3.up * 1.4f, "적: " + brain.name, memberStyle);
    }

    private static string GetMemberLabel(string name)
    {
        if (name.StartsWith("Member_A")) return "돌격대원 A";
        if (name.StartsWith("Member_B")) return "돌격대원 B";
        if (name.StartsWith("Member_C")) return "돌격대원 C";
        return "대원: " + name;
    }

    private static void EnsureStyles()
    {
        if (leaderStyle != null) return;
        leaderStyle = MakeStyle(new Color(1f, 0.8f, 0.25f));
        memberStyle = MakeStyle(new Color(0.4f, 0.9f, 1f));
        deadStyle = MakeStyle(Color.gray);
    }

    private static GUIStyle MakeStyle(Color color)
    {
        var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
        style.normal.textColor = color;
        return style;
    }
}
