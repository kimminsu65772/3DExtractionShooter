using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.InputSystem;

// 이 씬에서만 생성하는 실험 공간으로 실제 게임 씬과 프리팹을 변경하지 않는다.
public class AITestSandbox : MonoBehaviour
{
    private CharacterController target;
    private AITestAgent brain;
    private GameObject runtimeRoot;
    private Material floorMaterial, wallMaterial, targetMaterial, enemyMaterial;
    private GUIStyle labelStyle;

    private void Start()
    {
        runtimeRoot = new GameObject("BT 실험 공간 (런타임)");
        floorMaterial = MakeMaterial(new Color(0.25f, 0.3f, 0.35f));
        wallMaterial = MakeMaterial(new Color(0.7f, 0.65f, 0.45f));
        targetMaterial = MakeMaterial(Color.cyan);
        enemyMaterial = MakeMaterial(Color.red);
        Shape("바닥", PrimitiveType.Cube, new Vector3(0, -0.5f, 0), new Vector3(30, 1, 24), floorMaterial);
        Shape("시야 차폐벽", PrimitiveType.Cube, new Vector3(0, 1.5f, 0), new Vector3(1, 3, 8), wallMaterial);
        // 환경만 먼저 베이크해 움직이는 대상이 고정 장애물로 수집되지 않게 한다.
        NavMeshSurface surface = runtimeRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        Physics.SyncTransforms();
        surface.BuildNavMesh();

        GameObject player = Shape("이동 대상 (WASD)", PrimitiveType.Capsule, new Vector3(6, 1, 0), Vector3.one, targetMaterial);
        Destroy(player.GetComponent<Collider>());
        target = player.AddComponent<CharacterController>();
        target.height = 2;
        target.radius = 0.4f;

        Transform[] points = new Transform[4];
        Vector3[] positions = { new Vector3(-7, 0, -7), new Vector3(-7, 0, 7), new Vector3(7, 0, 7), new Vector3(7, 0, -7) };
        for (int i = 0; i < points.Length; i++)
        {
            GameObject point = new GameObject("순찰 지점 " + i);
            point.transform.SetParent(runtimeRoot.transform);
            point.transform.position = positions[i];
            points[i] = point.transform;
        }
        GameObject enemy = Shape("BT 적", PrimitiveType.Capsule, new Vector3(-7, 0, -7), Vector3.one, enemyMaterial);
        enemy.GetComponent<Collider>().enabled = false;
        // 에이전트 루트는 발밑에 두고 메시만 높여 NavMesh와 렌더링 기준을 분리한다.
        GameObject visual = Shape("적 외형", PrimitiveType.Capsule, enemy.transform.position + Vector3.up, Vector3.one, enemyMaterial);
        visual.transform.SetParent(enemy.transform, true);
        visual.GetComponent<Collider>().enabled = false;
        enemy.GetComponent<Renderer>().enabled = false;
        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 3;
        agent.angularSpeed = 360;
        agent.radius = 0.4f;
        agent.height = 2;
        enemy.AddComponent<EnemyMotor>();
        brain = enemy.AddComponent<AITestAgent>();
        LineRenderer tracer = enemy.AddComponent<LineRenderer>();
        tracer.sharedMaterial = enemyMaterial;
        tracer.positionCount = 2;
        tracer.startWidth = tracer.endWidth = 0.04f;
        tracer.useWorldSpace = true;
        tracer.enabled = false;
        // 환경 전용 레이어로 벽만 시야를 막고 대상과 적은 자기 시야를 막지 않게 한다.
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0) obstacleLayer = 2;
        runtimeRoot.transform.Find("시야 차폐벽").gameObject.layer = obstacleLayer;
        brain.Configure(player.transform, points, 1 << obstacleLayer, tracer);

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0, 24, -18);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 16;
        }
    }

    private void Update()
    {
        if (target == null || Keyboard.current == null) return;
        Keyboard keyboard = Keyboard.current;
        Vector3 direction = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), 0,
            (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
        target.Move((direction.normalized * 6f + Vector3.down * 9.81f) * Time.deltaTime);
    }

    private GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject value = GameObject.CreatePrimitive(type);
        value.name = name;
        value.transform.SetParent(runtimeRoot.transform);
        value.transform.position = position;
        value.transform.localScale = scale;
        value.GetComponent<Renderer>().sharedMaterial = material;
        return value;
    }

    private static Material MakeMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        return material;
    }

    private void OnGUI()
    {
        if (brain == null) return;
        labelStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.UpperLeft };
        GUI.Box(new Rect(15, 15, 540, 150), "BT 실험: WASD로 파란 대상 이동\n현재 행동: " + brain.ActiveAction +
            "\n모의 사격: " + brain.SimulatedShots + " (피해 없음)\n판단 횟수: " + brain.DecisionCount +
            " / 시야 Raycast: " + brain.VisionRayCount + "\n벽 뒤로 숨으면 마지막 발견 위치를 조사한다.", labelStyle);
    }

    private void OnDestroy()
    {
        if (runtimeRoot != null) Destroy(runtimeRoot);
        Destroy(floorMaterial); Destroy(wallMaterial); Destroy(targetMaterial); Destroy(enemyMaterial);
    }
}
