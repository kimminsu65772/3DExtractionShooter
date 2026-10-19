using UnityEngine;
using UnityEngine.UI;

public class LoadingDuckAnimator : MonoBehaviour
{
    [SerializeField] private RawImage duckImage;
    [SerializeField] private int columns = 4;
    [SerializeField] private int rows = 4;
    [SerializeField] private float duration = 1f;

    private float elapsedTime;

    // 경과 시간을 통해 프레임 번호를 계산하고, 해당 프레임의 UV 좌표를 RawImage에 적용하는 방식으로 애니메이션을 구현한다.
    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;
        elapsedTime %= duration;

        float t = elapsedTime / duration;

        int totalFrames = columns * rows;

        // 시간 경과에 따라 현재 프레임을 계산한다. Mathf.FloorToInt를 사용하여 소수점을 버리고 정수로 변환하며, % 연산자를 사용하여 반복적으로 애니메이션이 재생되도록 한다.
        int currentFrame = Mathf.FloorToInt(t * totalFrames) % totalFrames;

        // 현재 프레임의 행과 열을 계산한다.
        // 행과 열을 계산하는 일종의 공식으로, 현재 프레임 번호를 열의 개수로 나눈 몫이 행 번호가 되고, 나머지가 열 번호가 된다.
        int currentRow = currentFrame / columns;
        int currentColumn = currentFrame % columns;

        // UV 좌표를 계산한다.
        // 이미지의 전체 너비와 높이를 1로 정규화했을 때, 각 프레임의 UV 좌표를 계산한다.
        // 여기서 U는 수평 좌표, V는 수직 좌표를 의미하며, 각 프레임의 위치를 계산하기 위해 현재 행과 열을 사용한다.
        float uMin = (float)currentColumn / columns;
        // 좌표는 왼쪽 아래에서 시작하는 것과 달리 프레임 번호는 위쪽에서 시작한다. 따라서 Y 좌표를 계산할 때는 1에서 현재 행 번호를 나눈 값을 빼주어야 한다.
        float vMin = 1f - (float)(currentRow + 1) / rows; 

        float uMax = (float)(currentColumn + 1) / columns;
        float vMax = 1f - (float)currentRow / rows;

        // RawImage의 UV Rect를 설정한다.
        duckImage.uvRect = new Rect(uMin, vMin, uMax - uMin, vMax - vMin);
    }
}
