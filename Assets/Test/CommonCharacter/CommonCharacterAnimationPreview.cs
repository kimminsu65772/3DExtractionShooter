using KINEMATION.KAnimationCore.Runtime.Rig;
using KINEMATION.MagicBlend.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

// 원작 게임 제어를 실행하지 않고 파라미터와 자세 합성만 확인한다.
[DefaultExecutionOrder(200)]
public sealed class CommonCharacterAnimationPreview : MonoBehaviour
{
    public Animator animator;
    public MagicBlending blending;
    public KRigComponent rig;
    public MagicBlendAsset[] overlays;
    public Camera previewCamera;
    public Transform weapon;
    public string[] baseStates;
    public string[] handStates;
    private static CommonCharacterAnimationPreview activePreview;
    public static bool IsActive => activePreview != null;
    private Vector2 scroll;
    private float speed;
    private bool ready = true;
    private int hand = 2;
    private float dashUntil;
    private float attackUntil;
    private Camera[] cameras;
    private bool[] cameraStates;
    private AnimationClip aimClip;
    private PlayableGraph aimGraph;
    private AnimationClipPlayable aimPlayable;
    private float aimTime;

    private void Awake()
    {
        rig.Initialize();
        // 여러 시험 씬이 열려 있어도 활성 씬의 담당자만 카메라와 GUI를 제어한다.
        if (gameObject.scene != SceneManager.GetActiveScene() || activePreview != null)
        {
            enabled = false;
            return;
        }
        activePreview = this;
    }

    private void Start()
    {
        // 기존 Aim 시험과 같은 클립을 합성 자산에서 찾아 별도 재생 버튼에 사용한다.
        foreach (var overlay in overlays)
            if (overlay != null && overlay.name == "MagicBlend_Normal" && overlay.overlayPose != null)
                aimClip = overlay.overlayPose;
        // 다른 시험 씬의 카메라를 잠시 끄고 종료 시 복구한다.
        cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        cameraStates = new bool[cameras.Length];
        for (int i = 0; i < cameras.Length; i++)
        {
            cameraStates[i] = cameras[i].enabled;
            if (previewCamera != null && cameras[i] != previewCamera) cameras[i].enabled = false;
        }
        animator.SetInteger("HandState", hand);
        animator.SetBool("GunReady", ready);
        Move(0, Vector2.up);
    }

    private void OnDestroy()
    {
        StopAimPlayback();
        // 비활성 시험의 종료가 활성 시험의 카메라 상태를 덮어쓰지 않게 한다.
        if (activePreview != this) return;
        activePreview = null;
        if (cameras == null) return;
        for (int i = 0; i < cameras.Length; i++)
            if (cameras[i] != null) cameras[i].enabled = cameraStates[i];
    }

    private void Update()
    {
        if (attackUntil > 0 && Time.time >= attackUntil)
        {
            attackUntil = 0;
            animator.SetLayerWeight(animator.GetLayerIndex("MeleeAttack"), 0);
        }
        if (dashUntil > 0 && Time.time >= dashUntil)
        {
            dashUntil = 0;
            animator.SetBool("Dashing", false);
        }
    }

    private void Move(float value, Vector2 direction)
    {
        StopAimPlayback();
        speed = value;
        animator.SetFloat("MoveSpeed", value);
        animator.SetFloat("MoveDirX", direction.x);
        animator.SetFloat("MoveDirY", direction.y);
        animator.SetBool("Die", false);
        animator.SetBool("Dashing", false);
        animator.SetInteger("VehicleType", 0);
        animator.Play("Base Layer.IdleWalkRun", 0, 0);
    }

    private void PlayAimAnimation()
    {
        if (aimClip == null) return;
        Move(0, Vector2.up);
        hand = 2;
        animator.SetInteger("HandState", hand);
        if (weapon != null) weapon.gameObject.SetActive(true);
        // 원본 컨트롤러를 교체하지 않고 조준 클립 출력을 합성 출력보다 나중에 적용한다.
        aimGraph = PlayableGraph.Create("Direct Aim Animation Preview");
        aimGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        aimPlayable = AnimationClipPlayable.Create(aimGraph, aimClip);
        aimTime = 0;
        var output = AnimationPlayableOutput.Create(aimGraph, "Direct Aim", animator);
        output.SetSourcePlayable(aimPlayable);
        output.SetSortingOrder(1000);
        aimGraph.Play();
    }

