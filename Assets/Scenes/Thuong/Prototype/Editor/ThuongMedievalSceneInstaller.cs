using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Thuong;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ThuongMedievalSceneInstaller
{
    public const string Art = ThuongPrototypeBuilder.Root + "/Art/Medieval";
    public const string World = Art + "/Sprites/World";
    public const string Generated = Art + "/Unity";
    private static Material worldMaterial;
    private static TMP_FontAsset font;

    [MenuItem("Tools/Thuong/Apply Medieval Top-Down Art")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(Art + "/ImportManifest.json")) throw new FileNotFoundException("Run Tools/prepare_thuong_medieval_assets.py first.");
        Directory.CreateDirectory("Logs");
        File.Copy(ThuongPrototypeBuilder.ScenePath, "Logs/ThuongGameplayPrototype-before-medieval-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".unity");
        EnsureFolder(Generated);
        EnsureFolder(Generated + "/Animations");
        AssetDatabase.Refresh();
        ImportSprites();
        var cast = BuildCast();
        var scene = EditorSceneManager.OpenScene(ThuongPrototypeBuilder.ScenePath);
        var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
        var stations = components.OfType<TaskStation>().OrderBy(s => s.task.name).ToArray();
        var players = components.OfType<PlayerManager>().Single();
        var game = components.OfType<GameRoleManager>().Single();
        var tasks = stations.Select(s => s.task).ToArray();
        int lobby = players.prototypeLobbySize;
        var durations = (game.roleRevealDuration, game.nightDuration, game.discussionDuration, game.votingDuration);
        worldMaterial = AssetDatabase.LoadAssetAtPath<Material>(ThuongPrototypeBuilder.Root + "/Art/World.mat");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThuongPrototypeBuilder.Root + "/Art/PrototypeFont.asset");
        var camera = components.OfType<Camera>().Single(c => c.CompareTag("MainCamera"));
        camera.orthographic = true;
        camera.orthographicSize = 6.5f;
        camera.rect = new Rect(.24f, .17f, .75f, .69f);
        camera.backgroundColor = new Color32(39, 48, 35, 255);
        camera.transparencySortMode = TransparencySortMode.CustomAxis;
        camera.transparencySortAxis = Vector3.up;
        camera.transform.rotation = Quaternion.identity;
        var follow = camera.GetComponent<ThuongTopDownCamera>() ?? camera.gameObject.AddComponent<ThuongTopDownCamera>();
        var player = components.OfType<PlayerMovement>().Single();
        player.transform.position = new Vector3(1, 0, 0);
        ConfigurePlayer(player.gameObject, cast);
        follow.Follow(player.transform);
        PrefabUtility.SaveAsPrefabAsset(player.gameObject, ThuongGameplayIntegrationInstaller.Root + "/Prefabs/GameplayPlayer.prefab");
        components.OfType<ThuongFourRoleOfflineTest>().Single().playerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(ThuongGameplayIntegrationInstaller.Root + "/Prefabs/GameplayPlayer.prefab");
        var map = scene.GetRootGameObjects().Single(r => r.name == "Map").transform;
        BuildVillage(map);
        ConfigureStations(stations);
        PrefabUtility.SaveAsPrefabAsset(stations[0].gameObject, ThuongGameplayIntegrationInstaller.Root + "/Prefabs/TaskStation.prefab");
        ApplyHUD(components.OfType<GameHUD>().Single());
        ApplyRoleArt(components.OfType<RoleManager>().Single());
        if (players.prototypeLobbySize != lobby || durations != (game.roleRevealDuration, game.nightDuration, game.discussionDuration, game.votingDuration) ||
            stations.Where((s, i) => s.task != tasks[i] || s.player != player.transform).Any())
            throw new InvalidOperationException("Presentation installation changed gameplay wiring.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Debug.Log("THUONG_MEDIEVAL_ART_INSTALLED: original scene, 20 ID-based skins, six stations, village, UI and top-down camera.");
    }

    public static void InstallAndCheck()
    {
        Install();
        ThuongMedievalSceneChecks.CheckSavedScene();
        ThuongGameplayIntegrationChecks.Run();
        VoteClickRegression.Run();
        ThuongMedievalSceneChecks.RunPlayMode();
    }

    private static void ImportSprites()
    {
        foreach (string path in Directory.GetFiles(Art + "/Sprites", "*.png", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(Art + "/Resources", "*.png", SearchOption.AllDirectories)))
        {
            string assetPath = path.Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 48;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0);
            importer.SetTextureSettings(settings);
            if (assetPath.Contains("/Characters/"))
            {
                int columns = assetPath.Contains("walkcycle") ? 9 : 6;
                int rows = assetPath.Contains("walkcycle") ? 4 : 1;
                var sprites = new List<SpriteMetaData>();
                for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    sprites.Add(new SpriteMetaData { name = Path.GetFileNameWithoutExtension(path) + $"_r{row}_c{column}",
                        rect = new Rect(column*64, (rows-row-1)*64, 64, 64),
                        alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, 7f/64f) });
                importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable 0618
                importer.spritesheet = sprites.ToArray();
#pragma warning restore 0618
            }
            else
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                if (assetPath.EndsWith("Grass.png") || assetPath.EndsWith("Cobblestone.png") || assetPath.EndsWith("Soil.png"))
                    importer.spritePivot = new Vector2(.5f, .5f);
                if (assetPath.Contains("/Resources/")) importer.spritePivot = new Vector2(.5f, .5f);
                if (assetPath.EndsWith("PaperPanel.png")) importer.spriteBorder = new Vector4(16, 12, 16, 12);
                if (assetPath.EndsWith("WoodPanel.png")) importer.spriteBorder = new Vector4(12, 12, 12, 12);
            }
            importer.SaveAndReimport();
        }
    }

    private static ThuongMedievalCast BuildCast()
    {
        var cast = AssetDatabase.LoadAssetAtPath<ThuongMedievalCast>(Generated + "/MedievalCast.asset");
        if (cast == null) { cast = ScriptableObject.CreateInstance<ThuongMedievalCast>(); AssetDatabase.CreateAsset(cast, Generated + "/MedievalCast.asset"); }
        cast.idleSprites = new Sprite[20];
        cast.controllers = new RuntimeAnimatorController[20];
        Vector2[] directions = { Vector2.up, Vector2.left, Vector2.down, Vector2.right };
        for (int index = 0; index < 20; index++)
        {
            string name = $"Villager{index+1:00}";
            var walk = AssetDatabase.LoadAllAssetsAtPath(Art + "/Sprites/Characters/" + name + "_walkcycle.png").OfType<Sprite>().ToArray();
            var death = AssetDatabase.LoadAllAssetsAtPath(Art + "/Sprites/Characters/" + name + "_hurt.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            Sprite Frame(int row, int column) => walk.Single(s => s.name.EndsWith($"_r{row}_c{column}"));
            cast.idleSprites[index] = Frame(2, 0);
            string controllerPath = Generated + "/Animations/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
                controller.AddParameter("isDead", AnimatorControllerParameterType.Bool);
                controller.AddParameter("LastInputX", AnimatorControllerParameterType.Float);
                controller.AddParameter("LastInputY", AnimatorControllerParameterType.Float);
                var machine = controller.layers[0].stateMachine;
                var idle = machine.AddState("Idle");
                var walking = machine.AddState("Walking");
                var dead = machine.AddState("Death");
                machine.defaultState = idle;
                var idleTree = new BlendTree { name = "Idle directions", blendType = BlendTreeType.SimpleDirectional2D,
                    blendParameter = "LastInputX", blendParameterY = "LastInputY" };
                var walkTree = new BlendTree { name = "Walk directions", blendType = BlendTreeType.SimpleDirectional2D,
                    blendParameter = "LastInputX", blendParameterY = "LastInputY" };
                AssetDatabase.AddObjectToAsset(idleTree, controller);
                AssetDatabase.AddObjectToAsset(walkTree, controller);
                for (int row = 0; row < 4; row++)
                {
                    idleTree.AddChild(Clip(name + "_Idle_" + row, new[] { Frame(row, 0) }, 1, true), directions[row]);
                    walkTree.AddChild(Clip(name + "_Walk_" + row, Enumerable.Range(1, 8).Select(column => Frame(row, column)).ToArray(), 9, true), directions[row]);
                }
                idle.motion = idleTree;
                walking.motion = walkTree;
                dead.motion = Clip(name + "_Death", death, 8, false);
                Transition(idle.AddTransition(walking), "isWalking", AnimatorConditionMode.If);
                Transition(walking.AddTransition(idle), "isWalking", AnimatorConditionMode.IfNot);
                var die = machine.AddAnyStateTransition(dead);
                die.canTransitionToSelf = false;
                Transition(die, "isDead", AnimatorConditionMode.If);
                Transition(dead.AddTransition(idle), "isDead", AnimatorConditionMode.IfNot);
            }
            cast.controllers[index] = controller;
        }
        EditorUtility.SetDirty(cast);
        return cast;
    }

    private static AnimationClip Clip(string name, Sprite[] sprites, float rate, bool loop)
    {
        string path = Generated + "/Animations/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = rate;
        var binding = new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        var keys = sprites.Select((sprite, index) => new ObjectReferenceKeyframe { time = index/rate, value = sprite }).ToList();
        keys.Add(new ObjectReferenceKeyframe { time = sprites.Length/rate, value = loop ? sprites[0] : sprites.Last() });
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void Transition(AnimatorStateTransition transition, string parameter, AnimatorConditionMode condition)
    {
        transition.hasExitTime = false; transition.duration = 0; transition.AddCondition(condition, 0, parameter);
    }

    private static void ConfigurePlayer(GameObject root, ThuongMedievalCast cast)
    {
        root.transform.localScale = Vector3.one;
        var body = root.GetComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        var collider = root.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(.42f, .23f); collider.offset = new Vector2(0, .12f);
        var old = root.GetComponent<SpriteRenderer>(); if (old != null) old.enabled = false;
        var view = root.GetComponentInChildren<ThuongCharacterView>();
        view.appearance.sprite = cast.idleSprites[0]; view.appearance.tint = Color.white;
        view.appearance.visualScale = new Vector3(.90f, .90f, 1);
        view.appearance.animatorController = cast.controllers[0];
        EditorUtility.SetDirty(view.appearance); view.ApplyAppearance();
        view.transform.localPosition = Vector3.zero;
        var character = view.GetComponent<ThuongTopDownCharacter>() ?? view.gameObject.AddComponent<ThuongTopDownCharacter>();
        character.cast = cast; character.spriteRenderer = view.spriteRenderer; character.animator = view.animator;
        view.spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        if (character.selectionRing == null)
        {
            var ring = new GameObject("Selected player", typeof(LineRenderer)); ring.transform.SetParent(root.transform, false);
            character.selectionRing = ring.GetComponent<LineRenderer>();
        }
        var line = character.selectionRing;
        line.sharedMaterial = worldMaterial;
        line.useWorldSpace = false; line.loop = true; line.positionCount = 32; line.widthMultiplier = .035f;
        line.startColor = line.endColor = new Color32(255, 211, 91, 255);
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2/32;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle)*.34f, Mathf.Sin(angle)*.13f+.03f, 0));
        }
        line.enabled = false;
        character.SetPlayerID(0);
    }

    private static void BuildVillage(Transform map)
    {
        foreach (var renderer in map.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
        foreach (var collider in map.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        var previous = map.Find("Medieval Village"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var village = new GameObject("Medieval Village").transform; village.SetParent(map, false);
        var gridRoot = new GameObject("Terrain Grid", typeof(Grid)); gridRoot.transform.SetParent(village, false);
        Tilemap floor = Tilemap("Grass", gridRoot.transform, "Grass", -200);
        Tilemap paths = Tilemap("Village lanes", gridRoot.transform, "Cobblestone", -190);
        TileBase grass = TileAsset("Grass"), cobble = TileAsset("Cobblestone");
        for (int y = -11; y < 13; y++)
        for (int x = -13; x < 15; x++)
        {
            var cell = new Vector3Int(x, y, 0); floor.SetTile(cell, grass);
            bool lane = (y >= -1 && y <= 1) || (x >= 0 && x <= 1) ||
                        (x >= -4 && x <= 5 && y >= -3 && y <= 3) ||
                        (y == 4 && x >= -10 && x <= 11) || (y == -5 && x >= -10 && x <= 10) ||
                        ((x == -7 || x == 8) && y >= -6 && y <= 5);
            if (lane) paths.SetTile(cell, cobble);
        }
        Vector2[] houses = { new Vector2(-8, 6), new Vector2(1, 7), new Vector2(10, 6),
            new Vector2(-9, -8), new Vector2(1, -9), new Vector2(10, -8) };
        for (int i = 0; i < houses.Length; i++)
        {
            var house = Prop("House " + (i+1), village, "House" + (i%3+1), houses[i]);
            Obstacle(house, new Vector2(4.5f, 1.8f), new Vector2(0, .9f));
            Prop("Barrel", village, "Barrel", houses[i] + new Vector2(-2.7f, -.05f));
            Prop("Planter", village, "Planter", houses[i] + new Vector2(2.3f, -.05f));
        }
        for (int x = -12; x <= 14; x += 2)
        {
            Tree(village, new Vector2(x, 11.7f));
            if (Mathf.Abs(x-1) > 2) Tree(village, new Vector2(x, -10.5f));
        }
        for (int y = -9; y <= 10; y += 2)
        {
            Tree(village, new Vector2(-12.2f, y)); Tree(village, new Vector2(14.3f, y));
        }
        Tree(village, new Vector2(-10.8f, -4.7f)); Tree(village, new Vector2(-9.8f, -5.5f));
        Prop("Market stall", village, "MarketStall", new Vector2(5, -7));
        Prop("Market cart", village, "Cart", new Vector2(5.5f, -5.7f));
        Prop("Sacks", village, "Sacks", new Vector2(4, -6.7f));
        Prop("Hay stack", village, "Hay", new Vector2(-9.3f, 1.5f));
        Prop("Village sign", village, "Signpost", new Vector2(2.9f, 3.7f));
        for (int i = 0; i < 4; i++) Torch(village, new Vector2(i%2 == 0 ? -3.9f : 5.9f, i < 2 ? 3.3f : -3.3f));
        Boundary(village, new Vector2(1, -11.2f), new Vector2(28.6f, .4f));
        Boundary(village, new Vector2(1, 13.2f), new Vector2(28.6f, .4f));
        Boundary(village, new Vector2(-13.2f, 1), new Vector2(.4f, 24.8f));
        Boundary(village, new Vector2(15.2f, 1), new Vector2(.4f, 24.8f));
        village.gameObject.AddComponent<ThuongVillageMood>();
    }

    private static void ConfigureStations(TaskStation[] stations)
    {
        if (stations.Length != 6) throw new InvalidOperationException("Expected six original task stations.");
        Vector2[] positions = { new Vector2(-6, -2), new Vector2(-7, 3), new Vector2(8, 3),
            new Vector2(-7, -6), new Vector2(8, -4), new Vector2(1, 5) };
        string[] sprites = { "Well", "Wheat", "Fence", "Woodpile", "CrateStack", "Torch0" };
        string[] labels = { "GIẾNG NƯỚC", "RUỘNG LÚA", "HÀNG RÀO", "BÃI CỦI", "KHO LÀNG", "ĐÈN LÀNG" };
        for (int i = 0; i < 6; i++)
        {
            TaskStation station = stations[i]; station.transform.localScale = Vector3.one;
            station.transform.position = positions[i]; station.interactionRadius = 1.3f;
            station.GetComponent<SpriteRenderer>().enabled = false;
            var previous = station.transform.Find("Medieval station art"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var art = new GameObject("Medieval station art").transform; art.SetParent(station.transform, false);
            if (i == 1)
            {
                for (int row = 0; row < 3; row++)
                for (int column = 0; column < 5; column++)
                {
                    Vector2 point = positions[i] + new Vector2((column-2)*2f/3f, row*2f/3f);
                    var soil = Prop("Ploughed soil", art, "Soil", point);
                    soil.transform.position += new Vector3(0, 1f/3f, 0);
                    soil.GetComponent<ThuongDepthSort>().enabled = false; soil.GetComponent<SpriteRenderer>().sortingOrder = -180;
                    Prop("Wheat", art, "Wheat", point);
                }
            }
            else
            {
                var main = Prop(labels[i], art, sprites[i], positions[i]);
                if (i == 0) Obstacle(main, new Vector2(1, .5f), new Vector2(0, .25f));
                if (i == 2)
                {
                    Obstacle(main, new Vector2(2, .18f), new Vector2(0, .13f));
                    Prop("Fence left", art, "Fence", positions[i] + new Vector2(-2, 0));
                    Prop("Fence right", art, "Fence", positions[i] + new Vector2(2, 0));
                }
                if (i == 4) { Obstacle(main, new Vector2(1.5f, .45f), new Vector2(0, .25f)); Prop("Store barrel", art, "Barrel", positions[i] + new Vector2(1.3f, 0)); }
                if (i == 5)
                {
                    var loop = main.AddComponent<ThuongSpriteLoop>(); loop.frames = TorchFrames(); loop.staysBrightAtNight = true;
                }
            }
            float height = i == 4 ? 2.2f : i == 1 ? 2.1f : 1.35f;
            var marker = station.marker.GetComponent<SpriteRenderer>(); marker.sprite = Sprite("QuestMarker");
            marker.color = Color.white; marker.drawMode = SpriteDrawMode.Simple; marker.sharedMaterial = worldMaterial;
            marker.sortingOrder = 4800; marker.transform.localScale = Vector3.one;
            marker.transform.localPosition = new Vector3(0, height, 0);
            WorldLabel("Interact key", marker.transform, "E", Vector2.zero, 3.5f, new Vector2(.7f, .7f));
            var label = station.transform.Find("Label").GetComponent<TextMeshPro>();
            label.text = labels[i]; label.font = font; label.fontSize = 3.8f;
            label.color = new Color32(255, 241, 194, 255);
            label.rectTransform.sizeDelta = new Vector2(4, .6f);
            label.transform.localPosition = new Vector3(0, height+.55f, 0);
            label.transform.localScale = Vector3.one; label.GetComponent<Renderer>().sortingOrder = 4800;
        }
    }

    private static void ApplyHUD(GameHUD hud)
    {
        var theme = hud.GetComponent<ThuongMedievalTheme>() ?? hud.gameObject.AddComponent<ThuongMedievalTheme>();
        theme.paper = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Resources/MedievalUI/PaperPanel.png");
        theme.wood = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Resources/MedievalUI/WoodPanel.png");
        theme.defaultPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Resources/MedievalUI/RoleIcon.png");
        theme.ApplyHUD();
        hud.transform.Find("TopBar/Title").GetComponent<TMP_Text>().text = "MA SÓI  /  LÀNG TRUNG CỔ";
        if (hud.progressSlider.fillRect.TryGetComponent(out Image fill)) fill.color = new Color32(126, 158, 77, 255);
        if (hud.dailyProgressSlider.fillRect.TryGetComponent(out Image daily)) daily.color = new Color32(203, 147, 62, 255);
    }

    private static void ApplyRoleArt(RoleManager roles)
    {
        foreach (var definition in roles.roleDefinitions.definitions)
        {
            definition.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Resources/RolePortraits/" + definition.roleType + ".png");
            definition.abilityIcon = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Resources/MedievalUI/RoleIcon.png");
            EditorUtility.SetDirty(definition);
        }
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(World + "/" + name + ".png") ?? throw new FileNotFoundException(name);
    private static Sprite[] TorchFrames() => Enumerable.Range(0, 6).Select(i => Sprite("Torch"+i)).ToArray();
    private static GameObject Prop(string name, Transform parent, string sprite, Vector2 position)
    {
        var go = new GameObject(name, typeof(SpriteRenderer), typeof(ThuongDepthSort));
        go.transform.SetParent(parent, false); go.transform.position = position;
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = Sprite(sprite);
        renderer.sharedMaterial = worldMaterial; renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.sortingOrder = ThuongDepthSort.Order(position.y);
        return go;
    }
    private static void Tree(Transform parent, Vector2 position)
    {
        var tree = Prop("Tree", parent, "Tree", position); Obstacle(tree, new Vector2(.40f, .28f), new Vector2(0, .25f));
    }
    private static void Torch(Transform parent, Vector2 position)
    {
        var go = Prop("Lantern", parent, "Torch0", position);
        var loop = go.AddComponent<ThuongSpriteLoop>(); loop.frames = TorchFrames(); loop.staysBrightAtNight = true;
    }
    private static void Obstacle(GameObject go, Vector2 size, Vector2 offset)
    { var collider = go.AddComponent<BoxCollider2D>(); collider.size = size; collider.offset = offset; }
    private static void Boundary(Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject("Village boundary", typeof(BoxCollider2D)); go.transform.SetParent(parent, false);
        go.transform.position = position; go.GetComponent<BoxCollider2D>().size = size;
    }
    private static Tilemap Tilemap(string name, Transform parent, string sprite, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sharedMaterial = worldMaterial; renderer.sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }
    private static TileBase TileAsset(string name)
    {
        string path = Generated + "/" + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile, path); }
        tile.sprite = Sprite(name); tile.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tile); return tile;
    }
    private static void WorldLabel(string name, Transform parent, string value, Vector2 localPosition, float size, Vector2 dimensions)
    {
        var existing = parent.Find(name); if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var go = new GameObject(name, typeof(TextMeshPro)); go.transform.SetParent(parent, false); go.transform.localPosition = localPosition;
        var text = go.GetComponent<TextMeshPro>(); text.font = font; text.text = value; text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = dimensions;
        text.color = new Color32(255, 233, 166, 255); text.GetComponent<Renderer>().sortingOrder = 4801;
    }
    private static void EnsureFolder(string path)
    { if (!AssetDatabase.IsValidFolder(path)) { EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); } }
}
