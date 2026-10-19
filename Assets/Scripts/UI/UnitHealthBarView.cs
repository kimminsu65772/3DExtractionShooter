using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitHealthBarView : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

    [Header("피해 잔상 / 회복 표시")]
    [SerializeField] private Image followFill;
    [SerializeField, Min(0f)] private float followDelay = 0.2f;
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.3f;
    [SerializeField] private Color damageFollowColor = new Color(1f, 0.7f, 0.25f, 1f);
    [SerializeField] private Color healingFollowColor = new Color(0.2f, 0.9f, 0.35f, 1f);
    [SerializeField] private Image namedIcon;
    [SerializeField] private TMP_Text nameText;

    private float targetRatio;
    private float displayedRatio;
    private float velocity;
    private float followRatio;
    private float followVelocity;
    private float followResumeTime;
    private bool showingHealing;

    private const float RatioEpsilon = 0.001f;

    // 처음 연결하거나 다른 유닛에 재사용할 때
    public void InitializeHealth(float current, float maximum, EnemyDefinition enemyDefinition)
    {
        if (maximum <= 0 || current > maximum)
        {
            Debug.LogWarning("최대 체력이 잘못 설정되었습니다.");
            return;
        }

        if (current < 0)
        {
            Debug.LogWarning("현재 체력이 잘못 설정되었습니다.");
            return;
        }

        targetRatio = Mathf.Clamp01(current / maximum);
        displayedRatio = targetRatio;
        fill.fillAmount = displayedRatio;
        velocity = 0;
        // 재연결·재사용할 때 이전 유닛의 잔상을 남기지 않는다.
        followRatio = targetRatio;
        followVelocity = 0f;
        followResumeTime = 0f;
        showingHealing = false;
        if (namedIcon != null)
            namedIcon.gameObject.SetActive(enemyDefinition != null && enemyDefinition.IsNamed);
        if (nameText != null)
            nameText.text = enemyDefinition != null ? enemyDefinition.DisplayName : string.Empty;
        ApplyFollowFill();
    }

    // 연결된 유닛의 체력이 바뀔 때
    public void SetHealthTarget(float current, float maximum)
    {
        if (maximum <= 0 || current > maximum)
        {
            Debug.LogWarning("최대 체력이 잘못 설정되었습니다.");
            return;
        }
        if (current < 0)
        {
            Debug.LogWarning("현재 체력이 잘못 설정되었습니다.");
            return;
        }
        float nextRatio = Mathf.Clamp01(current / maximum);
        if (nextRatio == targetRatio)
            return;

        if (nextRatio < targetRatio)
        {
            // 연속 피격은 마지막 한 발부터 다시 기다리고, 감소 전 길이를 잔상으로 남긴다.
            followRatio = Mathf.Max(followRatio, Mathf.Max(displayedRatio, targetRatio));
            followResumeTime = Time.time + followDelay;
            showingHealing = false;
        }
        else
        {
            // 회복 후 실제 체력을 초록색으로 먼저 보여주고, 앞쪽 Fill이 따라가게 한다.
            followRatio = nextRatio;
            showingHealing = true;
        }

        followVelocity = 0f;
        targetRatio = nextRatio;
        ApplyFollowFill();
    }

    private void Update()
    {
        if (displayedRatio == targetRatio && followRatio == targetRatio && !showingHealing)
            return;

        if (displayedRatio != targetRatio)
            displayedRatio = Mathf.SmoothDamp(displayedRatio, targetRatio, ref velocity, smoothTime);

        // 목표와의 차이가 표시상 무시할 수준이면 보간을 끝내고 잔여 속도를 정리한다.
        if (Mathf.Abs(displayedRatio - targetRatio) <= RatioEpsilon)
        {
            velocity = 0;
            displayedRatio = targetRatio;
        }

        fill.fillAmount = displayedRatio;

        // 체력을 회복하는 경우는 followRatio를 targetRatio로 바로 맞춘다. (잔상은 회복 표시로 전환)
        if (showingHealing)
        {
            // 회복 중 추가 피격은 SetHealthTarget에서 피해 표시로 전환한다.
            if (displayedRatio == targetRatio)
            {
                followRatio = targetRatio;
                showingHealing = false;
            }
        }
        // 체력을 잃는 경우는 followDelay 이후에 followRatio를 targetRatio로 맞춘다.
        else if (Time.time >= followResumeTime && followRatio != targetRatio)
        {
            followRatio = Mathf.SmoothDamp(followRatio, targetRatio, ref followVelocity, followSmoothTime);
            // 피해 잔상이 앞쪽 체력바보다 짧아지지 않도록 한다.
            followRatio = Mathf.Max(followRatio, displayedRatio);
            if (Mathf.Abs(followRatio - targetRatio) <= RatioEpsilon && displayedRatio == targetRatio)
            {
                followRatio = targetRatio;
                followVelocity = 0f;
            }
        }

        ApplyFollowFill();
    }

    // 색상과 함께 followFill의 fillAmount를 갱신한다.
    private void ApplyFollowFill()
    {
        // 아직 연결하지 않았어도 기존 단일 체력바 표시는 이어간다.
        if (followFill == null)
            return;

        followFill.fillAmount = followRatio;
        followFill.color = showingHealing ? healingFollowColor : damageFollowColor;
    }
}
