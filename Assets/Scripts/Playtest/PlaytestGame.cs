using System;
using System.Collections;
using System.Collections.Generic;
using Richman.Core;
using UnityEngine;

namespace Richman.Playtest
{
    // A small 3D presentation shell for the M0/M1 rules core.
    // The board and toy characters are made from Unity primitives so the
    // playtest has no external art, Scene references, Steam, or networking.
    public sealed class PlaytestGame : MonoBehaviour
    {
        private static readonly Color[] PlayerColors =
        {
            new Color(0.95f, 0.25f, 0.22f),
            new Color(0.20f, 0.55f, 0.98f),
            new Color(0.18f, 0.78f, 0.36f),
            new Color(0.98f, 0.66f, 0.12f)
        };

        private static readonly Color[] DistrictColors =
        {
            new Color(0.73f, 0.32f, 0.38f),
            new Color(0.28f, 0.55f, 0.83f),
            new Color(0.32f, 0.70f, 0.55f),
            new Color(0.76f, 0.52f, 0.28f)
        };

        private readonly Dictionary<int, GameObject> _pawns = new Dictionary<int, GameObject>();
        private readonly Dictionary<string, GameObject> _buildingRoots = new Dictionary<string, GameObject>();

        private GameSession _session;
        private GameObject _worldRoot;
        private GameObject[] _tileObjects;
        private TextMesh[] _tileLabels;
        private Material[] _tileMaterials;
        private Material[] _playerMaterials;
        private bool _isMoving;
        private string _message;

        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _smallStyle;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.SetResolution(1280, 800, false);

            _session = PrototypeGameFactory.CreateFourPlayerSession(Environment.TickCount);
            BuildWorld();
            StartNewGame();
        }

        private void BuildWorld()
        {
            if (_worldRoot != null) return;

            _worldRoot = new GameObject("Richman3DWorld");
            _playerMaterials = new Material[PlayerColors.Length];
            for (var i = 0; i < _playerMaterials.Length; i++)
            {
                _playerMaterials[i] = CreateMaterial(PlayerColors[i], 0.05f, 0.35f);
            }

            var floorMaterial = CreateMaterial(new Color(0.055f, 0.075f, 0.11f), 0.05f, 0.25f);
            CreatePrimitive(
                PrimitiveType.Cube,
                "BoardFloor",
                _worldRoot.transform,
                new Vector3(0f, -0.38f, 0f),
                new Vector3(13.4f, 0.45f, 13.4f),
                floorMaterial);

            var innerMaterial = CreateMaterial(new Color(0.09f, 0.12f, 0.18f), 0f, 0.2f);
            CreatePrimitive(
                PrimitiveType.Cube,
                "BoardInner",
                _worldRoot.transform,
                new Vector3(0f, -0.08f, 0f),
                new Vector3(10.8f, 0.16f, 10.8f),
                innerMaterial);

            CreateCenterLabel();
            CreateBoardTiles();
            CreatePawns();
            ConfigureCameraAndLighting();
        }

