using System;

// 실제 창고·장비 데이터는 이 기반을 상속한 별도 저장 타입에서 정의한다.
// Unity 오브젝트 참조 대신 파일에 남길 값만 담는다.
[Serializable]
public abstract class SaveData
{
    public int SaveVersion { get; set; } = SaveDataVersion.CurrentVersion;
    public string LastSavedAtUtc { get; set; } = string.Empty;
}
