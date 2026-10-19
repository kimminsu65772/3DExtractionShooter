using System;

public static class SaveDataFactory
{
    // 도메인별 초기 값은 TSaveData의 초기화 코드에서 정의한다.
    // 다른 게임의 기본 장비나 재화를 새 저장에 섞지 않는다.
    public static TSaveData CreateNewData<TSaveData>() where TSaveData : SaveData, new()
    {
        return new TSaveData
        {
            SaveVersion = SaveDataVersion.CurrentVersion,
            LastSavedAtUtc = DateTime.UtcNow.ToString("o")
        };
    }
}

public static class SaveDataVersion
{
    // 기존 팀 프로젝트의 버전 번호와 마이그레이션은 가져오지 않는다.
    public const int CurrentVersion = 1;
}
