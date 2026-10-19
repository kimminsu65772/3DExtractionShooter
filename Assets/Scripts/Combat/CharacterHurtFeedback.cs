using System.Collections.Generic;
using UnityEngine;

// 원본 HurtVisual의 순간 발광과 피격 FX를 우리 체력 이벤트에 연결한다.
[DisallowMultipleComponent]
public sealed class CharacterHurtFeedback : MonoBehaviour
{
    [Header("피격 대상")]
    [SerializeField] private HealthComponent health;
    [Tooltip("비워 두면 자식의 MeshRenderer와 SkinnedMeshRenderer를 한 번 수집한다. 무기를 제외하려면 직접 지정한다.")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("순간 발광")]
    [SerializeField, ColorUsage(false, true)] private Color flashColor = Color.white;
    [SerializeField, Min(0.01f)] private float hurtCoolSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float hurtValueMultiplier = 1f;

    [Header("피격 FX (선택)")]
    [SerializeField] private GameObject hitFx;
    [SerializeField] private GameObject hitFxNoBlood;
    [SerializeField] private bool useBloodFx = true;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    private readonly List<FlashSlot> slots = new List<FlashSlot>();
    private float hurtValue;

    private void Reset()
    {
        health = GetComponentInParent<HealthComponent>();
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponentInParent<HealthComponent>();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        HashSet<Renderer> collected = new HashSet<Renderer>();
        foreach (Renderer target in targetRenderers)
        {
            if (target == null || (!(target is MeshRenderer) && !(target is SkinnedMeshRenderer)))
                continue;
            if (!collected.Add(target))
                continue;

            Material[] materials = target.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                string shaderName = material.shader.name;
                bool isUrpLit = shaderName == "Universal Render Pipeline/Lit" ||
                                shaderName == "Universal Render Pipeline/Simple Lit";
                bool supported = isUrpLit || shaderName.StartsWith("MK/Toon/") ||
                                 shaderName == "ExtractionShooter/Restored Pato Chapo Character";
                if (!supported || !material.HasProperty(EmissionColorId))
                {
                    Debug.LogWarning($"피격 발광을 지원하지 않는 셰이더: {shaderName} ({target.name})", target);
                    continue;
                }

                // URP Lit의 발광은 키워드가 필요하다. 공유 에셋 대신 이 슬롯의 복제본만 바꾼다.
                Material runtimeMaterial = null;
                if (isUrpLit && !material.IsKeywordEnabled("_EMISSION"))
                {
                    runtimeMaterial = new Material(material) { name = material.name + " (Hurt Feedback)" };
                    runtimeMaterial.EnableKeyword("_EMISSION");
                    runtimeMaterial.SetColor(EmissionColorId, Color.black);
                    materials[i] = runtimeMaterial;
                }

                slots.Add(new FlashSlot(target, i, material, runtimeMaterial));
            }
            target.sharedMaterials = materials;
        }
    }

    private void OnEnable()
    {
        if (health == null)
        {
            Debug.LogError("CharacterHurtFeedback에 HealthComponent를 연결해야 한다.", this);
            return;
        }
        health.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamageTaken -= HandleDamageTaken;

        RestoreFlash();
    }

    private void HandleDamageTaken(DamageInfo damageInfo)
    {
        // 연속 피격은 효과를 겹치지 않고 같은 강도에서 다시 감쇠한다.
        if (hurtValue <= 0f)
        {
            foreach (FlashSlot slot in slots)
                slot.Capture();
        }

        hurtValue = 1f;
        ApplyFlash();

        GameObject prefab = !useBloodFx && hitFxNoBlood != null ? hitFxNoBlood : hitFx;
        if (prefab == null)
            return;

        // 우리 DamageInfo는 충돌 법선 대신 탄환 진행 방향을 제공한다. 입사 반대쪽으로 FX를 향한다.
        Vector3 normal = -damageInfo.hitDirection;
        Quaternion rotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.FromToRotation(Vector3.forward, normal.normalized)
            : Quaternion.identity;
        Instantiate(prefab, damageInfo.hitPoint, rotation);
    }

    private void Update()
    {
        if (hurtValue <= 0f)
            return;

        // 원본과 같이 시간 배율과 무관하게 약 1 / 8초 동안 원래 색으로 돌아간다.
        hurtValue = Mathf.Max(0f, hurtValue - Time.unscaledDeltaTime * hurtCoolSpeed);
        if (hurtValue > 0f)
            ApplyFlash();
        else
            RestoreFlash();
    }

    private void ApplyFlash()
    {
        float strength = hurtValue * hurtValueMultiplier;
        foreach (FlashSlot slot in slots)
            slot.Apply(flashColor, strength);
    }

    private void RestoreFlash()
    {
        foreach (FlashSlot slot in slots)
            slot.Restore();
        hurtValue = 0f;
    }

    private void OnDestroy()
    {
        foreach (FlashSlot slot in slots)
            slot.Release();
    }

    // 머티리얼 슬롯마다 원래 발광색과 프로퍼티 블록을 보존한다.
    private sealed class FlashSlot
    {
        private readonly Renderer renderer;
        private readonly int index;
        private readonly Material originalMaterial;
        private readonly Material runtimeMaterial;
        private readonly MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock workingBlock = new MaterialPropertyBlock();
        private Color originalEmission;
        private bool captured;

        public FlashSlot(Renderer renderer, int index, Material originalMaterial, Material runtimeMaterial)
        {
            this.renderer = renderer;
            this.index = index;
            this.originalMaterial = originalMaterial;
            this.runtimeMaterial = runtimeMaterial;
        }

        public void Capture()
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(originalBlock, index);
            // 슬롯별 블록이 없으면 렌더러 전체에 설정된 다른 표현 값도 이어받는다.
            if (originalBlock.isEmpty)
                renderer.GetPropertyBlock(workingBlock);
            else
                renderer.GetPropertyBlock(workingBlock, index);

            originalEmission = workingBlock.HasColor(EmissionColorId)
                ? workingBlock.GetColor(EmissionColorId)
                : (runtimeMaterial != null ? runtimeMaterial : originalMaterial).GetColor(EmissionColorId);
            captured = true;
        }

        public void Apply(Color color, float strength)
        {
            if (renderer == null || !captured)
                return;

            workingBlock.SetColor(EmissionColorId, Color.Lerp(originalEmission, color, strength));
            // 발광 마스크가 검은 부위도 함께 반짝이게 한다. 종료 시 원래 블록과 마스크로 복원한다.
            workingBlock.SetTexture(EmissionMapId, Texture2D.whiteTexture);
            renderer.SetPropertyBlock(workingBlock, index);
        }

        public void Restore()
        {
            if (renderer != null && captured)
                renderer.SetPropertyBlock(originalBlock.isEmpty ? null : originalBlock, index);
            captured = false;
        }

        public void Release()
        {
            if (runtimeMaterial == null)
                return;

            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;
                if (index < materials.Length && materials[index] == runtimeMaterial)
                {
                    materials[index] = originalMaterial;
                    renderer.sharedMaterials = materials;
                }
            }
            Object.Destroy(runtimeMaterial);
        }
    }
}
