using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public sealed class SaveDataLoader
{
    private readonly string saveFilePath;

    public SaveDataLoader(string saveFilePath)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath))
            throw new ArgumentException("저장 파일 경로가 비어 있다.", nameof(saveFilePath));

        this.saveFilePath = Path.GetFullPath(saveFilePath);
    }

    public bool Exists() => File.Exists(saveFilePath);
    public bool BackupExists() => File.Exists(saveFilePath + ".bak");

    public TSaveData Load<TSaveData>() where TSaveData : SaveData
    {
        return LoadFile<TSaveData>(saveFilePath);
    }

    // 손상된 파일을 새 게임으로 덮거나 과거 백업으로 조용히 되돌리지 않는다.
    // 백업 복구는 호출자가 실패를 확인한 뒤 명시적으로 선택한다.
    public TSaveData LoadBackup<TSaveData>() where TSaveData : SaveData
    {
        return LoadFile<TSaveData>(saveFilePath + ".bak");
    }

    private static TSaveData LoadFile<TSaveData>(string path) where TSaveData : SaveData
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("저장 파일을 찾을 수 없다.", path);

        string json = File.ReadAllText(path);
        try
        {
            JObject root = JObject.Parse(json);
            JToken versionToken = root[nameof(SaveData.SaveVersion)];
            if (versionToken == null || versionToken.Type != JTokenType.Integer)
                throw new InvalidDataException("저장 파일의 버전 정보가 유효하지 않다.");

            long version = versionToken.Value<long>();
            if (version != SaveDataVersion.CurrentVersion)
                throw new InvalidDataException($"지원하지 않는 저장 버전이다: {version}");

            TSaveData data = root.ToObject<TSaveData>();
            if (data == null)
                throw new InvalidDataException("저장 데이터가 비어 있다.");

            return data;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("저장 JSON을 변환할 수 없다.", exception);
        }
    }
}
