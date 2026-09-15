using System;
using System.Collections.Generic;
using System.IO;
using Richman.Core;
using Richman.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Richman.Editor
{
    public static class BuildM15Presentation
    {
        private const string ScenePath = "Assets/Scenes/Gameplay.unity";
        private const string MaterialPath = "Assets/Art/Materials";
        private const string PrefabPath = "Assets/Prefabs";

        [MenuItem("Richman/Generate M1.5 Gameplay Scene")]
        public static void Generate()
        {
            try
            {
                EnsureFolders();
                var materials = CreateMaterials();
                var tilePrefab = CreateBoardTilePrefab(materials.Tile);
                var propertyPrefab = CreatePropertyPrefab(materials.Toy, materials.Dark);
                var pawnPrefab = CreatePawnPrefab(materials.Toy, materials.Dark, materials.Skin);
                var dicePrefab = CreateDicePrefab(materials.Toy, materials.Dark);
                var cityPrefab = CreateCityPrefab(materials.Toy, materials.Dark, materials.Green);
                var groundPrefab = CreateGroundPrefab(materials.Ground);
                var cameraPrefab = CreateCameraPrefab();
                var hudPrefab = CreateHudPrefab();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Board/BoardTile.prefab").GetComponent<BoardTileView>();
                propertyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Board/Property.prefab").GetComponent<PropertyView>();
                pawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Characters/Pawn.prefab").GetComponent<PawnView>();
                dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Props/Dice.prefab").GetComponent<DiceView>();
                cityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Environment/MiniatureCity.prefab");
                groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Environment/Ground.prefab");
                cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Environment/CameraRig.prefab");
                hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/UI/GameplayHUD.prefab");

                CreateGameplayScene(
                    tilePrefab,
                    propertyPrefab,
                    pawnPrefab,
                    dicePrefab,
                    cityPrefab,
                    groundPrefab,
                    cameraPrefab,
                    hudPrefab);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Richman M1.5 Gameplay Scene and replaceable prefabs generated.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) throw;
                EditorUtility.DisplayDialog(
                    "Richman M1.5 Generation Failed",
                    exception.Message + "\n\nOpen the Console for the full stack trace.",
                    "OK");
            }
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Art/Materials"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Board"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Characters"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Environment"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/Props"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Prefabs/UI"));
            AssetDatabase.Refresh();
        }

        private static PresentationMaterials CreateMaterials()
        {
            return new PresentationMaterials
            {
                Toy = EnsureMaterial(MaterialPath + "/ToyCity.mat", Color.white, 0.05f, 0.4f),
                Dark = EnsureMaterial(MaterialPath + "/ToyCityDark.mat", new Color(0.06f, 0.08f, 0.12f), 0.05f, 0.25f),
                Skin = EnsureMaterial(MaterialPath + "/ToyCitySkin.mat", new Color(1f, 0.78f, 0.62f), 0f, 0.35f),
                Tile = EnsureMaterial(MaterialPath + "/TileBase.mat", Color.white, 0.02f, 0.3f),
                Ground = EnsureMaterial(MaterialPath + "/Ground.mat", new Color(0.06f, 0.08f, 0.12f), 0.05f, 0.2f),
                Green = EnsureMaterial(MaterialPath + "/ToyCityGreen.mat", new Color(0.25f, 0.65f, 0.43f), 0.02f, 0.3f),
                UiPanel = EnsureMaterial(MaterialPath + "/UIPanel.mat", new Color(0.025f, 0.04f, 0.08f, 0.94f), 0f, 0f, "UI/Default"),
                UiButton = EnsureMaterial(MaterialPath + "/UIButton.mat", new Color(0.12f, 0.30f, 0.55f, 1f), 0f, 0f, "UI/Default")
            };
        }

        private static Material EnsureMaterial(string path, Color color, float metallic, float smoothness, string shaderName = "Standard")
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = FindPresentationShader(shaderName);
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "Richman M1.5 could not find a usable presentation shader. " +
                        "Enable the built-in rendering modules and reimport the project.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindPresentationShader(string preferredName)
        {
            var shader = Shader.Find(preferredName);
            if (shader != null) return shader;

            shader = Shader.Find("Standard");
            if (shader != null) return shader;

            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            shader = Shader.Find("Unlit/Color");
            if (shader != null) return shader;

            return Shader.Find("Sprites/Default");
        }

        private static BoardTileView CreateBoardTilePrefab(Material tileMaterial)
        {
            var root = new GameObject("BoardTile");
            var view = root.AddComponent<BoardTileView>();
            var surface = CreatePrimitive(PrimitiveType.Cube, "Surface", root.transform,
                Vector3.zero, new Vector3(1.18f, 0.22f, 1.18f), tileMaterial);
            var trim = CreatePrimitive(PrimitiveType.Cube, "Trim", root.transform,
                new Vector3(0f, 0.13f, 0f), new Vector3(1.23f, 0.045f, 1.23f), tileMaterial);
            var label = CreateWorldText(root.transform, "TileLabel", new Vector3(0f, 0.17f, 0f),
                Quaternion.Euler(90f, 0f, 0f), "TILE", 32, 0.075f, Color.white, TextAnchor.MiddleCenter);
            var pawnAnchor = CreateEmpty(root.transform, "PawnAnchor", new Vector3(0f, 0.18f, 0f));
            var propertyAnchor = CreateEmpty(root.transform, "PropertyAnchor", new Vector3(0f, 0.18f, 0f));
            view.SetPrefabReferences(surface.GetComponent<Renderer>(), trim.GetComponent<Renderer>(), label,
                pawnAnchor.transform, propertyAnchor.transform, null);
            return SavePrefab(root, "Assets/Prefabs/Board/BoardTile.prefab").GetComponent<BoardTileView>();
        }

        private static PropertyView CreatePropertyPrefab(Material toyMaterial, Material darkMaterial)
        {
            var root = new GameObject("Property");
            var view = root.AddComponent<PropertyView>();
            var lot = CreatePrimitive(PrimitiveType.Cube, "EmptyLot", root.transform,
                new Vector3(0f, 0.025f, 0f), new Vector3(0.86f, 0.05f, 0.86f), toyMaterial);
            var indicator = CreatePrimitive(PrimitiveType.Cylinder, "OwnershipIndicator", root.transform,
                new Vector3(0f, 0.11f, 0f), new Vector3(0.55f, 0.035f, 0.55f), darkMaterial);

            var level1 = CreateEmpty(root.transform, "BuildingSlot_Lv1", Vector3.zero);
            level1.AddComponent<BuildingSlot>().SetLevel(1);
            CreatePrimitive(PrimitiveType.Cube, "SmallBuilding", level1.transform,
                new Vector3(0f, 0.30f, 0f), new Vector3(0.38f, 0.48f, 0.38f), toyMaterial);
            CreatePrimitive(PrimitiveType.Cube, "SmallRoof", level1.transform,
                new Vector3(0f, 0.56f, 0f), new Vector3(0.48f, 0.06f, 0.48f), toyMaterial);

            var level2 = CreateEmpty(root.transform, "BuildingSlot_Lv2", Vector3.zero);
            level2.AddComponent<BuildingSlot>().SetLevel(2);
            CreatePrimitive(PrimitiveType.Cube, "MediumBuilding", level2.transform,
                new Vector3(0f, 0.40f, 0f), new Vector3(0.52f, 0.68f, 0.52f), toyMaterial);
            CreatePrimitive(PrimitiveType.Cube, "MediumRoof", level2.transform,
                new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.06f, 0.62f), toyMaterial);

            var level3 = CreateEmpty(root.transform, "BuildingSlot_Lv3", Vector3.zero);
            level3.AddComponent<BuildingSlot>().SetLevel(3);
            CreatePrimitive(PrimitiveType.Cube, "LargeBuilding", level3.transform,
                new Vector3(0f, 0.47f, 0f), new Vector3(0.60f, 0.82f, 0.60f), toyMaterial);
            CreatePrimitive(PrimitiveType.Cube, "LargeTower", level3.transform,
                new Vector3(0f, 0.98f, 0f), new Vector3(0.34f, 0.34f, 0.34f), toyMaterial);
            CreatePrimitive(PrimitiveType.Cube, "LargeRoof", level3.transform,
                new Vector3(0f, 1.18f, 0f), new Vector3(0.72f, 0.07f, 0.72f), toyMaterial);

            level1.SetActive(false);
            level2.SetActive(false);
            level3.SetActive(false);
            view.SetPrefabReferences(lot, indicator, new[] { level1, level2, level3 },
                root.GetComponentsInChildren<Renderer>(true));
            return SavePrefab(root, "Assets/Prefabs/Board/Property.prefab").GetComponent<PropertyView>();
        }

        private static PawnView CreatePawnPrefab(Material toyMaterial, Material darkMaterial, Material skinMaterial)
        {
            var root = new GameObject("Pawn");
            var view = root.AddComponent<PawnView>();
            var baseObject = CreatePrimitive(PrimitiveType.Cylinder, "Base", root.transform,
                new Vector3(0f, 0.08f, 0f), new Vector3(0.46f, 0.08f, 0.46f), darkMaterial);
            var body = CreatePrimitive(PrimitiveType.Capsule, "Body", root.transform,
                new Vector3(0f, 0.42f, 0f), new Vector3(0.35f, 0.42f, 0.35f), toyMaterial);
            CreatePrimitive(PrimitiveType.Sphere, "Head", root.transform,
                new Vector3(0f, 0.90f, 0f), new Vector3(0.26f, 0.26f, 0.26f), skinMaterial);
            var hat = CreatePrimitive(PrimitiveType.Cylinder, "Hat", root.transform,
                new Vector3(0f, 1.13f, 0f), new Vector3(0.29f, 0.08f, 0.29f), toyMaterial);
            var label = CreateWorldText(root.transform, "PlayerLabel", new Vector3(0f, 1.48f, 0f),
                Quaternion.identity, "P", 42, 0.07f, Color.white, TextAnchor.MiddleCenter);
            view.SetPrefabReferences(new[] { body.GetComponent<Renderer>(), hat.GetComponent<Renderer>() }, label);
            return SavePrefab(root, "Assets/Prefabs/Characters/Pawn.prefab").GetComponent<PawnView>();
        }

        private static DiceView CreateDicePrefab(Material toyMaterial, Material darkMaterial)
        {
            var root = new GameObject("Dice");
            var view = root.AddComponent<DiceView>();
            var visual = CreatePrimitive(PrimitiveType.Cube, "DiceVisual", root.transform,
                Vector3.zero, new Vector3(0.92f, 0.92f, 0.92f), toyMaterial);
            CreatePip(visual.transform, new Vector3(-0.23f, 0.23f, -0.48f), darkMaterial);
            CreatePip(visual.transform, new Vector3(0.23f, -0.23f, -0.48f), darkMaterial);
            CreatePip(visual.transform, new Vector3(0f, 0f, -0.48f), darkMaterial);
            CreatePip(visual.transform, new Vector3(-0.23f, 0.23f, 0.48f), darkMaterial);
            CreatePip(visual.transform, new Vector3(0.23f, -0.23f, 0.48f), darkMaterial);
            var label = CreateWorldText(root.transform, "DiceResultLabel", new Vector3(0f, 0.90f, 0f),
                Quaternion.identity, "?", 54, 0.08f, Color.white, TextAnchor.MiddleCenter);
            view.SetPrefabReferences(visual.transform, label);
            return SavePrefab(root, "Assets/Prefabs/Props/Dice.prefab").GetComponent<DiceView>();
        }

        private static GameObject CreateCityPrefab(Material toyMaterial, Material darkMaterial, Material greenMaterial)
        {
            var root = new GameObject("MiniatureCity");
            CreatePrimitive(PrimitiveType.Cube, "CityPlinth", root.transform,
                new Vector3(0f, 0.12f, 0f), new Vector3(7.6f, 0.25f, 7.6f), darkMaterial);
            CreateCityBlock(root.transform, new Vector3(-2.3f, 0.25f, -2.1f), new Vector3(1.2f, 1.3f, 1.1f), toyMaterial);
            CreateCityBlock(root.transform, new Vector3(0.0f, 0.25f, -2.3f), new Vector3(1.0f, 2.0f, 1.0f), toyMaterial);
            CreateCityBlock(root.transform, new Vector3(2.2f, 0.25f, -1.9f), new Vector3(1.3f, 1.0f, 1.2f), toyMaterial);
            CreateCityBlock(root.transform, new Vector3(-2.0f, 0.25f, 1.8f), new Vector3(1.4f, 1.8f, 1.2f), toyMaterial);
            CreateCityBlock(root.transform, new Vector3(0.3f, 0.25f, 1.9f), new Vector3(1.1f, 1.2f, 1.1f), toyMaterial);
            CreateCityBlock(root.transform, new Vector3(2.35f, 0.25f, 1.7f), new Vector3(1.0f, 2.3f, 1.0f), toyMaterial);
            for (var i = 0; i < 5; i++)
            {
                var x = -3.0f + i * 1.5f;
                CreatePrimitive(PrimitiveType.Cylinder, "ParkTreeTrunk_" + i, root.transform,
                    new Vector3(x, 0.43f, 0f), new Vector3(0.08f, 0.25f, 0.08f), darkMaterial);
                CreatePrimitive(PrimitiveType.Sphere, "ParkTreeTop_" + i, root.transform,
                    new Vector3(x, 0.86f, 0f), new Vector3(0.32f, 0.32f, 0.32f), greenMaterial);
            }

            return SavePrefab(root, "Assets/Prefabs/Environment/MiniatureCity.prefab");
        }

        private static GameObject CreateGroundPrefab(Material material)
        {
            var root = new GameObject("Ground");
            CreatePrimitive(PrimitiveType.Cube, "GroundSurface", root.transform,
                Vector3.zero, new Vector3(15.6f, 0.35f, 15.6f), material);
            return SavePrefab(root, "Assets/Prefabs/Environment/Ground.prefab");
        }

        private static GameObject CreateCameraPrefab()
        {
            var root = new GameObject("CameraRig");
            var rig = root.AddComponent<CameraRig>();
            var target = CreateEmpty(root.transform, "OverviewTarget", new Vector3(0f, 0.2f, 0f));
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.07f);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            rig.SetSceneReferences(camera, target.transform);
            return SavePrefab(root, "Assets/Prefabs/Environment/CameraRig.prefab");
        }

        private static GameObject CreateHudPrefab()
        {
            var root = new GameObject("GameplayHUD");
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = EnsurePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/GameplayHUD.uxml");
            var hud = root.AddComponent<GameplayHUD>();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/GameplayHUD.uss");
            hud.SetDocumentReferences(document, styleSheet);
            return SavePrefab(root, "Assets/Prefabs/UI/GameplayHUD.prefab");
        }

        private static PanelSettings EnsurePanelSettings()
        {
            const string path = "Assets/UI/GameplayPanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 800);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void CreateGameplayScene(
            BoardTileView tilePrefab,
            PropertyView propertyPrefab,
            PawnView pawnPrefab,
            DiceView dicePrefab,
            GameObject cityPrefab,
            GameObject groundPrefab,
            GameObject cameraPrefab,
            GameObject hudPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Board/BoardTile.prefab").GetComponent<BoardTileView>();
            propertyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Board/Property.prefab").GetComponent<PropertyView>();
            pawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Characters/Pawn.prefab").GetComponent<PawnView>();
            dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Props/Dice.prefab").GetComponent<DiceView>();
            cityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Environment/MiniatureCity.prefab");
            groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Environment/Ground.prefab");
            cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/Environment/CameraRig.prefab");
            hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/UI/GameplayHUD.prefab");
            var sceneRoot = new GameObject("GameplayScene");
            var boardObject = new GameObject("BoardRoot");
            boardObject.transform.SetParent(sceneRoot.transform, false);
            var tileRoot = CreateEmpty(boardObject.transform, "TileInstances", Vector3.zero).transform;
            var cityRoot = CreateEmpty(boardObject.transform, "CityRoot", Vector3.zero).transform;
            var pawnRoot = CreateEmpty(boardObject.transform, "PawnRoot", Vector3.zero).transform;
            var diceRoot = CreateEmpty(boardObject.transform, "DiceRoot", Vector3.zero).transform;
            var centerTarget = CreateEmpty(boardObject.transform, "BoardCenter", new Vector3(0f, 0.35f, 0f)).transform;
            var boardView = boardObject.AddComponent<BoardView>();

            var boardDefinition = PrototypeBoardFactory.Create();
            var tiles = new List<BoardTileView>(boardDefinition.TileCount);
            for (var i = 0; i < boardDefinition.TileCount; i++)
            {
                var tileObject = InstantiatePrefab(tilePrefab.gameObject, tileRoot);
                var definition = boardDefinition.GetTileAt(i);
                tileObject.name = "Tile_" + i.ToString("00") + "_" + definition.Id;
                tileObject.transform.localPosition = BoardLayout.TilePosition(i);
                var tileView = tileObject.GetComponent<BoardTileView>();
                if (definition.Type == BoardTileType.Property)
                {
                    var propertyObject = InstantiatePrefab(propertyPrefab.gameObject, tileView.PropertyAnchor);
                    propertyObject.name = "Property_" + definition.Id;
                    propertyObject.transform.localPosition = Vector3.zero;
                    tileView.SetScenePropertyView(propertyObject.GetComponent<PropertyView>());
                }

                tiles.Add(tileView);
            }

            var cityObject = InstantiatePrefab(cityPrefab, cityRoot);
            cityObject.name = "MiniatureCity_Center";
            cityObject.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            var groundObject = InstantiatePrefab(groundPrefab, sceneRoot.transform);
            groundObject.name = "Ground";
            groundObject.transform.localPosition = new Vector3(0f, -0.60f, 0f);

            var pawns = new List<PawnView>(boardDefinition.TileCount);
            for (var i = 0; i < 4; i++)
            {
                var pawnObject = InstantiatePrefab(pawnPrefab.gameObject, pawnRoot);
                pawnObject.name = "Pawn_Player_" + (i + 1);
                var pawn = pawnObject.GetComponent<PawnView>();
                pawnObject.transform.position = BoardLayout.TilePosition(0) + BoardLayout.PawnOffset(i + 1);
                pawns.Add(pawn);
            }

            var diceObject = InstantiatePrefab(dicePrefab.gameObject, diceRoot);
            diceObject.name = "GameplayDice";
            diceObject.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var diceView = diceObject.GetComponent<DiceView>();

            var cameraObject = InstantiatePrefab(cameraPrefab, sceneRoot.transform);
            cameraObject.name = "CameraRig";
            var cameraRig = cameraObject.GetComponent<CameraRig>();
            var camera = cameraObject.GetComponentInChildren<Camera>();
            cameraRig.SetSceneReferences(camera, centerTarget);

            var hudObject = InstantiatePrefab(hudPrefab, sceneRoot.transform);
            hudObject.name = "Canvas_GameplayHUD";
            var hud = hudObject.GetComponent<GameplayHUD>();

            var sunObject = new GameObject("Directional Light");
            sunObject.transform.SetParent(sceneRoot.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.92f, 0.82f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.30f);
            RenderSettings.fog = false;

            boardView.SetSceneReferences(boardObject.transform, tileRoot, cityRoot, pawnRoot, diceRoot,
                tilePrefab, propertyPrefab, pawnPrefab, dicePrefab, cityPrefab,
                tiles.ToArray(), pawns.ToArray(), diceView);
            var controllerObject = new GameObject("GameController");
            controllerObject.transform.SetParent(sceneRoot.transform, false);
            var controller = controllerObject.AddComponent<Richman.Playtest.PlaytestGame>();
            controller.SetSceneReferences(boardView, hud, cameraRig, diceView);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject CreatePip(Transform parent, Vector3 position, Material material)
        {
            return CreatePrimitive(PrimitiveType.Sphere, "Pip", parent, position,
                new Vector3(0.075f, 0.075f, 0.035f), material);
        }

        private static void CreateCityBlock(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            CreatePrimitive(PrimitiveType.Cube, "CityBuilding", parent, position,
                new Vector3(size.x, size.y, size.z), material);
            CreatePrimitive(PrimitiveType.Cube, "CityRoof", parent,
                position + Vector3.up * (size.y * 0.55f),
                new Vector3(size.x * 1.12f, 0.08f, size.z * 1.12f), material);
        }

        private static GameObject CreateEmpty(Transform parent, string name, Vector3 localPosition)
        {
            var objectToCreate = new GameObject(name);
            objectToCreate.transform.SetParent(parent, false);
            objectToCreate.transform.localPosition = localPosition;
            return objectToCreate;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var objectToCreate = GameObject.CreatePrimitive(type);
            objectToCreate.name = name;
            objectToCreate.transform.SetParent(parent, false);
            objectToCreate.transform.localPosition = localPosition;
            objectToCreate.transform.localScale = localScale;
            var collider = objectToCreate.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = objectToCreate.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return objectToCreate;
        }

        private static TextMesh CreateWorldText(Transform parent, string name, Vector3 localPosition,
            Quaternion localRotation, string value, int fontSize, float characterSize, Color color, TextAnchor anchor)
        {
            var objectToCreate = new GameObject(name);
            objectToCreate.transform.SetParent(parent, false);
            objectToCreate.transform.localPosition = localPosition;
            objectToCreate.transform.localRotation = localRotation;
            var text = objectToCreate.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.color = color;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return asset;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate prefab " + prefab.name);
            return instance;
        }

        private sealed class PresentationMaterials
        {
            public Material Toy;
            public Material Dark;
            public Material Skin;
            public Material Tile;
            public Material Ground;
            public Material Green;
            public Material UiPanel;
            public Material UiButton;
        }
    }
}
