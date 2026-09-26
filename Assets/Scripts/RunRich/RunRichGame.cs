using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RunRich3D.GameEngine;
using Zenject;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RunRichClone
{
    public enum PickupKind
    {
        Money,
        Alcohol,
        Obstacle,
        GoodGate,
        BadGate,
        Finish
    }

    public sealed class RunRichGame : MonoBehaviour
    {
        [Header("Original imported assets")]
        [SerializeField] private GameObject playerModel;
        [SerializeField] private Material playerMaterial;
        [SerializeField] private GameObject moneyModel;
        [SerializeField] private GameObject bottleModel;
        [SerializeField] private GameObject doorModel;
        [SerializeField] private Mesh partyGateMesh;
        [SerializeField] private Mesh schoolGateMesh;
        [SerializeField] private Material groundAssetMaterial;
        [SerializeField] private Material waterAssetMaterial;
        [SerializeField] private Material goodDoorAssetMaterial;
        [SerializeField] private Material badDoorAssetMaterial;
        [SerializeField] private Material finishAssetMaterial;
        [SerializeField] private Material checkpointAssetMaterial;
        [SerializeField] private Material badItemAssetMaterial;
        [SerializeField] private Material moneyAssetMaterial;
        [SerializeField] private Material gateIconAssetMaterial;
        [SerializeField] private Material positiveEffectMaterial;
        [SerializeField] private Material negativeEffectMaterial;
        [SerializeField] private Material moneyGlowParticleMaterial;
        [SerializeField] private Material moneySparkleParticleMaterial;
        [SerializeField] private Sprite progressFrameSprite;
        [SerializeField] private Sprite progressCircleSprite;
        [SerializeField] private Sprite poorProgressIcon;
        [SerializeField] private Sprite richProgressIcon;
        [SerializeField] private AudioClip moneySound;
        [SerializeField] private AudioClip badSound;
        [SerializeField] private AudioClip winSound;
        [SerializeField] private AudioClip walkingSoundLeft;
        [SerializeField] private AudioClip walkingSoundRight;
        [SerializeField] private AudioClip bonusMultiplierSound;

        public bool IsRunning => controller != null && controller.State.IsRunning;

        private const float FinishZ = 91f;
        private const float FlagStraightenDistance = 6f;
        private const float FlagStraightenSpeed = 120f;
        private readonly List<RunRichPickup> pickups = new List<RunRichPickup>();
        private readonly List<Transform> flags = new List<Transform>();
        private readonly Dictionary<Transform, Quaternion> initialFlagRotations = new Dictionary<Transform, Quaternion>();

        [Header("Scene References - editable in Inspector")]
        [SerializeField] private Transform levelRoot;
        [SerializeField] private RunRichPlayer player;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private AudioSource audioSource;

        [Header("Canvas References - editable in Inspector")]
        [SerializeField] private Canvas startCanvas;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Text levelText;
        [SerializeField] private Text progressText;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text tutorialText;
        [SerializeField] private Text swipeArrows;
        [SerializeField] private Text resultText;
        [SerializeField] private Text continueText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image wealthFill;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform hand;
        private RunRichGameController controller;
        private int wealth => controller != null ? controller.State.Wealth : 50;
        private int currentLevel => controller != null ? controller.State.Level : 1;
        private bool completed => controller != null && controller.State.IsCompleted;
        private bool failed => controller != null && controller.State.IsFailed;
        private bool transitioning => controller != null && controller.State.IsTransitioning;
        private float tutorialPhase;
        private float walkingSoundTimer;
        private bool useRightWalkingSound;
        private bool flagsStraightened;
        private readonly Dictionary<string, GameObject> outfits = new Dictionary<string, GameObject>();
        private readonly Dictionary<Material, Material> compatibleMaterials = new Dictionary<Material, Material>();

        private Material whiteMaterial;
        private Material blueMaterial;
        private Material cyanMaterial;
        private Material greenMaterial;
        private Material redMaterial;
        private Material yellowMaterial;
        private Material blackMaterial;
        private Material purpleMaterial;
        private Material gateIconMaterial;

        [Inject]
        private void Construct(RunRichGameController gameController)
        {
            controller = gameController;
            controller.StateChanged += RefreshStateUi;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            if (controller == null)
            {
                Debug.LogError("RunRichGame was not injected. Add SceneContext and RunRichSceneInstaller to the scene.");
                enabled = false;
                return;
            }
            if (!HasBakedScene())
            {
                Debug.LogError("RunRichLevel must use the authored scene hierarchy. Rebuild it from Run Rich/Build First Level.");
                enabled = false;
                return;
            }

            player.Game = this;
            var cameraController = gameCamera.GetComponent<RunRichCamera>();
            if (cameraController == null)
                cameraController = gameCamera.gameObject.AddComponent<RunRichCamera>();
            cameraController.SetTarget(player.transform);
            if (player.VisualRoot == null && player.transform.childCount > 0)
                player.VisualRoot = player.transform.GetChild(0);
            ApplyPlayerMaterial(player.VisualRoot);
            CacheOutfits(player.VisualRoot, false);
            player.CacheModel();
            pickups.Clear();
            pickups.AddRange(levelRoot.GetComponentsInChildren<RunRichPickup>(true));
            ConfigureMoneyPickups();
            CacheFlags();
            ResetLevel();
        }

        private void OnDestroy()
        {
            if (controller != null)
                controller.StateChanged -= RefreshStateUi;
        }

        private bool HasBakedScene()
        {
            return levelRoot != null && player != null && gameCamera != null && startCanvas != null && hudCanvas != null &&
                   levelText != null && progressText != null && tutorialText != null && wealthFill != null &&
                   playerMaterial != null && walkingSoundLeft != null && walkingSoundRight != null && bonusMultiplierSound != null &&
                   positiveEffectMaterial != null && negativeEffectMaterial != null &&
                   moneyGlowParticleMaterial != null && moneySparkleParticleMaterial != null;
        }

#if UNITY_EDITOR
        public void BakeSceneLayoutInEditor()
        {
            if (playerModel == null)
                throw new MissingReferenceException("Player Model must be assigned in the Inspector before baking the scene.");

            BuildMaterials();
            BuildWorld();
            BuildHud();
            pickups.Clear();
            pickups.AddRange(levelRoot.GetComponentsInChildren<RunRichPickup>(true));
            ConfigureMoneyPickups();
            EditorUtility.SetDirty(this);
        }
#endif

        private void Update()
        {
            if (player == null)
                return;

            if (progressText != null)
                progressText.text = wealth.ToString();

            UpdateWalkingSound();
            UpdateFlagRotation();

            if (!IsRunning && !completed && hand != null)
            {
                tutorialPhase += Time.deltaTime * 2.8f;
                hand.anchoredPosition = new Vector2(Mathf.Sin(tutorialPhase) * 90f, 125f);
            }

            var touchBegan = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
            if (completed && !transitioning && (Input.GetMouseButtonDown(0) || touchBegan) && controller.TryBeginTransition())
            {
                StartCoroutine(failed ? RestartAfterResult() : LoadNextLevel());
            }
        }

        public void StartRun()
        {
            if (!controller.TryStartRun())
                return;

            startCanvas.gameObject.SetActive(false);
            hudCanvas.gameObject.SetActive(true);
            walkingSoundTimer = 0.05f;
        }

        public void HandlePickup(RunRichPickup pickup)
        {
            switch (pickup.kind)
            {
                case PickupKind.Money:
                    ChangeWealth(Mathf.Abs(pickup.value), pickup.transform.position, true);
                    controller.AddCoin();
                    PlaySound(moneySound, 0.85f);
                    SpawnMoneyClaimEffect(pickup.transform.position, 9);
                    pickup.gameObject.SetActive(false);
                    break;
                case PickupKind.Alcohol:
                case PickupKind.Obstacle:
                case PickupKind.BadGate:
                    ChangeWealth(-Mathf.Abs(pickup.value), pickup.transform.position, false);
                    PlaySound(badSound, 0.9f);
                    SpawnColorBurst(pickup.transform.position, negativeEffectMaterial, new Color(1f, 0.12f, 0.08f), 10);
                    if (pickup.kind != PickupKind.BadGate)
                        pickup.gameObject.SetActive(false);
                    else
                        DisableGatePair(pickup);
                    break;
                case PickupKind.GoodGate:
                    ChangeWealth(Mathf.Abs(pickup.value), pickup.transform.position, true);
                    SpawnMoneyClaimEffect(pickup.transform.position, 14);
                    DisableGatePair(pickup);
                    break;
                case PickupKind.Finish:
                    FinishLevel();
                    break;
            }
        }

        private void DisableGatePair(RunRichPickup gate)
        {
            if (gate.transform.parent == null)
                return;
            foreach (var sibling in gate.transform.parent.GetComponentsInChildren<RunRichPickup>())
                sibling.collected = true;
        }

        private void ChangeWealth(int delta, Vector3 worldPosition, bool positive)
        {
            controller.ChangeWealth(delta);
            ShowFloatingText(worldPosition + Vector3.up * 1.8f, positive ? $"+${Mathf.Abs(delta)}" : $"-${Mathf.Abs(delta)}", positive ? Color.green : Color.red);
            UpdateOutfit();
            if (wealth <= 0 && delta < 0)
                FailLevel();
        }

        private void FailLevel()
        {
            if (!controller.TryFailLevel())
                return;

            resultText.text = "ВЫ ПРОИГРАЛИ";
            resultText.color = new Color(1f, 0.16f, 0.12f);
            resultText.gameObject.SetActive(true);
            continueText.text = "НАЖМИТЕ, ЧТОБЫ ПОВТОРИТЬ";
            continueText.gameObject.SetActive(true);
            PlaySound(badSound, 1f);
        }

        private void FinishLevel()
        {
            if (!controller.TryCompleteLevel())
                return;

            resultText.text = $"УРОВЕНЬ {currentLevel} ЗАВЕРШЕНО\nВЫ ПОБЕДИЛИ!";
            resultText.color = wealth >= 65 ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.8f, 0.1f);
            resultText.gameObject.SetActive(true);
            continueText.gameObject.SetActive(true);
            PlaySound(winSound, 1f);
            StartCoroutine(player.Celebrate());
            StartCoroutine(SaveCompletedLevel());
        }

        private IEnumerator SaveCompletedLevel()
        {
            yield return null;
            controller.SaveCompletedLevel();
        }

        private IEnumerator LoadNextLevel()
        {
            continueText.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);
            controller.AdvanceLevel();
            ResetLevel();
        }

        private IEnumerator RestartAfterResult()
        {
            continueText.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.2f);
            controller.RestartCurrentLevel();
            ResetLevel();
        }

        private void ResetLevel()
        {
            StopAllCoroutines();
            controller.ResetLevel();
            levelText.text = $"Уровень {currentLevel}";
            resultText.gameObject.SetActive(false);
            continueText.gameObject.SetActive(false);
            continueText.text = "НАЖМИТЕ, ЧТОБЫ ПРОДОЛЖИТЬ";
            startCanvas.gameObject.SetActive(true);
            hudCanvas.gameObject.SetActive(false);
            progressFill.fillAmount = Mathf.Clamp01((currentLevel - 1f) / 4f);
            wealthFill.fillAmount = wealth / 100f;
            player.ResetPlayer();
            UpdateOutfit();
            flagsStraightened = false;
            foreach (var flag in flags)
            {
                if (flag != null && initialFlagRotations.TryGetValue(flag, out var initialRotation))
                    flag.localRotation = initialRotation;
            }

            foreach (var pickup in pickups)
            {
                pickup.collected = false;
                pickup.gameObject.SetActive(true);
            }
        }

        private void ConfigureMoneyPickups()
        {
            foreach (var pickup in pickups)
            {
                if (pickup != null && pickup.kind == PickupKind.Money)
                    pickup.value = 2;
            }
        }

        private void CacheFlags()
        {
            flags.Clear();
            initialFlagRotations.Clear();
            foreach (var child in levelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Flag" && child.name != "Flag(1)" && child.name != "Flag (1)")
                    continue;

                flags.Add(child);
                initialFlagRotations[child] = child.localRotation;
            }
        }

        private void UpdateFlagRotation()
        {
            if (!IsRunning || flags.Count == 0)
                return;

            if (!flagsStraightened)
            {
                var distanceToFlags = flags[0].position.z - player.transform.position.z;
                if (distanceToFlags > FlagStraightenDistance)
                    return;

                flagsStraightened = true;
                PlaySound(bonusMultiplierSound, 0.9f);
            }

            foreach (var flag in flags)
            {
                if (flag == null)
                    continue;

                flag.localRotation = Quaternion.RotateTowards(
                    flag.localRotation,
                    Quaternion.identity,
                    FlagStraightenSpeed * Time.deltaTime);
            }
        }

        private void RefreshStateUi(RunRichGameState state)
        {
            if (wealthFill != null)
                wealthFill.fillAmount = state.Wealth / 100f;
            if (coinsText != null)
                coinsText.text = state.Coins.ToString();
            if (progressText != null)
                progressText.text = state.Wealth.ToString();
            if (levelText != null)
                levelText.text = $"Уровень {state.Level}";
        }

        private void BuildMaterials()
        {
            whiteMaterial = MakeUrpCompatible(groundAssetMaterial, "Track Ground", new Color(0.91f, 0.95f, 1f));
            blueMaterial = MakeUrpCompatible(waterAssetMaterial, "Ocean Water", new Color(0.12f, 0.62f, 0.95f));
            cyanMaterial = MakeUrpCompatible(checkpointAssetMaterial, "Checkpoint", new Color(0.35f, 0.92f, 1f));
            greenMaterial = MakeUrpCompatible(moneyAssetMaterial, "Money", new Color(0.15f, 0.83f, 0.28f));
            redMaterial = MakeUrpCompatible(badItemAssetMaterial, "Bad Item", new Color(0.93f, 0.16f, 0.12f));
            positiveEffectMaterial = greenMaterial;
            negativeEffectMaterial = redMaterial;
            moneyGlowParticleMaterial = MakeUrpParticleCompatible(moneyGlowParticleMaterial, "Money Glow Particle");
            moneySparkleParticleMaterial = MakeUrpParticleCompatible(moneySparkleParticleMaterial, "Money Sparkle Particle");
            yellowMaterial = MakeUrpCompatible(checkpointAssetMaterial, "Status", new Color(1f, 0.71f, 0.05f));
            blackMaterial = MakeMaterial("Checker Black", new Color(0.04f, 0.04f, 0.05f));
            purpleMaterial = MakeUrpCompatible(finishAssetMaterial, "Finish", new Color(0.72f, 0.16f, 0.65f));
            gateIconMaterial = MakeUrpCompatible(gateIconAssetMaterial, "Gate Icon", Color.white);
        }

        private Material MakeUrpCompatible(Material source, string materialName, Color fallbackColor)
        {
            if (source == null)
                return MakeMaterial(materialName, fallbackColor);
            if (compatibleMaterials.TryGetValue(source, out var cached))
                return cached;

            var sourceShaderName = source.shader != null ? source.shader.name : string.Empty;
            if (sourceShaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
            {
                compatibleMaterials[source] = source;
                return source;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
#if UNITY_EDITOR
            var converted = GetOrCreateProjectMaterial(materialName, source, shader);
#else
            var converted = new Material(shader) { name = source.name + " (URP)" };
#endif
            var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                source.HasProperty("_Color") ? source.GetColor("_Color") : fallbackColor;
            if (texture != null)
            {
                if (converted.HasProperty("_BaseMap"))
                    converted.SetTexture("_BaseMap", texture);
                converted.mainTexture = texture;
            }
            if (converted.HasProperty("_BaseColor"))
                converted.SetColor("_BaseColor", color);
            converted.color = color;
            if (converted.HasProperty("_Smoothness"))
                converted.SetFloat("_Smoothness", 0.12f);
#if UNITY_EDITOR
            EditorUtility.SetDirty(converted);
#endif
            compatibleMaterials[source] = converted;
            return converted;
        }

        private void ApplyCompatibleMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = MakeUrpCompatible(materials[i], materials[i] != null ? materials[i].name : "Model Material", Color.white);
                renderer.sharedMaterials = materials;
            }
        }

        private Material MakeMaterial(string materialName, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
#if UNITY_EDITOR
            var material = GetOrCreateProjectMaterial(materialName, null, shader);
#else
            var material = new Material(shader) { name = materialName };
#endif
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.15f);
#if UNITY_EDITOR
            EditorUtility.SetDirty(material);
