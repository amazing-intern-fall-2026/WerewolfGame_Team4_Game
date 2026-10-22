using System;
using System.IO;
using System.Linq;
using Thuong.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ThuongMapVisualChecks
{
    [MenuItem("Tools/Thuong/Maps/Render All Map Event Combinations")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Directory.CreateDirectory("Logs/Maps/Views");
        var catalog=Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
        var visuals=Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath);
        var cast=AssetDatabase.LoadAssetAtPath<ThuongMedievalCast>("Assets/Scenes/Thuong/Prototype/Art/Medieval/Unity/MedievalCast.asset");
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Thuong/Prototype/Art/World.mat");
        int renders=0;
        foreach (var definition in catalog.Released)
        {
            var root=new GameObject("Map visual QA"); var map=root.AddComponent<ThuongMapRuntime>();
            if (!map.Bind(definition,out string error)) throw new Exception(error);
            for (int i=0;i<25;i++)
            {
                var avatar=new GameObject("QA avatar "+i,typeof(SpriteRenderer)); avatar.transform.SetParent(map.World.transform,false);
                avatar.transform.position=definition.spawns[i]; var sprite=avatar.GetComponent<SpriteRenderer>();
                sprite.sprite=cast.idleSprites[i%cast.Count]; sprite.sharedMaterial=material;
                sprite.sortingOrder=ThuongDepthSort.Order(avatar.transform.position.y);
            }
            foreach (var station in map.Stations) station.marker.SetActive(true);
            var events=root.AddComponent<ThuongEventPresentation>(); events.Bind(map,visuals);
            var cameraRoot=new GameObject("QA camera",typeof(Camera)); var camera=cameraRoot.GetComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=13; camera.aspect=8f/7;
            camera.transform.position=new Vector3(1,1,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.14f,.17f,.13f);
            camera.transparencySortMode=TransparencySortMode.CustomAxis; camera.transparencySortAxis=Vector3.up;
            foreach (var phase in new[]{GamePhase.Task,GamePhase.Night})
            {
                events.Apply(GameEventType.None,1,phase,true); Save(camera,definition.mapId+"_"+phase); renders++;
            }
            foreach (GameEventType type in Enum.GetValues(typeof(GameEventType)))
            {
                if (type==GameEventType.None) continue;
                events.Apply(type,2,type==GameEventType.ClearSky||type==GameEventType.HarvestFestival?GamePhase.Task:GamePhase.Night,true);
                Save(camera,definition.mapId+"_"+type); renders++;
                if (Object.FindObjectsByType<Camera>().Length!=1 || Object.FindObjectsByType<AudioSource>().Length>1)
                    throw new Exception("Map event leaked camera or audio source");
            }
            Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot);
        }
        File.WriteAllText("Logs/Maps/visuals.txt","THUONG_MAP_VISUALS_PASSED: "+renders+" rendered views; 25 avatars per layout\n");
        Debug.Log("THUONG_MAP_VISUALS_PASSED: "+renders);
    }
    private static void Save(Camera camera,string name)
    {
        var previous=RenderTexture.active; var target=new RenderTexture(960,840,24); camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var texture=new Texture2D(960,840,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,960,840),0,0); texture.Apply();
        File.WriteAllBytes("Logs/Maps/Views/"+name+".png",texture.EncodeToPNG());
        RenderTexture.active=previous; camera.targetTexture=null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
    }
}
