using UnityEngine;
using TMPro;

public class LoadingTextController : MonoBehaviour
{
    [SerializeField] private TMP_Text[] TMP_Text;
    [SerializeField] private float updateInterval = 0.5f;

    private int currentTextIndex = 0;
    private float nextUpdateTime = 0f;

    private void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            UpdateLoadingText();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    private void UpdateLoadingText()
    {
        // 현재 텍스트를 비활성화
        TMP_Text[currentTextIndex].gameObject.SetActive(false);
        // 다음 텍스트 인덱스를 계산
        currentTextIndex = (currentTextIndex + 1) % TMP_Text.Length;
        // 다음 텍스트를 활성화
        TMP_Text[currentTextIndex].gameObject.SetActive(true);
    }

}
