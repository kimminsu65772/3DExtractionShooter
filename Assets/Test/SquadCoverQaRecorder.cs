#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

// 에디터 플레이 모드에서만 임시로 붙이는 녹화 도구다. 씬이나 프리팹에 저장하지 않는다.
[DefaultExecutionOrder(10000)]
public class SquadCoverQaRecorder : MonoBehaviour
{
    public bool Finished { get; private set; }
    public string OutputFolder { get; private set; }
    private readonly List<object> frames = new List<object>();
    private readonly List<object> events = new List<object>();
    private SquadMemberBrain[] members;
    private SquadCommander commander;
    private SquadBlackboard blackboard;
    private GameObject player;
    private float started;
    private int shots;
    private string stage;
    private Texture2D output;
    private RenderTexture scaled;
    private bool coverRemoved, memberRemoved, leaderRemoved;
    private VisibilityTarget[] visibilityTargets;

    public void Begin(string outputFolder)
    {
        OutputFolder = outputFolder;
        Directory.CreateDirectory(OutputFolder);
        var root = GameObject.Find("Factory_SquadTest");
        commander = root.GetComponent<SquadCommander>();
        blackboard = root.GetComponent<SquadBlackboard>();
        members = new[] { GameObject.Find("Member_A_Observer").GetComponent<SquadMemberBrain>(),
            GameObject.Find("Member_B_Receiver").GetComponent<SquadMemberBrain>(),
            GameObject.Find("Member_C_Assault").GetComponent<SquadMemberBrain>(),
            GameObject.Find("Squad_Leader").GetComponent<SquadMemberBrain>() };
        player = GameObject.Find("Player_YellowDuck");
        player.GetComponent<PlayerController>().enabled = false;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = new Vector3(437f, 0.1f, 239f);
        controller.enabled = true;
        player.GetComponent<HealthComponent>().SetInvulnerable(true);

        commander.enabled = false;
        Vector3[] positions = { new Vector3(437, 0, 267), new Vector3(440, 0, 269),
            new Vector3(434, 0, 270), new Vector3(439, 0, 280) };
        for (int i = 0; i < members.Length; i++)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(positions[i], out hit, 0.8f, -1))
                members[i].NavigationAgent.Warp(hit.position);
            members[i].transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - members[i].transform.position, Vector3.up));
            members[i].ClearCommand();
            members[i].GetComponent<HealthComponent>().SetInvulnerable(true);
            members[i].GetComponent<EnemyAttackController>().OnFired += CountShot;
        }
        foreach (var cover in FindObjectsByType<DestructibleCover>(FindObjectsSortMode.None))
            cover.GetComponent<HealthComponent>().SetInvulnerable(true);

        var camera = Camera.main;
        camera.GetComponent<QuaterViewCameraController>().enabled = false;
        camera.transform.position = new Vector3(435, 30, 284);
        camera.transform.LookAt(new Vector3(435, 0, 265));
        camera.orthographic = true;
        camera.orthographicSize = 14;
        visibilityTargets = root.GetComponentsInChildren<VisibilityTarget>(true);
        Physics.SyncTransforms();
        // 관측 위치를 한 번 주입한 뒤에는 기존 Perception의 실제 시야 갱신을 사용한다.
        members[0].GetComponent<EnemyPerception>().ReportStimulusPosition(player.transform.position);
        commander.enabled = true;
        Application.runInBackground = true;
        started = Time.time;
        stage = "4 members: advance and cover-first rear positioning";
        AddEvent("Begin: player and squad relocated in Play Mode; invulnerability enabled for stable observation.");
        output = new Texture2D(960, 540, TextureFormat.RGB24, false);
        scaled = new RenderTexture(960, 540, 0);
        StartCoroutine(Record());
    }

    private void CountShot() { shots++; }

    private void LateUpdate()
    {
        // 관전자 녹화에서만 전원을 표시한다. 게임용 FOV나 적의 Perception 판단은 바꾸지 않는다.
        if (visibilityTargets != null)
            foreach (var target in visibilityTargets)
                if (target != null) target.SetVisible(true);
    }

    private IEnumerator Record()
    {
        var endOfFrame = new WaitForEndOfFrame();
        float nextFrame = 0f;
        while (Time.time - started < 55f)
        {
            float elapsed = Time.time - started;
            if (!coverRemoved && elapsed >= 22f)
            {
                DestructibleCover selected = members[3].AssignedRearCover;
                if (selected == null)
                    for (int i = 1; i <= 2 && selected == null; i++) selected = members[i].AssignedRearCover;
                if (selected != null)
                {
                    var health = selected.GetComponent<HealthComponent>();
                    health.SetInvulnerable(false);
                    health.TakeDamage(new DamageInfo { incomingDamage = 100000f, attacker = player });
                    AddEvent("Destroyed selected cover: " + selected.name);
                }
                else AddEvent("No selected cover available at destruction checkpoint.");
                coverRemoved = true;
                stage = "Cover destroyed: rear position re-evaluation";
            }
            if (!memberRemoved && elapsed >= 38f)
            {
                var health = members[2].GetComponent<HealthComponent>();
                health.SetInvulnerable(false);
                health.TakeDamage(new DamageInfo { incomingDamage = 100000f, attacker = player });
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false;
                player.transform.position = new Vector3(439, 0.1f, 226);
                controller.enabled = true;
                Physics.SyncTransforms();
                members[0].GetComponent<EnemyPerception>().ReportStimulusPosition(player.transform.position);
                memberRemoved = true;
                stage = "3 members: regroup and formation pressure";
                AddEvent("Assault C killed through HealthComponent.TakeDamage; player moved away to test formation pressure.");
            }
            if (!leaderRemoved && elapsed >= 47f)
            {
                var health = members[3].GetComponent<HealthComponent>();
                health.SetInvulnerable(false);
                health.TakeDamage(new DamageInfo { incomingDamage = 100000f, attacker = player });
                leaderRemoved = true;
                stage = "Leader dead: commands released, radio retained";
                SoundStimulusHub.Emit(player.transform.position, 60f, player);
                AddEvent("Leader killed through HealthComponent.TakeDamage; player sound emitted to verify surviving radio links.");
            }
            yield return endOfFrame;
            if (Time.time < nextFrame)
                continue;
            nextFrame = Time.time + 0.25f;
            Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(capture, scaled);
            RenderTexture.active = scaled;
            output.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            output.Apply();
            RenderTexture.active = previous;
            Destroy(capture);
            string filename = "frame-" + frames.Count.ToString("D4") + ".png";
            File.WriteAllBytes(Path.Combine(OutputFolder, filename), output.EncodeToPNG());
            frames.Add(new { file = filename, time = Time.time - started, stage, tactic = commander.CurrentTactic.ToString(),
                phase = commander.AdvancePhase.ToString(), shots, observationValid = blackboard.HasValidObservation,
                observation = new[] { blackboard.LatestObservation.Position.x, blackboard.LatestObservation.Position.y, blackboard.LatestObservation.Position.z },
                members = SnapshotMembers() });
        }
        File.WriteAllText(Path.Combine(OutputFolder, "recording.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(new { events, frames }, Newtonsoft.Json.Formatting.Indented));
        Finished = true;
        Destroy(output);
        scaled.Release();
        Destroy(scaled);
    }

    private object[] SnapshotMembers()
    {
        object[] result = new object[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member == null) { result[i] = new { name = "removed", alive = false }; continue; }
            Vector3 point = member.transform.position;
            Vector3 viewport = Camera.main.WorldToViewportPoint(point + Vector3.up);
            Vector3 goal = member.RearPosition;
            result[i] = new { name = member.name, alive = member.IsAlive, position = new[] { point.x, point.y, point.z },
                viewport = new[] { viewport.x, viewport.y }, command = member.HasCommand ? member.CurrentCommand.Type.ToString() : "None",
                cover = member.AssignedRearCover == null ? null : member.AssignedRearCover.name,
                hasRearPosition = member.HasRearPosition, rearPosition = new[] { goal.x, goal.y, goal.z },
                speed = member.NavigationAgent.enabled && member.NavigationAgent.isOnNavMesh ? member.NavigationAgent.velocity.magnitude : 0f,
                perception = member.GetComponent<EnemyPerception>().CurrentLevel.ToString() };
        }
        return result;
    }

    private void AddEvent(string description) { events.Add(new { time = Time.time - started, description }); }

    private void OnDestroy()
    {
        if (members == null) return;
        foreach (var member in members)
            if (member != null) member.GetComponent<EnemyAttackController>().OnFired -= CountShot;
    }
}
#endif
