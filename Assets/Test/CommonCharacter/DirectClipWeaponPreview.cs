using UnityEngine;
using UnityEngine.SceneManagement;

// 첫 시험처럼 Animator 상태를 직접 재생하며 합성 그래프나 총구 강제 회전을 사용하지 않는다.
[DefaultExecutionOrder(50)]
public sealed class DirectClipWeaponPreview : MonoBehaviour
{
    public Animator animator;
    public Camera previewCamera;
    public DirectClipShotPreview shotPreview;
    public int CompletedRolls { get; private set; }
    public static bool IsActive { get; private set; }
    private Camera[] cameras;
    private bool[] cameraStates;
    private string currentState = "Idle";

    private void Awake()
    {
        if (gameObject.scene != SceneManager.GetActiveScene() || IsActive) { enabled = false; return; }
        IsActive = true;
    }

    private void Start()
    {
        cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cameraStates = new bool[cameras.Length];
        for (int i = 0; i < cameras.Length; i++)
        {
            cameraStates[i] = cameras[i].enabled;
            cameras[i].enabled = cameras[i] == previewCamera;
        }
        Play("Idle");
    }

    public void Play(string state)
    {
        if (state != "Idle" && state != "Walk" && state != "Aim" && state != "WalkLeft" && state != "WalkRight" && state != "WalkBackward" && state != "Roll") return;
        currentState = state;
        animator.Play(state, 0, 0);
    }

    private void Update()
    {
        // 구르기는 한 번 재생하고 대기로 돌아간다. 게임의 회피 이동은 이 외형 시험에 포함하지 않는다.
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState == "Roll" && state.IsName("Roll") && state.normalizedTime >= 1f)
        {
            CompletedRolls++;
            Play("Idle");
        }
    }

    public void RequestShot()
    {
        Play("Aim");
        shotPreview.QueueShot();
    }

    private void OnGUI()
    {
        // 모든 주요 버튼을 짧은 두 열로 배치해 스크롤 없이 표시한다.
        GUILayout.BeginArea(new Rect(Screen.width - 320, 12, 308, 300), GUI.skin.box);
        GUILayout.Label("Direct Animator + project WeaponView");
        GUILayout.Label("No MagicBlend / in-place playback");
        if (GUILayout.Button("Idle", GUILayout.Height(32))) Play("Idle");
        if (GUILayout.Button("Walk forward", GUILayout.Height(28))) Play("Walk");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Walk left", GUILayout.Height(28))) Play("WalkLeft");
        if (GUILayout.Button("Walk right", GUILayout.Height(28))) Play("WalkRight");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Walk backward", GUILayout.Height(28))) Play("WalkBackward");
        if (GUILayout.Button("Roll", GUILayout.Height(28))) Play("Roll");
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Aim animation", GUILayout.Height(32))) Play("Aim");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Shot", GUILayout.Height(28))) RequestShot();
        if (GUILayout.Button("Reset target / ammo", GUILayout.Height(28))) shotPreview.ResetTrial();
        GUILayout.EndHorizontal();
        GUILayout.Label("State: " + currentState);
        if (shotPreview != null) GUILayout.Label("Shots " + shotPreview.Fired + " / Hits " + shotPreview.Hits + " / HP " + shotPreview.target.CurrentHealth + " / Ammo " + shotPreview.Rounds);
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (cameras == null) return;
        IsActive = false;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].enabled = cameraStates[i];
    }
}
