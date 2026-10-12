using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ExtractionShooter.DataPipeline;

// 비교 씬만 제어한다. 플레이어 입력과 실제 게임의 장비 소유권을 변경하지 않는다.
public sealed class WeaponShowcase : MonoBehaviour
{
    public TextAsset weaponCsv;
    public TextAsset ammoCsv;
    public WeaponShowcaseStation[] stations;
    public Camera previewCamera;
    public Camera[] stationCameras;
    public Transform inactiveRoot;
    public string[] viewAddresses;
    public string projectileAddress = "projectiles/bullet";
    public bool automaticShot = true;
    public int SelectedWeaponId { get; private set; }
    public int Fired { get; private set; }
    public string Status { get; private set; } = "Loading weapons...";
    private readonly KeyedPoolService pools = new KeyedPoolService();
    private readonly AddressablesAssetStore assets = new AddressablesAssetStore();
    private readonly ProjectileSpawnService projectiles = new ProjectileSpawnService();
    private readonly PoolLease[] views = new PoolLease[4];
    private readonly WeaponInstance[] weapons = new WeaponInstance[4];
    private readonly WeaponView[] weaponViews = new WeaponView[4];
    private WeaponDefinition[] definitions;
    private IReadOnlyList<AmmoDefinition> ammunition;
    private CancellationTokenSource lifetime;
    private Task initialization;
    private bool ready;
    private bool pendingShot;
    private bool awaitingFrame;
    private float nextShot;
    private float frameDeadline;
    private int selected;
    private bool previousBackground;
    private static readonly string[] PoseLabels = { "IDLE", "RUN", "AIM", "SHOT" };