    private void LateUpdate()
    {
        if (!aimGraph.IsValid()) return;
        // 합성 처리 이후 직접 클립을 평가해 조준 시험 출력이 다시 덮어써지지 않게 한다.
        aimTime = Mathf.Min(aimTime + Time.deltaTime, aimClip.length);
        aimPlayable.SetTime(aimTime);
        aimGraph.Evaluate(0);
    }

    private void StopAimPlayback()
    {
        // 직접 재생 출력을 제거하면 원래 Animator와 MagicBlend 출력이 다시 보인다.
        if (aimGraph.IsValid()) aimGraph.Destroy();
    }

    private void OnGUI()
    {
        // 함께 열린 AITest의 왼쪽 HUD와 겹치지 않도록 시험 조작을 오른쪽에 배치한다.
        float panelWidth = Mathf.Min(330, Screen.width - 24);
        GUILayout.BeginArea(new Rect(Screen.width - panelWidth - 12, 12, panelWidth, Mathf.Max(100, Screen.height - 24)), GUI.skin.box);
        GUILayout.Label("Common duck / original controller + MagicBlend");
        GUILayout.Label("In-place preview (no AI / no projectile)");
        // 핵심 재생 버튼은 스크롤 밖에 두어 긴 상태 목록을 내려도 항상 보이게 한다.
        if (GUILayout.Button("Idle", GUILayout.Height(30))) Move(0, Vector2.up);
        GUI.enabled = aimClip != null;
        if (GUILayout.Button("Aim animation", GUILayout.Height(30))) PlayAimAnimation();
        GUI.enabled = true;
        scroll = GUILayout.BeginScrollView(scroll);
        if (GUILayout.Button("Walk forward")) Move(1, Vector2.up);
        if (GUILayout.Button("Walk backward")) Move(1, Vector2.down);
        if (GUILayout.Button("Walk left")) Move(1, Vector2.left);
        if (GUILayout.Button("Walk right")) Move(1, Vector2.right);
        if (GUILayout.Button("Run forward")) Move(2, Vector2.up);
        bool nextReady = GUILayout.Toggle(ready, "Gun / bow ready");
        if (nextReady != ready) { ready = nextReady; animator.SetBool("GunReady", ready); }
        GUILayout.Label("Hand state (original transition)");
        string[] names = { "Empty", "Normal", "Gun", "Melee", "Bow", "Carry" };
        int[] values = { 0, 1, 2, 3, 4, -1 };
        for (int i = 0; i < names.Length; i++)
            if (GUILayout.Button(names[i]))
            {
                StopAimPlayback();
                hand = values[i]; animator.SetInteger("HandState", hand);
                if (weapon != null) weapon.gameObject.SetActive(hand == 2);
            }
        if (GUILayout.Button("Attack trigger (melee / bow)"))
        {
            StopAimPlayback();
            // 원작처럼 근접 공격 레이어의 가중치를 별도로 켠다.
            if (hand == 3)
            {
                animator.SetLayerWeight(animator.GetLayerIndex("MeleeAttack"), 1);
                attackUntil = Time.time + 1;
            }
            animator.SetTrigger("Attack");
        }
        if (GUILayout.Button("Dash")) { StopAimPlayback(); animator.SetBool("Dashing", true); dashUntil = Time.time + .6f; }
        GUILayout.Label("Die1: legacy T-pose reference (not a death clip)");
        GUILayout.Label("Base states (direct playback)");
        foreach (string state in baseStates)
            if (!state.EndsWith(".Die1") && GUILayout.Button(state)) { StopAimPlayback(); animator.Play(state, 0, 0); }
        GUILayout.Label("Hand states (direct playback)");
        foreach (string state in handStates)
            if (GUILayout.Button(state)) { StopAimPlayback(); animator.Play(state, animator.GetLayerIndex("Hand"), 0); }
        GUILayout.Label("Overlay asset (manual blend preview)");
        foreach (var overlay in overlays)
            if (GUILayout.Button(overlay.name)) { StopAimPlayback(); blending.UpdateMagicBlendAsset(overlay, true); }
        GUILayout.Label("Speed=" + speed + "  Blend=" + (blending.BlendAsset == null ? "none" : blending.BlendAsset.name));
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