#endif
            return material;
        }

        private Material MakeUrpParticleCompatible(Material source, string materialName)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
#if UNITY_EDITOR
            var material = GetOrCreateProjectMaterial(materialName, source, shader);
#else
            var material = source;
#endif
            if (material == null)
                return null;

#if UNITY_EDITOR
            var texture = GetSavedParticleTexture(source);
#else
            var texture = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") :
                source != null && source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
#endif
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex"))
                    material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha"))
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha"))
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
#if UNITY_EDITOR
            EditorUtility.SetDirty(material);
#endif
            return material;
        }

#if UNITY_EDITOR
        private static Texture GetSavedParticleTexture(Material source)
        {
            if (source == null)
                return null;

            var properties = new SerializedObject(source).FindProperty("m_SavedProperties.m_TexEnvs");
            if (properties == null)
                return null;

            foreach (var propertyName in new[] { "_BaseMap", "_MainTex" })
            {
                for (var i = 0; i < properties.arraySize; i++)
                {
                    var entry = properties.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("first").stringValue != propertyName)
                        continue;
                    var texture = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                    if (texture != null)
                        return texture;
                }
            }

            return null;
        }
#endif

#if UNITY_EDITOR
        private const string GeneratedMaterialFolder = "Assets/RunRich/Materials";

        private static Material GetOrCreateProjectMaterial(string materialName, Material source, Shader shader)
        {
            EnsureGeneratedMaterialFolder();
            var suffix = string.Empty;
            if (source != null)
            {
                var sourcePath = AssetDatabase.GetAssetPath(source);
                var guid = AssetDatabase.AssetPathToGUID(sourcePath);
                if (!string.IsNullOrEmpty(guid))
                    suffix = "_" + guid.Substring(0, 8);
            }

            var assetName = SanitizeAssetName(materialName) + suffix;
            var assetPath = $"{GeneratedMaterialFolder}/{assetName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static void EnsureGeneratedMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RunRich"))
                AssetDatabase.CreateFolder("Assets", "RunRich");
            if (!AssetDatabase.IsValidFolder(GeneratedMaterialFolder))
                AssetDatabase.CreateFolder("Assets/RunRich", "Materials");
        }

        private static string SanitizeAssetName(string value)
        {
            foreach (var invalidCharacter in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter, '_');
            return value.Replace('/', '_').Replace('\\', '_');
        }
