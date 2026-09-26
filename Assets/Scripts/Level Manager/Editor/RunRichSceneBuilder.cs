#if UNITY_EDITOR
using System.Linq;
using ButchersGames;
using RunRichClone;
using RunRich3D.Installers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public static class RunRichSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/RunRichLevel.unity";
    private const string LevelsListPath = "Assets/Configs/RunRichLevels.asset";
    private const string PlayerPath = "Assets/Assets/Assets/Visual/Mesh/LowPoly/player.fbx";
    private const string MoneyPath = "Assets/Assets/Assets/Visual/Mesh/LowPoly/bills.fbx";
    private const string BottlePath = "Assets/Assets/Assets/Visual/Mesh/LowPoly/bottle.fbx";
    private const string DoorPath = "Assets/Assets/Assets/Visual/Mesh/LowPoly/Door_Million.fbx";
    private const string PartyGateMeshPath = "Assets/Assets/Assets/Visual/Mesh/Party.asset";
    private const string SchoolGateMeshPath = "Assets/Assets/Assets/Visual/Mesh/Hat_School.asset";
    private const string GroundMaterialPath = "Assets/Assets/Assets/Visual/Material/Ground.mat";
    private const string WaterMaterialPath = "Assets/Assets/Assets/Visual/Material/Water.mat";
    private const string GoodDoorMaterialPath = "Assets/Assets/Assets/Visual/Material/GoodDoor.mat";
    private const string BadDoorMaterialPath = "Assets/Assets/Assets/Visual/Material/BadDoor.mat";
    private const string FinishMaterialPath = "Assets/Assets/Assets/Visual/Material/Plane_Finish.mat";
    private const string CheckpointMaterialPath = "Assets/Assets/Assets/Visual/Material/Checkpoints.mat";
    private const string BadItemMaterialPath = "Assets/Assets/Assets/Visual/Material/BadItem.mat";
    private const string MoneyMaterialPath = "Assets/Assets/Assets/Visual/Material/PlaceHolderMonney.mat";
    private const string GateIconMaterialPath = "Assets/Assets/Assets/Visual/Material/props_flat.mat";
    private const string MoneyGlowMaterialPath = "Assets/Assets/Assets/Visual/Material/fading_circle.mat";
    private const string MoneySparkleMaterialPath = "Assets/Assets/Assets/Visual/Material/stars_add.mat";
    private const string ProgressFrameSpritePath = "Assets/Assets/Assets/Visual/Sprite/rounded-rectangle.asset";
    private const string ProgressCircleSpritePath = "Assets/Assets/Assets/Visual/Sprite/Circle.asset";
    private const string PoorProgressIconPath = "Assets/Assets/Assets/Visual/Sprite/Base_Thumbnail.asset";
    private const string RichProgressIconPath = "Assets/Assets/Assets/Visual/Sprite/Rappeur_Thumbnail.asset";
    private const string MoneySoundPath = "Assets/Assets/Assets/Sounds/AudioClip/collect_coin.ogg";
    private const string BadSoundPath = "Assets/Assets/Assets/Sounds/AudioClip/RemoveMoney.ogg";
    private const string WinSoundPath = "Assets/Assets/Assets/Sounds/AudioClip/shortcutrun_sfx_jingle_victory.ogg";
    private const string PlayerMaterialPath = "Assets/Assets/Assets/Visual/Material/player_mat.mat";
    private const string PlayerAtlasPath = "Assets/Assets/Assets/Visual/Texture2D/atlas.png";
    private const string WalkingSoundLeftPath = "Assets/Assets/Assets/Sounds/AudioClip/HighHeels_1.ogg";
    private const string WalkingSoundRightPath = "Assets/Assets/Assets/Sounds/AudioClip/HighHeels_2.ogg";
    private const string BonusMultiplierSoundPath = "Assets/Assets/Assets/Sounds/AudioClip/shortcutrun_sfx_joueur_bonus_multiplier_x01_to_x05_variation01.ogg";

    [InitializeOnLoadMethod]
    private static void RecoverAuthoredSceneAfterScriptReload()
    {
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorApplication.delayCall += OpenAuthoredSceneIfUnityRestoredABackup;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (IsBackupScene(scene.path))
            EditorApplication.delayCall += OpenAuthoredSceneIfUnityRestoredABackup;
    }

    private static void OpenAuthoredSceneIfUnityRestoredABackup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += OpenAuthoredSceneIfUnityRestoredABackup;
            return;
        }

        if (!IsBackupScene(SceneManager.GetActiveScene().path))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += OpenAuthoredSceneIfUnityRestoredABackup;
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("Run Rich recovery: Unity backup scene was replaced with the authored RunRichLevel scene.");
    }

    private static bool IsBackupScene(string path)
    {
        return !string.IsNullOrEmpty(path) &&
               path.Replace('\\', '/').StartsWith("Temp/__Backupscenes/", System.StringComparison.OrdinalIgnoreCase);
    }

    [MenuItem("Run Rich/Fix Zenject Scene Setup")]
    public static void FixZenjectSceneSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += FixZenjectSceneSetup;
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var game = Object.FindFirstObjectByType<RunRichGame>(FindObjectsInactive.Include);
        if (game == null)
            throw new MissingReferenceException("RunRichLevel does not contain RunRichGame.");

        var levelManager = Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
        if (levelManager == null)
            levelManager = game.gameObject.AddComponent<LevelManager>();

        var serializedLevelManager = new SerializedObject(levelManager);
        Assign(serializedLevelManager, "levels", LevelsListPath);
        serializedLevelManager.ApplyModifiedPropertiesWithoutUndo();

        var sceneContext = Object.FindFirstObjectByType<SceneContext>(FindObjectsInactive.Include);
        if (sceneContext == null)
            sceneContext = new GameObject("SceneContext").AddComponent<SceneContext>();

        var installer = sceneContext.GetComponent<RunRichSceneInstaller>();
        if (installer == null)
            installer = sceneContext.gameObject.AddComponent<RunRichSceneInstaller>();

        var serializedInstaller = new SerializedObject(installer);
        serializedInstaller.FindProperty("levelManager").objectReferenceValue = levelManager;
        serializedInstaller.ApplyModifiedPropertiesWithoutUndo();

        sceneContext.Installers = new MonoInstaller[] { installer };
        EditorUtility.SetDirty(levelManager);
        EditorUtility.SetDirty(installer);
        EditorUtility.SetDirty(sceneContext);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = sceneContext.gameObject;
        EditorGUIUtility.PingObject(sceneContext.gameObject);
        Debug.Log("Run Rich Zenject scene setup fixed and saved: " + ScenePath);
    }

    [MenuItem("Run Rich/Fix All Project Materials")]
    public static void FixAllProjectMaterials()
    {
        var materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        var fixedCount = 0;

        foreach (var materialGuid in materialGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(materialGuid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || IsUsableShader(material.shader))
                continue;

            var texture = GetSavedTexture(material, "_BaseMap", "_MainTex");
            var normalMap = GetSavedTexture(material, "_BumpMap");
            var color = GetSavedColor(material, Color.white, "_BaseColor", "_Color", "_FaceColor");
            var transparent = IsTransparentMaterial(path, material, color);
            var replacement = SelectReplacementShader(path, transparent);
            if (replacement == null)
            {
                Debug.LogWarning("No compatible shader was found for material: " + path);
                continue;
            }

            material.shader = replacement;
            ApplyTexture(material, texture);
            if (normalMap != null && material.HasProperty("_BumpMap"))
                material.SetTexture("_BumpMap", normalMap);
            ApplyColor(material, color);
            ConfigureSurface(material, transparent);
            EditorUtility.SetDirty(material);
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Run Rich material repair completed. Fixed {fixedCount} of {materialGuids.Length} project materials.");
    }

    [MenuItem("Run Rich/Fix Player Material Shine")]
    public static void FixPlayerMaterialShine()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerAtlasPath);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (material == null || texture == null || shader == null)
            throw new MissingReferenceException("Player material, atlas, or URP Lit shader is missing.");

        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_Metallic", 0.08f);
        material.SetFloat("_Smoothness", 0.62f);
        material.SetFloat("_EnvironmentReflections", 1f);
        material.SetFloat("_SpecularHighlights", 1f);
        material.DisableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(material);

        if (AssetImporter.GetAtPath(PlayerAtlasPath) is TextureImporter importer)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.compressionQuality = 100;
            importer.anisoLevel = 4;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Run Rich player material restored: sharp atlas, clean color, and glossy highlights.");
    }

    private static bool IsUsableShader(Shader shader)
    {
        return shader != null && shader.isSupported &&
               !shader.name.Equals("Hidden/InternalErrorShader", System.StringComparison.Ordinal);
    }

    private static Shader SelectReplacementShader(string path, bool transparent)
    {
        var lowerPath = path.ToLowerInvariant();
        if (lowerPath.Contains("textmeshpro_sprite"))
        {
            var spriteShader = Shader.Find("TextMeshPro/Sprite");
            if (IsUsableShader(spriteShader))
                return spriteShader;
        }

        if (lowerPath.Contains("textmeshpro") || lowerPath.Contains("atlas material") ||
            lowerPath.Contains("mainfont") || lowerPath.Contains("mikado material"))
        {
            var textShader = Shader.Find("TextMeshPro/Distance Field") ?? Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (IsUsableShader(textShader))
                return textShader;
        }

        var shaderName = transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
        return Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
    }

    private static bool IsTransparentMaterial(string path, Material material, Color color)
    {
        var value = (path + " " + material.name).ToLowerInvariant();
        return color.a < 0.999f || material.renderQueue >= 3000 ||
               value.Contains("transp") || value.Contains("fade") || value.Contains("cloud") ||
               value.Contains("halo") || value.Contains("sparkle") || value.Contains("light_") ||
               value.Contains("stars_") || value.Contains("winedrop") || value.Contains("stain");
    }

    private static Texture GetSavedTexture(Material material, params string[] propertyNames)
    {
        var properties = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        if (properties == null)
            return null;

        foreach (var propertyName in propertyNames)
        {
            for (var i = 0; i < properties.arraySize; i++)
            {
                var entry = properties.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue != propertyName)
                    continue;
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }
        }

        return null;
    }

    private static Color GetSavedColor(Material material, Color fallback, params string[] propertyNames)
    {
        var properties = new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
        if (properties == null)
            return fallback;

        foreach (var propertyName in propertyNames)
        {
            for (var i = 0; i < properties.arraySize; i++)
            {
                var entry = properties.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == propertyName)
                    return entry.FindPropertyRelative("second").colorValue;
            }
        }

        return fallback;
    }

    private static void ApplyTexture(Material material, Texture texture)
    {
        if (texture == null)
            return;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_FaceColor"))
            material.SetColor("_FaceColor", color);
    }

    private static void ConfigureSurface(Material material, bool transparent)
    {
        if (!material.shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", transparent ? 1f : 0f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (float)UnityEngine.Rendering.BlendMode.Zero);

        if (transparent)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
        }
    }

    [MenuItem("Run Rich/Build First Level")]
    public static void BuildFirstLevel()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before rebuilding the Run Rich scene.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "RunRichLevel";

        var root = new GameObject("Run Rich 3D");
        var levelManager = root.AddComponent<LevelManager>();
        var serializedLevelManager = new SerializedObject(levelManager);
        Assign(serializedLevelManager, "levels", LevelsListPath);
        serializedLevelManager.ApplyModifiedPropertiesWithoutUndo();
        var contextObject = new GameObject("SceneContext");
        var installer = contextObject.AddComponent<RunRichSceneInstaller>();
        var serializedInstaller = new SerializedObject(installer);
        serializedInstaller.FindProperty("levelManager").objectReferenceValue = levelManager;
        serializedInstaller.ApplyModifiedPropertiesWithoutUndo();
        var sceneContext = contextObject.AddComponent<SceneContext>();
        sceneContext.Installers = new MonoInstaller[] { installer };
        var game = root.AddComponent<RunRichGame>();
        var serializedGame = new SerializedObject(game);
        Assign(serializedGame, "playerModel", PlayerPath);
        Assign(serializedGame, "playerMaterial", PlayerMaterialPath);
        Assign(serializedGame, "moneyModel", MoneyPath);
        Assign(serializedGame, "bottleModel", BottlePath);
        Assign(serializedGame, "doorModel", DoorPath);
        Assign(serializedGame, "partyGateMesh", PartyGateMeshPath);
        Assign(serializedGame, "schoolGateMesh", SchoolGateMeshPath);
        Assign(serializedGame, "groundAssetMaterial", GroundMaterialPath);
        Assign(serializedGame, "waterAssetMaterial", WaterMaterialPath);
        Assign(serializedGame, "goodDoorAssetMaterial", GoodDoorMaterialPath);
        Assign(serializedGame, "badDoorAssetMaterial", BadDoorMaterialPath);
        Assign(serializedGame, "finishAssetMaterial", FinishMaterialPath);
        Assign(serializedGame, "checkpointAssetMaterial", CheckpointMaterialPath);
        Assign(serializedGame, "badItemAssetMaterial", BadItemMaterialPath);
        Assign(serializedGame, "moneyAssetMaterial", MoneyMaterialPath);
        Assign(serializedGame, "gateIconAssetMaterial", GateIconMaterialPath);
        Assign(serializedGame, "moneyGlowParticleMaterial", MoneyGlowMaterialPath);
        Assign(serializedGame, "moneySparkleParticleMaterial", MoneySparkleMaterialPath);
        Assign(serializedGame, "progressFrameSprite", ProgressFrameSpritePath);
        Assign(serializedGame, "progressCircleSprite", ProgressCircleSpritePath);
        Assign(serializedGame, "poorProgressIcon", PoorProgressIconPath);
        Assign(serializedGame, "richProgressIcon", RichProgressIconPath);
        Assign(serializedGame, "moneySound", MoneySoundPath);
        Assign(serializedGame, "badSound", BadSoundPath);
        Assign(serializedGame, "winSound", WinSoundPath);
        Assign(serializedGame, "walkingSoundLeft", WalkingSoundLeftPath);
        Assign(serializedGame, "walkingSoundRight", WalkingSoundRightPath);
        Assign(serializedGame, "bonusMultiplierSound", BonusMultiplierSoundPath);
        serializedGame.ApplyModifiedPropertiesWithoutUndo();

        game.BakeSceneLayoutInEditor();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Run Rich first level generated successfully: " + ScenePath);
    }

    private static void Assign(SerializedObject target, string propertyName, string assetPath)
    {
        var property = target.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError("Missing serialized field: " + propertyName);
            return;
        }

        property.objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (property.objectReferenceValue == null)
            Debug.LogWarning("Run Rich asset was not found: " + assetPath);
    }

    private static void AddSceneToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(entry => entry.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