    private async void Start()
    {
        // 에디터가 다른 창 뒤에 있어도 Addressables 준비와 네 비교 동작을 진행한다.
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        lifetime = new CancellationTokenSource();
        try
        {
            // 게임과 같은 CSV 바인딩과 검증기를 사용해 수치 복사본이 원본과 어긋나지 않게 한다.
            var report = new DataReport();
            var reader = new CsvHelperRecordReader();
            using (var text = new StringReader(weaponCsv.text))
                definitions = new CsvTableLoader<WeaponDefinition>().Load(reader.Read(text, weaponCsv.name, report),
                    new GeneratedWeaponBinding(), new WeaponValidator(), report).ToArray();
            using (var text = new StringReader(ammoCsv.text))
                ammunition = new CsvTableLoader<AmmoDefinition>().Load(reader.Read(text, ammoCsv.name, report),
                    new GeneratedAmmoBinding(), new AmmoValidator(), report);
            if (report.HasErrors) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(i => i.Message)));
            if (definitions.Length != viewAddresses.Length || stations.Length != 4)
                throw new InvalidOperationException("CSV 외형 목록과 네 비교 구역을 확인한다.");
            var plan = new List<PoolSpec> { Spec(1, AssetKind.Projectile, projectileAddress, 32, 128) };
            for (int i = 0; i < definitions.Length; i++)
                plan.Add(Spec(definitions[i].weaponId, AssetKind.WeaponView, viewAddresses[i], 4, 8));
            initialization = pools.InitializeAsync(plan, assets, inactiveRoot, lifetime.Token);
            await initialization;
            lifetime.Token.ThrowIfCancellationRequested();
            ready = true;
            SelectWeapon(0);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Status = exception.Message; Debug.LogException(exception, this); }
    }

    private static PoolSpec Spec(int id, AssetKind kind, string address, int prewarm, int maximum) => new PoolSpec
    {
        PoolId = id, Kind = kind, Address = address, PrewarmCount = prewarm,
        MaxActive = maximum, MaxRetained = maximum, Overflow = PoolOverflowPolicy.GrowWithinLimit
    };

    public bool SelectWeapon(int index)
    {
        if (!ready || index < 0 || index >= definitions.Length) return false;
        var leases = new PoolLease[4];
        var nextViews = new WeaponView[4];
        try
        {
            // 네 외형이 전부 준비되기 전에는 이전 비교 무기를 반환하지 않는다.
            for (int i = 0; i < 4; i++)
            {
                if (!pools.TryRent(definitions[index].weaponId, out leases[i])) throw new InvalidOperationException("외형 대여 실패");
                nextViews[i] = leases[i].Instance.GetComponentInChildren<WeaponView>(true);
                if (nextViews[i] == null || !nextViews[i].HasMuzzle || !nextViews[i].TryAttachTo(stations[i].mount))
                    throw new InvalidOperationException("총구 또는 장착 기준점 연결 실패");
            }
            AmmoDefinition ammo = ammunition.First(a => a.ammoType == definitions[index].ammoType);
            for (int i = 0; i < 4; i++)
            {
                if (views[i].Instance != null) pools.TryReturn(views[i]);
                views[i] = leases[i];
                weaponViews[i] = nextViews[i];
                weapons[i] = new WeaponInstance(new RuntimeWeapon(definitions[index], 1, definitions[index].weaponId, 0, 0, 0));
                weapons[i].AmmoState.Load(ammo, definitions[index].magazineCapacity);
                pools.Activate(views[i]);
                stations[i].ApplyPose();
            }
            selected = index;
            SelectedWeaponId = definitions[index].weaponId;
            pendingShot = awaitingFrame = false;
            nextShot = Time.time + 0.5f;
            Status = definitions[index].displayName;
            Debug.Log("WeaponShowcase equipped " + SelectedWeaponId + " on all four stations", this);
            return true;
        }
        catch (Exception exception)
        {
            foreach (var lease in leases) if (lease.Instance != null) pools.TryReturn(lease);
            Status = exception.Message;
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void Update()
    {
        if (!ready) return;
        // 작은 Game 창에서도 상단 패널과 네 카메라의 표시 영역을 분리한다.
        float viewportHeight = Mathf.Clamp01(1f - 154f / Mathf.Max(200, Screen.height));
        for (int i = 0; stationCameras != null && i < stationCameras.Length; i++)
            stationCameras[i].rect = new Rect(i * 0.25f, 0, 0.25f, viewportHeight);
        if (awaitingFrame && Time.time > frameDeadline)
        {
            awaitingFrame = false;
            Status = "Shot animation event was not received";
        }
        if (automaticShot && Time.time >= nextShot) RequestShot();
    }

    public void RequestShot()
    {
        if (!ready || awaitingFrame || Time.time < nextShot) return;
        awaitingFrame = true;
        frameDeadline = Time.time + 2f;
        // 기존 플레이어 Controller의 사격 이벤트를 그대로 받아 총구를 읽는 시점을 맞춘다.
        stations[3].animator.SetTrigger("Fire");
        nextShot = Time.time + Mathf.Max(0.8f, 1f / definitions[selected].fireRate);
    }

    public void OnShotFrame()
    {
        if (!awaitingFrame) return;
        awaitingFrame = false;
        pendingShot = true;
    }

    private void LateUpdate()
    {
        if (!ready || !pendingShot) return;
        pendingShot = false;
        var weapon = weapons[3];
        var ammo = ammunition.First(a => a.ammoType == weapon.RuntimeWeapon.Definition.ammoType);
        // 외형 반복 시험에서는 빈 탄창만 보충한다. 실제 인벤토리 장전 검증과 구분한다.
        if (!weapon.AmmoState.CanFire) weapon.AmmoState.Load(ammo, weapon.AmmoState.RemainingCapacity);
        var muzzle = weaponViews[3].MuzzleTransform;
        if (projectiles.TrySpawnShot(weapon.RuntimeWeapon, ammo, muzzle.position, muzzle.forward,
            15f, stations[3].gameObject, ~0, DamageStatSnapshot.Identity, pools))
        {
            weapon.AmmoState.TryConsumeRound(out _);
            Fired++;
        }
    }

    private void OnGUI()
    {
        // 한 패널 안에 조작을 모으되 비교 구역과 겹치지 않는 상단 공간만 사용한다.
        float width = Mathf.Min(760, Screen.width - 24);
        GUILayout.BeginArea(new Rect(12, 8, width, 146), GUI.skin.box);
        GUILayout.Label("Weapon Showcase  |  " + Status);
        GUI.enabled = ready;
        GUILayout.BeginHorizontal();
        if (definitions != null)
            for (int i = 0; i < definitions.Length; i++)
                if (GUILayout.Button(definitions[i].displayName, GUILayout.Height(30))) SelectWeapon(i);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        automaticShot = GUILayout.Toggle(automaticShot, "Repeat shot");
        if (GUILayout.Button("Shot once")) RequestShot();
        if (GUILayout.Button("Front view")) SetCamera(false);
        if (GUILayout.Button("Side view")) SetCamera(true);
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        GUILayout.Label("Idle / Run in place / Aim / Shot  |  Shots: " + Fired);
        GUILayout.Label("Run = walk x1.4. Shot = aim pose + fire event.");
        GUILayout.EndArea();
        // 좁은 카메라의 시야 밖으로 잘리는 월드 글자 대신 각 화면 아래에 자세를 표시한다.
        for (int i = 0; i < 4; i++)
            GUI.Label(new Rect(i * Screen.width * 0.25f + 8, Screen.height - 24, Screen.width * 0.25f - 16, 24), PoseLabels[i]);
    }

    private void SetCamera(bool side)
    {
        for (int i = 0; stationCameras != null && i < stationCameras.Length; i++)
        {
            Vector3 center = stations[i].transform.root.position + new Vector3(0, 0.55f, 0);
            stationCameras[i].transform.position = center + (side ? new Vector3(3, 1, 0.8f) : new Vector3(1.6f, 0.9f, 3));
            stationCameras[i].transform.LookAt(center);
        }
    }

    private async void OnDestroy()
    {
        Application.runInBackground = previousBackground;
        ready = false;
        lifetime?.Cancel();
        try
        {
            // 로딩 중 종료도 초기화의 되돌리기가 끝난 뒤 풀과 핸들을 해제한다.
            if (initialization != null)
                try { await initialization; } catch (OperationCanceledException) { }
            await pools.ShutdownAsync();
            await assets.ShutdownAsync(CancellationToken.None);
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { lifetime?.Dispose(); }
    }
}