        private void CreateBoardTiles()
        {
            var tileCount = _session.Board.TileCount;
            _tileObjects = new GameObject[tileCount];
            _tileLabels = new TextMesh[tileCount];
            _tileMaterials = new Material[tileCount];

            for (var i = 0; i < tileCount; i++)
            {
                var tile = _session.Board.GetTileAt(i);
                var position = TileWorldPosition(i);
                var material = CreateMaterial(GetTileColor(tile), 0.02f, 0.3f);
                _tileMaterials[i] = material;
                _tileObjects[i] = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Tile_" + i + "_" + tile.Id,
                    _worldRoot.transform,
                    position,
                    new Vector3(1.05f, 0.24f, 1.05f),
                    material);

                _tileLabels[i] = CreateText(
                    "TileLabel_" + i,
                    _worldRoot.transform,
                    position + Vector3.up * 0.16f,
                    Quaternion.Euler(90f, 0f, 0f),
                    "",
                    34,
                    0.075f,
                    Color.white,
                    TextAnchor.MiddleCenter);
            }
        }

        private void CreatePawns()
        {
            for (var i = 0; i < _session.State.Players.Count; i++)
            {
                var player = _session.State.Players[i];
                var pawn = new GameObject("Pawn_Player_" + player.Id);
                pawn.transform.SetParent(_worldRoot.transform, false);
                var color = _playerMaterials[(player.Id - 1) % _playerMaterials.Length];
                var darkMaterial = CreateMaterial(new Color(0.035f, 0.04f, 0.06f), 0.1f, 0.2f);
                var skinMaterial = CreateMaterial(new Color(1f, 0.78f, 0.60f), 0f, 0.4f);

                CreatePrimitive(PrimitiveType.Cylinder, "PawnBase", pawn.transform,
                    new Vector3(0f, 0.08f, 0f), new Vector3(0.44f, 0.08f, 0.44f), darkMaterial);
                CreatePrimitive(PrimitiveType.Capsule, "PawnBody", pawn.transform,
                    new Vector3(0f, 0.40f, 0f), new Vector3(0.34f, 0.40f, 0.34f), color);
                CreatePrimitive(PrimitiveType.Sphere, "PawnHead", pawn.transform,
                    new Vector3(0f, 0.86f, 0f), new Vector3(0.25f, 0.25f, 0.25f), skinMaterial);
                CreatePrimitive(PrimitiveType.Cylinder, "PawnHat", pawn.transform,
                    new Vector3(0f, 1.08f, 0f), new Vector3(0.28f, 0.08f, 0.28f), color);

                _pawns[player.Id] = pawn;
            }
        }

        private void CreateCenterLabel()
        {
            CreateText(
                "CenterTitle",
                _worldRoot.transform,
                new Vector3(0f, 0.04f, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                "RICHMAN\n3D PLAYTEST",
                64,
                0.085f,
                new Color(0.82f, 0.88f, 1f),
                TextAnchor.MiddleCenter);
        }

        private void ConfigureCameraAndLighting()
        {
            var camera = Camera.main;
            if (camera == null) camera = FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            camera.transform.position = new Vector3(0f, 15.5f, -14.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -1.2f, 0.3f));
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);

            var light = FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightObject = new GameObject("Playtest Sun");
                light = lightObject.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.20f, 0.22f, 0.30f);
            RenderSettings.fog = false;
        }

        private void StartNewGame()
        {
            StopAllCoroutines();
            _isMoving = false;
            _session = PrototypeGameFactory.CreateFourPlayerSession(Environment.TickCount);
            _message = "Player 1 starts. Roll the dice.";

            if (_pawns != null)
            {
                for (var i = 0; i < _session.State.Players.Count; i++)
                {
                    var player = _session.State.Players[i];
                    GameObject pawn;
                    if (_pawns.TryGetValue(player.Id, out pawn))
                    {
                        pawn.transform.position = PawnWorldPosition(player);
                    }
                }
            }

            RefreshBoardVisuals();
        }

        private void RefreshBoardVisuals()
        {
            if (_tileMaterials == null) return;

            for (var i = 0; i < _session.Board.TileCount; i++)
            {
                var tile = _session.Board.GetTileAt(i);
                var property = _session.State.GetProperty(tile.Id);
                var color = GetTileColor(tile);
                SetMaterialColor(_tileMaterials[i], color);

                if (_tileLabels[i] != null)
                {
                    var label = tile.PositionIndex + "\n" + ShortTileName(tile.DisplayName);
                    if (property != null && property.OwnerId.HasValue)
                    {
                        label += "\nP" + property.OwnerId.Value + " L" + property.UpgradeLevel;
                    }

                    _tileLabels[i].text = label;
                }

                RefreshPropertyBuilding(tile, property);
            }

            for (var i = 0; i < _session.State.Players.Count; i++)
            {
                var player = _session.State.Players[i];
                GameObject pawn;
                if (_pawns.TryGetValue(player.Id, out pawn) && !_isMoving)
                {
                    pawn.transform.position = PawnWorldPosition(player);
                }
            }
        }

        private void RefreshPropertyBuilding(BoardTileDefinition tile, PropertyState property)
        {
            GameObject oldRoot;
            if (_buildingRoots.TryGetValue(tile.Id, out oldRoot))
            {
                Destroy(oldRoot);
                _buildingRoots.Remove(tile.Id);
            }

            if (property == null || !property.OwnerId.HasValue || property.UpgradeLevel <= 0) return;

            var root = new GameObject("Buildings_" + tile.Id);
            root.transform.SetParent(_worldRoot.transform, false);
            root.transform.position = TileWorldPosition(tile.PositionIndex);
            var material = _playerMaterials[(property.OwnerId.Value - 1) % _playerMaterials.Length];
            var count = Mathf.Clamp(property.UpgradeLevel, 1, 3);
            for (var i = 0; i < count; i++)
            {
                var x = (i - (count - 1) * 0.5f) * 0.25f;
                CreatePrimitive(
                    PrimitiveType.Cube,
                    "Building_" + i,
                    root.transform,
                    new Vector3(x, 0.32f + i * 0.10f, 0f),
                    new Vector3(0.18f, 0.28f + i * 0.08f, 0.18f),
                    material);
            }

            _buildingRoots[tile.Id] = root;
        }

        private void Execute(GameCommand command)
        {
            if (_isMoving) return;

            var fromTileIndex = _session.State.GetPlayer(command.PlayerId).CurrentTileIndex;
            var result = _session.Execute(command);
            if (!result.Succeeded)
            {
                _message = "Error: " + result.ErrorMessage;
                return;
            }

            if (result.DiceResult != null)
            {
                _message = "P" + command.PlayerId + " rolled " + result.DiceResult.Total +
                           ". Watch the 3D pawn move.";
                RefreshBoardVisuals();
                StartCoroutine(AnimatePawn(command.PlayerId, result.FromTileIndex ?? fromTileIndex, result.DiceResult.Total));
                return;
            }

            if (_session.State.Phase == TurnPhase.Finished)
            {
                _message = "Game finished.";
            }
            else
            {
                var current = _session.State.GetPlayer(_session.State.CurrentPlayerId);
                _message = "Action accepted. " + current.DisplayName + " is next.";
            }

            RefreshBoardVisuals();
        }

        private IEnumerator AnimatePawn(int playerId, int fromTileIndex, int stepCount)
        {
            GameObject pawn;
            if (!_pawns.TryGetValue(playerId, out pawn)) yield break;

            _isMoving = true;
            var startPosition = pawn.transform.position;
            for (var step = 1; step <= stepCount; step++)
            {
                var tileIndex = (fromTileIndex + step) % _session.Board.TileCount;
                var target = TileWorldPosition(tileIndex) + GetPlayerOffset(playerId);
                yield return MovePawn(pawn, startPosition, target, 0.16f);
                startPosition = target;
            }

            var player = _session.State.GetPlayer(playerId);
            pawn.transform.position = PawnWorldPosition(player);
            _isMoving = false;
            RefreshBoardVisuals();
        }

        private IEnumerator MovePawn(GameObject pawn, Vector3 from, Vector3 to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var position = Vector3.Lerp(from, to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 0.45f;
                pawn.transform.position = position;
                yield return null;
            }

            pawn.transform.position = to;
        }

        private Vector3 PawnWorldPosition(PlayerState player)
        {
            return TileWorldPosition(player.CurrentTileIndex) + GetPlayerOffset(player.Id);
        }

        private static Vector3 GetPlayerOffset(int playerId)
        {
            switch ((playerId - 1) % 4)
            {
                case 0: return new Vector3(-0.28f, 0.03f, -0.28f);
                case 1: return new Vector3(0.28f, 0.03f, -0.28f);
                case 2: return new Vector3(-0.28f, 0.03f, 0.28f);
                default: return new Vector3(0.28f, 0.03f, 0.28f);
            }
        }

        private static Vector3 TileWorldPosition(int index)
        {
            const int side = 10;
            int x;
            int z;
            if (index < side)
            {
                x = index;
                z = 0;
            }
            else if (index < 2 * side - 1)
            {
                x = side - 1;
                z = index - (side - 1);
            }
            else if (index < 3 * side - 2)
            {
                x = side - 1 - (index - (2 * side - 2));
                z = side - 1;
            }
            else
            {
                x = 0;
                z = side - 1 - (index - (3 * side - 3));
            }

            return new Vector3(x - (side - 1) * 0.5f, 0f, z - (side - 1) * 0.5f);
        }

        private Color GetTileColor(BoardTileDefinition tile)
        {
            var property = _session.State.GetProperty(tile.Id);
            if (property != null && property.OwnerId.HasValue)
            {
                return Color.Lerp(PlayerColors[(property.OwnerId.Value - 1) % PlayerColors.Length],
                    Color.white, 0.18f);
            }

            if (tile.Type == BoardTileType.Start) return new Color(0.16f, 0.57f, 0.78f);
            if (tile.Type != BoardTileType.Property) return new Color(0.28f, 0.31f, 0.40f);
            return DistrictColors[Mathf.Abs(tile.DistrictId.GetHashCode()) % DistrictColors.Length];
        }

        private static string ShortTileName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "Tile";
            return displayName.Length <= 10 ? displayName : displayName.Substring(0, 10);
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var objectToCreate = GameObject.CreatePrimitive(type);
            objectToCreate.name = name;
            objectToCreate.transform.SetParent(parent, false);
            objectToCreate.transform.localPosition = localPosition;
            objectToCreate.transform.localScale = localScale;
            var renderer = objectToCreate.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            var collider = objectToCreate.GetComponent("Collider");
            if (collider != null) Destroy(collider);
            return objectToCreate;
        }

        private static TextMesh CreateText(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            string content,
            int fontSize,
            float characterSize,
            Color color,
            TextAnchor anchor)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = localRotation;
            var text = textObject.AddComponent<TextMesh>();
            text.text = content;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        private static Material CreateMaterial(Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material;
            if (shader != null)
            {
                material = new Material(shader);
            }
            else
            {
                // Player builds may strip shaders that are only requested by name.
                // A primitive's default material is a guaranteed local fallback.
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var probeRenderer = probe.GetComponent<Renderer>();
                if (probeRenderer == null || probeRenderer.sharedMaterial == null)
                {
                    Destroy(probe);
                    return null;
                }

                material = new Material(probeRenderer.sharedMaterial);
                Destroy(probe);
            }

            SetMaterialColor(material, color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (_session == null) return;

            var panelWidth = Mathf.Min(350f, Mathf.Max(300f, Screen.width * 0.30f));
            var panelRect = new Rect(16f, 16f, panelWidth, Mathf.Max(440f, Screen.height - 32f));
            var oldColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.065f, 0.94f);
            GUI.Box(panelRect, GUIContent.none);
            GUI.color = oldColor;

            GUILayout.BeginArea(new Rect(panelRect.x + 14f, panelRect.y + 12f, panelRect.width - 28f, panelRect.height - 24f));
            GUILayout.Label("RICHMAN 3D", _titleStyle);
            GUILayout.Label("Local four-player hot-seat prototype", _smallStyle);
            GUILayout.Space(8f);
            DrawPlayers();
            DrawControls();
            GUILayout.EndArea();
        }

        private void DrawPlayers()
        {
            GUILayout.Label("Players", _headingStyle);
            for (var i = 0; i < _session.State.Players.Count; i++)
            {
                var player = _session.State.Players[i];
                var currentMarker = player.Id == _session.State.CurrentPlayerId ? "  < TURN" : string.Empty;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("P" + player.Id + "  " + player.DisplayName + currentMarker);
                GUILayout.Label("Money: $" + player.Money + "   Tile: " + player.CurrentTileIndex +
                                "   " + player.Status, _smallStyle);
                GUILayout.EndVertical();
            }

            GUILayout.Space(6f);
        }

        private void DrawControls()
        {
            var state = _session.State;
            var current = state.GetPlayer(state.CurrentPlayerId);
            GUILayout.Label("Turn " + state.TurnNumber + " | " + state.Phase, _headingStyle);
            GUILayout.Label(_message, _messageStyle);

            var canRoll = !_isMoving && state.Phase == TurnPhase.AwaitingRoll && state.Outcome == null;
            var canBuy = !_isMoving && state.Phase == TurnPhase.AwaitingAction &&
                         state.PendingAction == PendingAction.BuyProperty;
            var canUpgrade = !_isMoving && state.Phase == TurnPhase.AwaitingAction &&
                             state.PendingAction == PendingAction.UpgradeProperty;
            var canEndTurn = !_isMoving &&
                             (state.Phase == TurnPhase.AwaitingAction || state.Phase == TurnPhase.AwaitingEndTurn) &&
                             state.Outcome == null;

            GUILayout.BeginHorizontal();
            GUI.enabled = canRoll;
            if (GUILayout.Button("Roll Dice", _buttonStyle)) Execute(new RollDiceCommand(current.Id));
            GUI.enabled = canBuy;
            if (GUILayout.Button("Buy", _buttonStyle)) Execute(new BuyPropertyCommand(current.Id));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = canUpgrade;
            if (GUILayout.Button("Upgrade", _buttonStyle)) Execute(new UpgradePropertyCommand(current.Id));
            GUI.enabled = canEndTurn;
            if (GUILayout.Button("End Turn", _buttonStyle)) Execute(new EndTurnCommand(current.Id));
            GUILayout.EndHorizontal();

            GUI.enabled = !_isMoving;
            if (GUILayout.Button("New Game", _buttonStyle)) StartNewGame();
            GUI.enabled = true;

            if (state.LastDiceResult != null)
            {
                GUILayout.Label("Last roll: " + state.LastDiceResult.First + " + " + state.LastDiceResult.Second +
                                " = " + state.LastDiceResult.Total, _smallStyle);
            }

            var currentTile = state.Board.GetTileAt(current.CurrentTileIndex);
            GUILayout.Label("Current tile: " + currentTile.DisplayName + " (" + currentTile.Type + ")", _smallStyle);
            if (state.Outcome != null)
            {
                GUILayout.Label("Winner: P" + state.Outcome.WinnerId + " - start a New Game.", _headingStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fixedHeight = 36f
            };
            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
        }
    }
}
