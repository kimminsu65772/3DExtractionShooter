using UnityEngine;

// 클립이 머리 트랜스폼을 갱신한 뒤 프리셋의 체형 보정을 다시 적용한다.
public sealed class SniperHeadPreview : MonoBehaviour
{
    public Transform head;
    public Transform forehead;
    public Transform hairSocket;
    public float headScaleOffset;
    public float foreheadHeight;
    public float foreheadRound;

    private void LateUpdate() => Apply();

    public void Apply()
    {
        if (head != null) head.localScale = Vector3.one * (1f + headScaleOffset);
        if (forehead == null) return;
        forehead.localPosition = Vector3.up * foreheadHeight;
        forehead.localScale = new Vector3(1, foreheadRound, 1);
        if (hairSocket != null) hairSocket.localPosition = forehead.localPosition;
    }
}