#endif

        private void BuildWorld()
        {
            RenderSettings.ambientLight = new Color(0.72f, 0.82f, 0.9f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.38f, 0.88f, 1f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 125f;

            levelRoot = new GameObject("LEVEL 1 - Runtime").transform;
            CreateLight();
            CreateTrack();
            CreatePlayer();
            CreateCamera();
            CreateLevelContent();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        private void CreateLight()
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(levelRoot);
            lightObject.transform.rotation = Quaternion.Euler(50f, -32f, 0f);
            var lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.25f;
            lightComponent.color = new Color(1f, 0.96f, 0.9f);
            lightComponent.shadows = LightShadows.Soft;
        }

        private void CreateTrack()
        {
            CreateCube("Ocean", new Vector3(0f, -1.25f, 58f), new Vector3(90f, 1.5f, 145f), blueMaterial, levelRoot);
            CreateCube("Main Track", new Vector3(0f, -0.2f, 49f), new Vector3(7f, 0.35f, 100f), whiteMaterial, levelRoot);

            for (var z = 4; z < 90; z += 10)
            {
                CreateCube("Track Accent", new Vector3(0f, 0.001f, z), new Vector3(7f, 0.015f, 0.11f), cyanMaterial, levelRoot);
            }

            for (var x = -3; x <= 3; x++)
            {
                var material = ((x + 3) % 2 == 0) ? whiteMaterial : blackMaterial;
                CreateCube("Finish Tile", new Vector3(x + 0.5f, 0.04f, FinishZ), new Vector3(1f, 0.08f, 1f), material, levelRoot);
            }

            CreateFinishArch();
        }

        private void CreatePlayer()
        {
            var playerObject = new GameObject("Player");
            playerObject.transform.SetParent(levelRoot);
            player = playerObject.AddComponent<RunRichPlayer>();
            player.Game = this;

            var visual = Instantiate(playerModel, playerObject.transform);
            visual.name = "Original Run Rich Character";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            ApplyCompatibleMaterials(visual);
            ApplyPlayerMaterial(visual.transform);

            player.VisualRoot = visual.transform;
            CacheOutfits(visual.transform, true);
            player.CacheModel();
        }

        private void CacheOutfits(Transform visualRoot, bool disableAll)
        {
            outfits.Clear();
            if (visualRoot == null)
                return;

            foreach (var renderer in visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var outfitName = renderer.gameObject.name.ToLowerInvariant();
                outfits[outfitName] = renderer.gameObject;
                if (disableAll)
                    renderer.gameObject.SetActive(false);
            }
        }

        private void CreateCamera()
        {
            var cameraObject = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 4.8f, -5.7f);
            gameCamera = cameraObject.GetComponent<Camera>();
            if (gameCamera == null)
                gameCamera = cameraObject.AddComponent<Camera>();
            if (cameraObject.GetComponent<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
            gameCamera.fieldOfView = 49f;
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = new Color(0.36f, 0.91f, 1f);
            gameCamera.nearClipPlane = 0.05f;
            gameCamera.farClipPlane = 180f;
        }

        private void CreateLevelContent()
        {
            CreateMoneyRow(5f, -2.1f, 5, 0.88f);
            CreateMoneyRow(11f, 1.8f, 4, 0.85f);
            CreateBottle(-1.65f, 14.5f);
            CreateBottle(1.6f, 17.5f, 16);
            CreateMoneyRow(20f, -1.7f, 5, 0.8f);
            CreateBottle(-1.2f, 26f, 16);
            CreateBottle(1.3f, 26f, 16);
            CreateMoneyRow(29f, 2f, 4, 0.82f);
            CreateChoiceGates(38f);
            CreateMoneyRow(46f, 0f, 6, 0.75f);
            CreateBottle(1.8f, 50f);
            CreateFlagZone(56f);
            CreateBottle(-1.7f, 61f, 16);
            CreateMoneyRow(64f, 1.8f, 5, 0.82f);
            CreateBottle(-1.6f, 70f, 14);
            CreateBottle(1.6f, 73f, 14);
            CreateMoneyRow(76f, 0f, 6, 0.75f);
            CreateBottle(0f, 82f, 16);
            CreateMoneyRow(84f, -2f, 5, 0.82f);

            var finishTrigger = new GameObject("Finish Trigger");
            finishTrigger.transform.SetParent(levelRoot);
            finishTrigger.transform.position = new Vector3(0f, 1f, FinishZ + 0.2f);
            var collider = finishTrigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(7f, 3f, 0.8f);
            var pickup = finishTrigger.AddComponent<RunRichPickup>();
            pickup.kind = PickupKind.Finish;
            pickups.Add(pickup);
        }

        private void CreateMoneyRow(float z, float centerX, int count, float spacing)
        {
            for (var i = 0; i < count; i++)
            {
                var x = Mathf.Clamp(centerX + (i - (count - 1) * 0.5f) * spacing, -2.7f, 2.7f);
                CreateMoney(x, z + Mathf.Abs(i - count * 0.5f) * 0.22f);
            }
        }

        private void CreateMoney(float x, float z)
        {
            GameObject item;
            if (moneyModel != null)
            {
                item = Instantiate(moneyModel, levelRoot);
                item.transform.localScale = Vector3.one * 1.5f;
                ApplyCompatibleMaterials(item);
            }
            else
            {
                item = CreateCube("Money", Vector3.zero, new Vector3(0.65f, 0.12f, 0.4f), greenMaterial, levelRoot);
            }
            item.name = "Money +2";
            item.transform.position = new Vector3(x, 0.48f, z);
            item.transform.rotation = Quaternion.Euler(-12f, UnityEngine.Random.Range(-15f, 15f), 0f);
            CreateWorldLabel("+", item.transform, new Vector3(0f, 0.20f, 0f), new Color(0.15f, 1f, 0.25f), 0.01f);
            EnsureTrigger(item, new Vector3(0.75f, 1f, 0.75f));
            var pickup = item.AddComponent<RunRichPickup>();
            pickup.kind = PickupKind.Money;
            pickup.value = 2;
            pickups.Add(pickup);
        }

        private void CreateBottle(float x, float z, int penalty = 10)
        {
            GameObject item;
            if (bottleModel != null)
            {
                item = Instantiate(bottleModel, levelRoot);
                item.transform.localScale = Vector3.one * 0.8f;
                ApplyCompatibleMaterials(item);
            }
            else
            {
                item = CreateCylinder("Alcohol", Vector3.zero, new Vector3(0.36f, 0.85f, 0.36f), redMaterial, levelRoot);
            }
            item.name = $"Bottle -{penalty}";
            item.transform.position = new Vector3(x, 0.45f, z);
            CreateWorldLabel("−", item.transform, new Vector3(0f, 1.15f, 0f), new Color(1f, 0.15f, 0.1f), 0.01f);
            EnsureCapsuleTrigger(item);
            var pickup = item.AddComponent<RunRichPickup>();
            pickup.kind = PickupKind.Alcohol;
            pickup.value = penalty;
            pickups.Add(pickup);
        }

        private void CreateChoiceGates(float z)
        {
            var pair = new GameObject("Choice Gates");
            pair.transform.SetParent(levelRoot);
            CreateGate(pair.transform, -1.15f, z, "ВЕЧЕРИНКА", partyGateMesh,
                MakeUrpCompatible(badDoorAssetMaterial, "Bad Door", new Color(0.95f, 0.12f, 0.08f)), PickupKind.BadGate, 20);
            CreateGate(pair.transform, 1.15f, z, "ШКОЛА", schoolGateMesh,
                MakeUrpCompatible(goodDoorAssetMaterial, "Good Door", new Color(0.1f, 0.9f, 0.25f)), PickupKind.GoodGate, 20);
        }

        private void CreateGate(Transform parent, float x, float z, string label, Mesh iconMesh, Material frameMaterial,
            PickupKind kind, int value)
        {
            var root = new GameObject(label + " Gate");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, 0f, z);

            CreateGatePart("Left Post", new Vector3(-1.05f, 1.4f, 0f), new Vector3(0.2f, 2.8f, 0.2f), frameMaterial, root.transform);
            CreateGatePart("Right Post", new Vector3(1.05f, 1.4f, 0f), new Vector3(0.2f, 2.8f, 0.2f), frameMaterial, root.transform);
            CreateGatePart("Black Header", new Vector3(0f, 2.62f, -0.01f), new Vector3(2.25f, 0.48f, 0.24f), blackMaterial, root.transform);
            CreateWorldLabel(label, root.transform, new Vector3(0f, 2.62f, -0.14f), Color.white, 0.0075f);
            CreateGateIcon(label + " Icon", iconMesh, root.transform);

            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 1.35f, 0f);
            collider.size = new Vector3(2.05f, 2.7f, 0.8f);
            var pickup = root.AddComponent<RunRichPickup>();
            pickup.kind = kind;
            pickup.value = value;
            pickups.Add(pickup);
        }

        private void CreateGatePart(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var part = CreateCube(objectName, position, scale, material, parent);
            part.GetComponent<Collider>().enabled = false;
        }

        private void CreateGateIcon(string objectName, Mesh mesh, Transform parent)
        {
            if (mesh == null)
                return;

            var icon = new GameObject(objectName);
            icon.transform.SetParent(parent, false);
            icon.transform.localPosition = new Vector3(0f, 1.25f, -0.12f);
            icon.transform.localRotation = Quaternion.Euler(10f, 180f, 0f);
            var largestSize = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
            icon.transform.localScale = Vector3.one * (1.15f / Mathf.Max(0.01f, largestSize));
            icon.AddComponent<MeshFilter>().sharedMesh = mesh;
            icon.AddComponent<MeshRenderer>().sharedMaterial = gateIconMaterial;
        }

        private void CreateFlagZone(float z)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * 3.15f;
                CreateCylinder("Flag Pole", new Vector3(x, 1.3f, z), new Vector3(0.08f, 1.3f, 0.08f), yellowMaterial, levelRoot);
                var flag = CreateCube("Flag", new Vector3(x + side * 0.42f, 2.15f, z), new Vector3(0.8f, 0.5f, 0.05f), side < 0 ? redMaterial : greenMaterial, levelRoot);
                flag.transform.rotation = Quaternion.Euler(0f, 0f, side * 6f);
            }
        }

        private void CreateFinishArch()
        {
            if (doorModel != null)
                InstantiateFittedModel(doorModel, "Original Millionaire Finish", levelRoot, new Vector3(0f, 0f, FinishZ + 2f), new Vector3(6.5f, 4.2f, 1.2f), purpleMaterial);
            else
            {
                CreateCube("Finish Left", new Vector3(-3f, 2f, FinishZ + 2f), new Vector3(0.55f, 4f, 0.6f), purpleMaterial, levelRoot);
                CreateCube("Finish Right", new Vector3(3f, 2f, FinishZ + 2f), new Vector3(0.55f, 4f, 0.6f), purpleMaterial, levelRoot);
                CreateCube("Finish Header", new Vector3(0f, 3.75f, FinishZ + 2f), new Vector3(6.5f, 0.65f, 0.65f), purpleMaterial, levelRoot);
            }
            CreateWorldLabel("x2", levelRoot, new Vector3(0f, 4.3f, FinishZ + 1.65f), Color.white, 0.012f);
        }

        private void BuildHud()
        {
            startCanvas = CreateScreenCanvas("Start Progress UI", 20);
            var startRoot = startCanvas.transform;

            var progressBackground = CreatePanel("Progress Bar", startRoot, new Color(0.04f, 0.35f, 0.47f, 0.98f));
            progressBackground.sprite = progressFrameSprite;
            progressBackground.type = progressFrameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            SetRect(progressBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(500f, 112f));

            var progressTrack = CreatePanel("Progress Track", progressBackground.transform, Color.white);
            SetRect(progressTrack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 13f), new Vector2(350f, 27f));
            progressFill = CreateFilledImage("Progress Fill", progressTrack.transform, new Color(0.22f, 0.78f, 0.92f));

            for (var i = 1; i < 5; i++)
            {
                var separator = CreatePanel("Separator " + i, progressTrack.transform, new Color(0.04f, 0.35f, 0.47f, 1f));
                SetRect(separator.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 70f, 0f), new Vector2(4f, 27f));
            }

            CreateProgressMarker("Poor Marker", progressBackground.transform, new Vector2(-210f, 13f), poorProgressIcon);
            CreateProgressMarker("Rich Marker", progressBackground.transform, new Vector2(210f, 13f), richProgressIcon);

            for (var i = 0; i < 5; i++)
            {
                var stage = CreatePanel("Stage " + (i + 1), progressBackground.transform, Color.white);
                stage.sprite = progressCircleSprite;
                SetRect(stage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140f + i * 70f, -31f), new Vector2(34f, 34f));
                var stageText = CreateText("Number", stage.transform, (i + 1).ToString(), 22, TextAnchor.MiddleCenter, new Color(0.04f, 0.09f, 0.12f));
                SetRect(stageText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            hudCanvas = CreateScreenCanvas("Gameplay HUD", 21);
            var hudRoot = hudCanvas.transform;

            levelText = CreateText("Level", hudRoot, "Уровень 1", 42, TextAnchor.UpperCenter, Color.white);
            SetRect(levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(360f, 62f));

            progressText = CreateText("Wealth Number", hudRoot, "50", 62, TextAnchor.UpperCenter, Color.white);
            SetRect(progressText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(250f, 82f));

            var coinPill = CreatePanel("Coin Pill", hudRoot, new Color(0.08f, 0.17f, 0.18f, 0.82f));
            SetRect(coinPill.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-92f, -38f), new Vector2(145f, 58f));
            coinsText = CreateText("Coins", coinPill.transform, "0", 38, TextAnchor.MiddleCenter, Color.white);
            SetRect(coinsText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dollar = CreateText("Dollar", coinPill.transform, "$", 40, TextAnchor.MiddleRight, new Color(0.2f, 1f, 0.35f));
            SetRect(dollar.rectTransform, Vector2.zero, Vector2.one, new Vector2(-12f, 0f), Vector2.zero);

            var tutorialBubble = CreatePanel("Tutorial Bubble", startRoot, new Color(0.16f, 0.16f, 0.17f, 0.82f));
            SetRect(tutorialBubble.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(460f, 76f));
            tutorialText = CreateText("Tutorial", tutorialBubble.transform, "ПРОВЕДИТЕ ПО ЭКРАНУ, ЧТОБЫ\nПЕРЕМЕСТИТЬ", 27, TextAnchor.MiddleCenter, Color.white);
            SetRect(tutorialText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tutorialText.horizontalOverflow = HorizontalWrapMode.Overflow;

            swipeArrows = CreateText("Swipe Arrows", startRoot, "←                          →", 46, TextAnchor.MiddleCenter, Color.white);
            SetRect(swipeArrows.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 125f), new Vector2(430f, 70f));

            var handImage = CreatePanel("Hand", startRoot, new Color(1f, 1f, 1f, 0.95f));
            hand = handImage.rectTransform;
            SetRect(hand, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 125f), new Vector2(58f, 88f));
            var finger = CreatePanel("Finger", hand, new Color(1f, 0.82f, 0.68f, 1f));
            SetRect(finger.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 72f));

            resultText = CreateText("Result", hudRoot, "ВЫ ПОБЕДИЛИ!", 72, TextAnchor.MiddleCenter, Color.white);
            SetRect(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(650f, 230f));
            continueText = CreateText("Continue", hudRoot, "НАЖМИТЕ, ЧТОБЫ ПРОДОЛЖИТЬ", 32, TextAnchor.MiddleCenter, Color.white);
            SetRect(continueText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(500f, 70f));

            startCanvas.gameObject.SetActive(true);
            hudCanvas.gameObject.SetActive(false);
            CreateWorldStatusCanvas();
        }

        private static Canvas CreateScreenCanvas(string objectName, int sortingOrder)
        {
            var canvasObject = new GameObject(objectName);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 0.7f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void CreateProgressMarker(string objectName, Transform parent, Vector2 position, Sprite iconSprite)
        {
            var marker = CreatePanel(objectName, parent, Color.white);
            marker.sprite = progressCircleSprite;
            marker.type = Image.Type.Simple;
            marker.preserveAspect = true;
            SetRect(marker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(82f, 82f));
            var mask = marker.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var icon = CreatePanel("Icon", marker.transform, Color.white);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f));
        }

        private void CreateWorldStatusCanvas()
        {
            var canvasObject = new GameObject("Wealth Status");
            canvasObject.transform.SetParent(player.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.0045f;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = gameCamera;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 85f);

            statusText = CreateText("Status", canvasObject.transform, "БЕДНЫЙ", 32, TextAnchor.MiddleCenter, new Color(1f, 0.45f, 0.05f));
            SetRect(statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(230f, 42f));
            var bar = CreatePanel("Bar", canvasObject.transform, new Color(0.25f, 0.25f, 0.25f, 0.9f));
            SetRect(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(220f, 24f));
            wealthFill = CreateFilledImage("Wealth Fill", bar.transform, new Color(1f, 0.45f, 0.05f));
        }

        private void UpdateOutfit()
        {
            string activeOutfit;
            Color statusColor;
            if (wealth < 70)
            {
                activeOutfit = wealth < 25 ? "poor" : "casual";
                statusText.text = "БЕДНЫЙ";
                statusColor = new Color(1f, 0.45f, 0.05f);
            }
            else if (wealth < 90)
            {
                activeOutfit = "middle";
                statusText.text = "СОСТОЯТЕЛЬНЫЙ";
                statusColor = new Color(0.2f, 0.95f, 0.3f);
            }
            else
            {
                activeOutfit = wealth > 90 ? "bling" : "buisiness";
                statusText.text = "БОГАТЫЙ";
                statusColor = new Color(0.15f, 1f, 0.25f);
            }

            foreach (var pair in outfits)
                pair.Value.SetActive(pair.Key == activeOutfit);
            statusText.color = statusColor;
            wealthFill.color = statusColor;
        }

        private void ShowFloatingText(Vector3 position, string message, Color color)
        {
            var canvasObject = new GameObject("Floating Score");
            canvasObject.transform.position = position;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = gameCamera;
            canvasObject.transform.rotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.01f;
            var text = CreateText("Value", canvasObject.transform, message, 44, TextAnchor.MiddleCenter, color);
            text.fontStyle = FontStyle.Bold;
            SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 70f));
            StartCoroutine(AnimateFloatingText(canvasObject.transform));
        }

        private IEnumerator AnimateFloatingText(Transform floating)
        {
            var elapsed = 0f;
            while (elapsed < 0.85f)
            {
                elapsed += Time.deltaTime;
                floating.position += Vector3.up * Time.deltaTime * 1.2f;
                yield return null;
            }
            Destroy(floating.gameObject);
        }

        private void SpawnMoneyClaimEffect(Vector3 position, int billCount)
        {
            var root = new GameObject("Money Claim Effect");
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position + Vector3.up * 0.8f;

            CreateBillParticleSystem(root.transform, billCount);
            CreateBillboardParticleSystem("Green Glow", root.transform, moneyGlowParticleMaterial,
                new Color(0.22f, 1f, 0.18f, 0.72f), 18, 0.35f, 0.55f, 0.65f);
            CreateBillboardParticleSystem("Money Sparkles", root.transform, moneySparkleParticleMaterial,
                new Color(0.55f, 1f, 0.18f, 0.95f), 14, 0.8f, 0.22f, 0.85f);
            Destroy(root, 1.8f);
        }

        private void SpawnColorBurst(Vector3 position, Material material, Color color, int count)
        {
            var root = new GameObject("Penalty Effect");
            root.transform.SetParent(levelRoot, false);
            root.transform.position = position + Vector3.up * 0.7f;
            CreateBillboardParticleSystem("Penalty Burst", root.transform, material, color, count, 1.1f, 0.18f, 0.55f);
            Destroy(root, 1.5f);
        }

        private void CreateBillParticleSystem(Transform parent, int count)
        {
            var particleObject = new GameObject("Flying Money");
            particleObject.transform.SetParent(parent, false);
            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            var meshFilter = moneyModel != null ? moneyModel.GetComponentInChildren<MeshFilter>(true) : null;
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = meshFilter.sharedMesh;
            }
            var sourceRenderer = moneyModel != null ? moneyModel.GetComponentInChildren<Renderer>(true) : null;
            renderer.sharedMaterial = sourceRenderer != null && sourceRenderer.sharedMaterial != null
                ? sourceRenderer.sharedMaterial
                : positiveEffectMaterial;
            particles.Play();
        }

        private static void CreateBillboardParticleSystem(string objectName, Transform parent, Material material,
            Color color, int count, float speed, float size, float radius)
        {
            var particleObject = new GameObject(objectName);
            particleObject.transform.SetParent(parent, false);
            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.01f, size);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            particles.Play();
        }

        private void PlaySound(AudioClip clip, float volume)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip, volume);
        }

        private void UpdateWalkingSound()
        {
            if (!IsRunning)
            {
                walkingSoundTimer = 0f;
                return;
            }

            walkingSoundTimer -= Time.deltaTime;
            if (walkingSoundTimer > 0f)
                return;

            var clip = useRightWalkingSound ? walkingSoundRight : walkingSoundLeft;
            useRightWalkingSound = !useRightWalkingSound;
            PlaySound(clip, 0.32f);
            walkingSoundTimer = 0.34f;
        }

        private void ApplyPlayerMaterial(Transform visualRoot)
        {
            if (visualRoot == null || playerMaterial == null)
                return;

            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = playerMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        private GameObject InstantiateFittedModel(GameObject source, string objectName, Transform parent,
            Vector3 localPosition, Vector3 targetSize, Material materialOverride)
        {
            var instance = Instantiate(source, parent);
            instance.name = objectName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ApplyCompatibleMaterials(instance);

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return instance;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var scaleX = bounds.size.x > 0.001f ? targetSize.x / bounds.size.x : 1f;
            var scaleY = bounds.size.y > 0.001f ? targetSize.y / bounds.size.y : 1f;
            var fitScale = Mathf.Min(scaleX, scaleY);
            instance.transform.localScale = Vector3.one * fitScale;

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            var desiredCenter = parent.TransformPoint(localPosition + Vector3.up * targetSize.y * 0.5f);
            instance.transform.position += desiredCenter - bounds.center;

            if (materialOverride != null)
            {
                foreach (var renderer in renderers)
                    renderer.sharedMaterial = materialOverride;
            }

            return instance;
        }

        private static void EnsureTrigger(GameObject target, Vector3 size)
        {
            foreach (var existing in target.GetComponentsInChildren<Collider>())
                existing.enabled = false;
            var collider = target.GetComponent<BoxCollider>();
            if (collider == null)
                collider = target.AddComponent<BoxCollider>();
            collider.enabled = true;
            collider.isTrigger = true;
            collider.size = size;
            collider.center = new Vector3(0f, size.y * 0.4f, 0f);
        }

        private static void EnsureCapsuleTrigger(GameObject target)
        {
            foreach (var existing in target.GetComponentsInChildren<Collider>())
                existing.enabled = false;

            var collider = target.GetComponent<CapsuleCollider>();
            if (collider == null)
                collider = target.AddComponent<CapsuleCollider>();
            collider.enabled = true;
            collider.isTrigger = true;
            collider.direction = 1;
            collider.radius = 0.42f;
            collider.height = 1.25f;
            collider.center = new Vector3(0f, 0.5f, 0f);
        }

        private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = objectName;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private GameObject CreateCylinder(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gameObject.name = objectName;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private void CreateWorldLabel(string label, Transform parent, Vector3 position, Color color, float scale)
        {
            var canvasObject = new GameObject(label + " Label");
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = position;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * scale;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var text = CreateText("Text", canvasObject.transform, label, 42, TextAnchor.MiddleCenter, color);
            text.fontStyle = FontStyle.Bold;
            SetRect(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 70f));
        }

        private static Text CreateText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Image CreatePanel(string objectName, Transform parent, Color color)
        {
            var panelObject = new GameObject(objectName, typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateFilledImage(string objectName, Transform parent, Color color)
        {
            var image = CreatePanel(objectName, parent, color);
            SetRect(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.fillAmount = 0f;
            return image;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
