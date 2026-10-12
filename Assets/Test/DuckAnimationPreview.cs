using UnityEngine;

// 원작 클립의 재생만 확인한다. 전투나 원작 게임 로직은 실행하지 않는다.
public sealed class DuckAnimationPreview : MonoBehaviour
{
    public Animator animator;
    public AnimationClip[] clips;
    public string[] labels;
    private int selected;
    private float elapsed;
    private Quaternion[] previousRotations;
    private Transform[] trackedBones;
    private bool reported;

    private void Start() => Select(0);

    private void Update()
    {
        // 일정 시간마다 자세를 바꿔 클립별 동작을 비교한다.
        elapsed += Time.deltaTime;
        if (elapsed >= 5f) Select((selected + 1) % clips.Length);
    }

    private void LateUpdate()
    {
        if (trackedBones == null || reported || elapsed < 0.75f) return;
        int changed = 0;
        for (int i = 0; i < trackedBones.Length; i++)
            if (Quaternion.Angle(previousRotations[i], trackedBones[i].localRotation) > 0.01f) changed++;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        // 실제 플레이 모드에서 컨트롤러 상태와 뼈 회전 변경을 함께 기록한다.
        Debug.Log("SniperRuntime " + labels[selected] + ": stateMatches=" + state.IsName(labels[selected]) + "; changedBones=" + changed + "; normalizedTime=" + state.normalizedTime);
        reported = true;
    }

    private void Select(int index)
    {
        if (animator == null || clips == null || clips.Length == 0) return;
        selected = index;
        elapsed = 0f;
        animator.Play(labels[index], 0, 0f);
        trackedBones = animator.GetComponentsInChildren<Transform>(true);
        previousRotations = new Quaternion[trackedBones.Length];
        for (int i = 0; i < trackedBones.Length; i++) previousRotations[i] = trackedBones[i].localRotation;
        reported = false;
    }

    private void OnGUI()
    {
        // 사격 시험이 실행 중이면 같은 위치의 외형 시험 패널을 중복으로 그리지 않는다.
        if (SniperFiringPreview.HasActivePreview || CommonCharacterAnimationPreview.IsActive || DirectClipWeaponPreview.IsActive) return;
        // 테스트 조작은 이 폴더에만 두어 삭제 시 게임 코드에 영향을 주지 않는다.
        GUILayout.BeginArea(new Rect(15, 15, 380, 180), GUI.skin.box);
        GUILayout.Label("Original duck animation / No MagicBlend");
        GUILayout.Label("Auto switch every 5 seconds");
        for (int i = 0; i < labels.Length; i++)
            if (GUILayout.Button(labels[i])) Select(i);
        GUILayout.EndArea();
    }
}
