using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MorningFogGreyboxBuilder
{
    public const string Output = "Temp/EditorTools/晨雾花谷_实施_20261003";
    private const string CommandFile = "Library/MorningFogGreybox.command";
    private static double nextPoll;
    static MorningFogGreyboxBuilder() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(CommandFile)) return;
        string command = File.ReadAllText(CommandFile).Trim();
        if (command.Length == 0) return;
        File.WriteAllText(CommandFile, "");
        try
        {
            Directory.CreateDirectory(Output);
            if (command == "inspect") Inspect();
            else if (command == "backup") Backup();
            else if (command == "build") Build();
            else if (command == "validate") Validate();
            else if (command == "render") RenderViews();
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "collision") CollisionChecks();
            else if (command == "play-start") StartRuntimeCheck();
            else if (command == "play-stop") EditorApplication.isPlaying=false;
            else if (command == "console") ConsoleCounts();
            else if (command == "restore-particles") RestoreParticleSerialization();
            else throw new InvalidOperationException("未知命令：" + command);
            File.WriteAllText(Output + "/last-result.txt", command + " OK " + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception error)
        {
            File.WriteAllText(Output + "/last-result.txt", command + " FAILED " + error);
            Debug.LogException(error);
        }
    }

    private static Scene RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出播放状态，避免保存运行时对象。");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/game.unity" || SceneManager.sceneCount != 1) throw new InvalidOperationException("必须仅载入真实 game 场景。");
        return scene;
    }

    private static IEnumerable<GameObject> Objects(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    }

    [Serializable] public class ObjectRecord
    {
        public string id, name, parent;
        public Vector3 position, scale, rotation;
        public bool active;
        public string[] components;
    }
    [Serializable] public class StarRecord
    {
        public int memoryId;
        public string name, memoryGuid, prefab, id, serialized;
        public Vector3 position, scale;
    }
    [Serializable] public class Snapshot
    {
        public string scene, timestamp;
        public bool dirty, playing;
        public ObjectRecord[] objects;
        public StarRecord[] stars;
    }

    private static StarRecord[] Stars(Scene scene)
    {
        return Objects(scene).SelectMany(g => g.GetComponents<StardustPickup>()).Select(p =>
        {
            SerializedObject serialized = new SerializedObject(p);
            var memory = serialized.FindProperty("memory").objectReferenceValue as StardustMemoryData;
            return new StarRecord { memoryId = memory != null ? memory.id : -1, name = p.name,
                memoryGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(memory)),
                prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p.gameObject),
                id = GlobalObjectId.GetGlobalObjectIdSlow(p).ToString(), serialized = EditorJsonUtility.ToJson(p),
                position = p.transform.position, scale = p.transform.localScale };
        }).OrderBy(s => s.memoryId).ToArray();
    }

    private static void Inspect()
    {
        Scene scene = SceneManager.GetActiveScene();
        Snapshot snapshot = new Snapshot {scene = scene.path, dirty = scene.isDirty, playing = EditorApplication.isPlaying,
            timestamp = DateTime.UtcNow.ToString("O"), stars = Stars(scene),
            objects = Objects(scene).Select(g => new ObjectRecord {id = GlobalObjectId.GetGlobalObjectIdSlow(g).ToString(),
                name = g.name, parent = g.transform.parent != null ? g.transform.parent.name : "", active = g.activeSelf,
                position = g.transform.position, scale = g.transform.localScale, rotation = g.transform.eulerAngles,
                components = g.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray()}).ToArray()};
        File.WriteAllText(Output + "/LiveSceneSnapshot.json", JsonUtility.ToJson(snapshot, true));
    }

    private static void Backup()
    {
        Scene scene = RequireScene();
        Inspect();
        string path = Output + "/备份/game_内存场景.unity";
        if (File.Exists(path)) throw new InvalidOperationException("内存备份已存在，不覆盖原件。");
        if (!EditorSceneManager.SaveScene(scene, path, true)) throw new InvalidOperationException("内存场景备份失败。");
        var all = Objects(scene).SelectMany(g => g.GetComponents<Component>()).Where(c => c != null);
        File.WriteAllLines(Output + "/备份/组件基线.tsv", all.Select(c => GlobalObjectId.GetGlobalObjectIdSlow(c) + "\t" + c.GetType().Name + "\t" + Hash(EditorJsonUtility.ToJson(c))));
    }

    private static string Hash(string value)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
    }

    private const string AssetFolder = "Assets/MorningFogGreybox";
    private static readonly Vector3[] Route = {
        new Vector3(3,0,-5), new Vector3(3,0,9), new Vector3(14,.4f,14), new Vector3(24,1.2f,24),
        new Vector3(24,.6f,33), new Vector3(18,0,40), new Vector3(10,.75f,40), new Vector3(10,.75f,44),
        new Vector3(10,.75f,47), new Vector3(0,.75f,50), new Vector3(-13,1.6f,44), new Vector3(-24,2.6f,34),
        new Vector3(-25,3.5f,24), new Vector3(-23,4.2f,16), new Vector3(-24,3.2f,6), new Vector3(-22,2,-6),
        new Vector3(-14,0,-6), new Vector3(-5,0,-6), new Vector3(-3.943799f,0,-4.8f)
    };
    private static readonly Vector3[] StarPositions = {
        new Vector3(3,1.05f,9), new Vector3(24,2.25f,24), new Vector3(10,1.8f,47),
        new Vector3(-24,3.65f,34), new Vector3(-23,5.25f,16)
    };
    private struct Pad
    {
        public Vector3 center;
        public Vector2 size;
        public Pad(Vector3 c, Vector2 s) { center = c; size = s; }
    }
    private static readonly Pad[] Pads = {
        new Pad(new Vector3(24,1.2f,24),new Vector2(10,10)),
        new Pad(new Vector3(10,.75f,47),new Vector2(8,6)),
        new Pad(new Vector3(-24,2.6f,34),new Vector2(10,10)),
        new Pad(new Vector3(-23,4.2f,16),new Vector2(12,10))
    };
    private static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }
    private static bool Rect(float x, float z, float x0, float x1, float z0, float z1)
    { return x >= x0 && x <= x1 && z >= z0 && z <= z1; }
    private static bool Supported(float x, float z)
    {
        return Rect(x,z,-28,28,-10,54) || Rect(x,z,-27,27,-18,-8) || Rect(x,z,-38,-26,-6,43)
            || Rect(x,z,26,38,0,46) || Rect(x,z,-28,28,50,62);
    }
    private static float PadDistance(Pad pad, Vector2 p)
    {
        Vector2 delta = p - XZ(pad.center);
        return new Vector2(Mathf.Max(0,Mathf.Abs(delta.x)-pad.size.x/2),Mathf.Max(0,Mathf.Abs(delta.y)-pad.size.y/2)).magnitude;
    }

    // 平台边缘是实际坡道锚点，不以平台中心间的低坡度掩盖短接坡。
    private static float PadExit(Vector3 from, Vector3 to)
    {
        float result = 0;
        foreach (Pad pad in Pads)
        {
            if (Vector2.Distance(XZ(from),XZ(pad.center)) > .01f) continue;
            Vector2 direction = XZ(to - from);
            float tx = Mathf.Abs(direction.x) > .001f ? pad.size.x/2/Mathf.Abs(direction.x) : 1;
            float tz = Mathf.Abs(direction.y) > .001f ? pad.size.y/2/Mathf.Abs(direction.y) : 1;
            result = Mathf.Min(tx,tz);
        }
        return Mathf.Clamp01(result);
    }
    private static void RouteSample(Vector2 p, out float distance, out float height, out int segment)
    {
        distance = float.PositiveInfinity; height = 0; segment = 0;
        for (int i=0;i<Route.Length-1;i++)
        {
            if (i>=5 && i<=7) continue;
            Vector2 a=XZ(Route[i]), b=XZ(Route[i+1]), delta=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
            float d=Vector2.Distance(p,a+delta*t);
            float start=PadExit(Route[i],Route[i+1]);
            float end=1-PadExit(Route[i+1],Route[i]);
            float localHeight=Mathf.Lerp(Route[i].y,Route[i+1].y,Mathf.InverseLerp(start,end,t));
            if(d<distance) { distance=d; segment=i; height=localHeight; }
        }
        // 先减去最近距离平方，避免远处权重过小引发突然退回分段高度。
        float weightedHeight=0,weightTotal=0;
        for(int i=0;i<Route.Length-1;i++)
        {
            if(i>=5&&i<=7) continue;
            Vector2 a=XZ(Route[i]),delta=XZ(Route[i+1])-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
            float d=Vector2.Distance(p,a+delta*t);
            float localHeight=Mathf.Lerp(Route[i].y,Route[i+1].y,Mathf.InverseLerp(PadExit(Route[i],Route[i+1]),1-PadExit(Route[i+1],Route[i]),t));
            float weight=Mathf.Exp(-(d*d-distance*distance)/24);
            weightedHeight+=localHeight*weight; weightTotal+=weight;
        }
        height=weightedHeight/weightTotal;
    }
    private static float LandHeight(float x, float z)
    {
        Vector2 p=new Vector2(x,z);
        RouteSample(p,out float distance,out float routeHeight,out int segment);
        float h=Mathf.Max(0, routeHeight-Mathf.Max(0,distance-5)*.35f);
        foreach (Pad pad in Pads) h=Mathf.Max(h,pad.center.y-PadDistance(pad,p)*.35f);
        float ridge=4.1f*Mathf.Clamp01((9.7f-Vector2.Distance(p,new Vector2(-14,10)))/7.7f);
        // 外围低丘避让主路、平台和支撑边界，不把预留矩形整块生成。
        float rim=0;
        if(z>=55) rim=Mathf.Max(rim,3.2f*Mathf.Clamp01((z-55)/7)*Mathf.Clamp01((28-Mathf.Abs(x))/5));
        if(x>=29 && z>=3 && z<=43) rim=Mathf.Max(rim,3*Mathf.Clamp01((x-29)/9)*Mathf.Clamp01((20-Mathf.Abs(z-23))/5));
        if(x<=-30 && z>=19 && z<=41) rim=Mathf.Max(rim,4.5f*Mathf.Clamp01((-30-x)/8)*Mathf.Clamp01((11-Mathf.Abs(z-30))/5));
        if(x<=-17 && z<=-4 && Supported(x,z)) rim=Mathf.Max(rim,2.5f*Mathf.Clamp01(1-Vector2.Distance(p,new Vector2(-25,-10))/8));
        h=Mathf.Max(h,Mathf.Min(rim,Mathf.Max(0,distance-6)*.4f));
        // 平地保护边缘连续收坡，避免把已有高程突然截成直墙。
        float protectedDistance=Mathf.Min(Mathf.Max(0,Vector2.Distance(p,new Vector2(-3.943799f,-1.5117153f))-5.5f),
            Mathf.Max(0,Vector2.Distance(p,new Vector2(3,-5))-3));
        float hubDistance=new Vector2(Mathf.Max(0,Mathf.Max(-14-x,x-12)),Mathf.Max(0,Mathf.Max(-14-z,z))).magnitude;
        float distanceToFlat=Mathf.Min(protectedDistance,hubDistance);
        h=Mathf.Max(Mathf.Min(h,distanceToFlat*.35f),Mathf.Min(ridge,distanceToFlat*.52f));
        // 停留平台全域保持指定高程，外围道路不得覆盖台面抬高或削掉平台角点。
        foreach(Pad pad in Pads) h=Mathf.Max(h,Mathf.Min(pad.center.y-PadDistance(pad,p)*.35f,distanceToFlat*.52f));
        foreach(Pad pad in Pads) h=Mathf.Min(h,pad.center.y+PadDistance(pad,p)*.35f);
        // 谷底分区的外边缘也连续落地，只有试跳板的指定边缘保留竖向台阶。
        if((x>=20 && z>=35.5f && z<=50) || (z<=37 && x>=6 && x<=21.5f))
            h*=Mathf.Clamp01(new Vector2(Mathf.Max(0,x-20),Mathf.Max(0,37-z)).magnitude/1.5f);
        if(x<=6 && z>=37 && z<44) h*=Mathf.Clamp01((6-x)/1.5f);
        if(z>=50 && x>=14 && x<=20) h*=Mathf.Clamp01((z-50)/1.5f);
        return Mathf.Max(0,h);
    }

    // 03 分区使用精确边界；沟底不接受宽路肩或平台距离场填充。
    private static int Zone(float x, float z)
    {
        if(Rect(x,z,8,12,37,41.4f) || Rect(x,z,8,12,42.6f,44) || Rect(x,z,6,14,44,50)) return 1;
        if(Rect(x,z,12,18,38.5f,41.4f)) return 2;
        if(Rect(x,z,14,17,45.5f,48.5f)) return 3;
        if(Rect(x,z,6,20,37,50)) return 0;
        return 4;
    }
    private static float Height(int zone,float x,float z)
    {
        if(zone==0) return 0;
        if(zone==1) return .75f;
        if(zone==2) return .75f*Mathf.Clamp01((18-x)/6);
        if(zone==3) return .75f*Mathf.Clamp01((17-x)/3);
        return LandHeight(x,z);
    }
    private static float[] Axis(float minimum,float maximum,params float[] extra)
    {
        var values=new SortedSet<float>(extra);
        for(float v=minimum;v<=maximum+.001f;v+=.5f) values.Add(v);
        return values.ToArray();
    }
    private sealed class Geometry
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        public readonly List<Vector2> uv=new List<Vector2>();
        public readonly List<int>[] indices=Enumerable.Range(0,5).Select(i=>new List<int>()).ToArray();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
        {
            int k=vertices.Count; vertices.AddRange(new[]{a,b,c,d});
            uv.AddRange(new[]{XZ(a)*.1f,XZ(b)*.1f,XZ(c)*.1f,XZ(d)*.1f});
            // 顶面选取更平缓的对角线，避免圆丘和平地过渡被离散成陡三角面。
            bool alternative=false;
            if(Vector3.Cross(b-a,c-a).y>0)
            {
                float ac=Mathf.Max(Vector3.Angle(Vector3.Cross(b-a,c-a),Vector3.up),Vector3.Angle(Vector3.Cross(c-a,d-a),Vector3.up));
                float bd=Mathf.Max(Vector3.Angle(Vector3.Cross(b-a,d-a),Vector3.up),Vector3.Angle(Vector3.Cross(c-b,d-b),Vector3.up));
                alternative=bd<ac;
            }
            indices[material].AddRange(alternative?new[]{k,k+1,k+3,k+1,k+2,k+3}:new[]{k,k+1,k+2,k,k+2,k+3});
        }
        public Mesh Mesh(string name)
        {
            Mesh mesh=new Mesh {name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=5;
            for(int i=0;i<5;i++) mesh.SetTriangles(indices[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
    private static int SurfaceMaterial(float x,float z,int zone,float height)
    {
        if(zone==1) return 4;
        if(zone==2 || zone==3) return 2;
        if(zone==0) return 0;
        RouteSample(new Vector2(x,z),out float distance,out float rh,out int segment);
        if(distance<(segment>=13?2.5f:2)) return 2;
        if(Vector2.Distance(new Vector2(x,z),new Vector2(-2,10))<3 || Vector2.Distance(new Vector2(x,z),new Vector2(29,30))<2.5f
            || Vector2.Distance(new Vector2(x,z),new Vector2(20,48))<2 || Vector2.Distance(new Vector2(x,z),new Vector2(-27,17))<2) return 3;
        return height>1.4f?1:0;
    }
    private static Material[] Materials()
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null) throw new InvalidOperationException("缺少 URP Lit Shader。");
        Color[] colors={new Color(.63f,.75f,.59f),new Color(.43f,.59f,.45f),new Color(.92f,.80f,.53f),new Color(.82f,.62f,.67f),new Color(.60f,.64f,.67f)};
        string[] names={"草地","丘肩","道路","景观预留","试跳台"};
        var result=new Material[5];
        for(int i=0;i<5;i++)
        {
            string path=AssetFolder+"/Materials/"+names[i]+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_BaseColor",colors[i]); material.SetFloat("_Smoothness",.05f);
            result[i]=material; EditorUtility.SetDirty(material);
        }
        return result;
    }
    private static void Build()
    {
        Scene scene=RequireScene();
        if(!File.Exists(Output+"/备份/game_内存场景.unity")) throw new InvalidOperationException("缺少开工前内存备份。");
        var objects=Objects(scene).ToArray();
        var pickups=objects.SelectMany(g=>g.GetComponents<StardustPickup>()).ToArray();
        var stars=Stars(scene);
        if(stars.Length!=23 || stars.Select(s=>s.memoryId).Distinct().Count()!=23) throw new InvalidOperationException("星尘必须为唯一编号的23个既有实例。");
        GameObject island=objects.Single(g=>g.name=="Island1");
        if(island.transform.position!=Vector3.zero || island.transform.localScale!=Vector3.one || island.transform.rotation!=Quaternion.identity)
            throw new InvalidOperationException("Island1 必须保持原点及单位变换。");
        Transform root=island.transform.Find("MorningFogGreybox");
        if(root==null) { var go=new GameObject("MorningFogGreybox"); Undo.RegisterCreatedObjectUndo(go,"搭建晨雾花谷灰盒"); go.transform.SetParent(island.transform,false); root=go.transform; }
        foreach(string group in new[]{"Base","Hub","Valley","Slopes","Jump03","Return","Rim","ArtReserve"})
            if(root.Find(group)==null) { var go=new GameObject(group); go.transform.SetParent(root,false); }
        Directory.CreateDirectory(AssetFolder+"/Meshes"); Directory.CreateDirectory(AssetFolder+"/Materials");
        AssetDatabase.Refresh();
        Material[] materials=Materials();
        float[] xs=Axis(-38,38,6,8,12,14,17,18,20), zs=Axis(-18,62,37,38.5f,41.4f,42.6f,44,45.5f,48.5f,50);
        var chunks=new Dictionary<string,Geometry>();
        for(int ix=0;ix<xs.Length-1;ix++) for(int iz=0;iz<zs.Length-1;iz++)
        {
            float x0=xs[ix],x1=xs[ix+1],z0=zs[iz],z1=zs[iz+1],cx=(x0+x1)/2,cz=(z0+z1)/2;
            if(!Supported(cx,cz)) continue;
            string key="地形_"+(ix/32)+"_"+(iz/32);
            if(!chunks.TryGetValue(key,out Geometry geo)) { geo=new Geometry(); chunks.Add(key,geo); }
            int zone=Zone(cx,cz);
            Vector3 a=new Vector3(x0,Height(zone,x0,z0),z0),b=new Vector3(x0,Height(zone,x0,z1),z1);
            Vector3 c=new Vector3(x1,Height(zone,x1,z1),z1),d=new Vector3(x1,Height(zone,x1,z0),z0);
            geo.Quad(a,b,c,d,SurfaceMaterial(cx,cz,zone,Height(zone,cx,cz)));
            geo.Quad(new Vector3(x0,-1.5f,z0),new Vector3(x1,-1.5f,z0),new Vector3(x1,-1.5f,z1),new Vector3(x0,-1.5f,z1),1);
            // 只有高度不连续或岛外缘才补竖面；相邻块不增加共面碰撞。
            Edge(geo,d,c,ix+1<xs.Length-1?(x1+xs[ix+2])/2:x1+.1f,cz,true);
            Edge(geo,c,b,cx,iz+1<zs.Length-1?(z1+zs[iz+2])/2:z1+.1f,true);
            if(!Supported(ix>0?(xs[ix-1]+x0)/2:x0-.1f,cz)) Edge(geo,b,a,x0-.1f,cz,false);
            if(!Supported(cx,iz>0?(zs[iz-1]+z0)/2:z0-.1f)) Edge(geo,a,d,cx,z0-.1f,false);
        }
        foreach(var item in chunks)
        {
            string path=AssetFolder+"/Meshes/"+item.Key+".asset";
            Mesh mesh=item.Value.Mesh(item.Key); var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) AssetDatabase.CreateAsset(mesh,path);
            else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); }
            Transform child=root.Find("Base/"+item.Key);
            if(child==null) { var go=new GameObject(item.Key); go.transform.SetParent(root.Find("Base"),false); child=go.transform; }
            MeshFilter filter=child.GetComponent<MeshFilter>(); if(filter==null) filter=child.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh=mesh;
            MeshRenderer renderer=child.GetComponent<MeshRenderer>(); if(renderer==null) renderer=child.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials=materials;
            MeshCollider collider=child.GetComponent<MeshCollider>(); if(collider==null) collider=child.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=null; collider.sharedMesh=mesh;
        }
        foreach(var pickup in pickups)
        {
            var memory=new SerializedObject(pickup).FindProperty("memory").objectReferenceValue as StardustMemoryData;
            if(memory.id>5) continue;
            Undo.RecordObject(pickup.transform,"移动第一岛既有星尘");
            pickup.transform.position=StarPositions[memory.id-1];
            PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.transform);
        }
        foreach(string name in new[]{"Player","SpawnPoint"}) { Transform t=objects.Single(g=>g.name==name).transform; Undo.RecordObject(t,"调整第一岛初始朝向"); t.rotation=Quaternion.Euler(0,-30,0); }
        GameObject plane=objects.Single(g=>g.name=="Plane");
        Undo.RecordObject(plane.GetComponent<MeshRenderer>(),"停用旧第一岛平面渲染"); plane.GetComponent<MeshRenderer>().enabled=false;
        Undo.RecordObject(plane.GetComponent<MeshCollider>(),"停用旧第一岛平面碰撞"); plane.GetComponent<MeshCollider>().enabled=false;
        foreach(Renderer r in objects.Single(g=>g.name=="Island1_Guide").GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r,"隐藏灰盒旧测试路标"); r.enabled=false; }
        AssetDatabase.SaveAssets(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        Validate();
        if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("game 场景保存失败。");
        RestoreParticleSerialization();
        Inspect(); Debug.Log("晨雾花谷灰盒已保存；静态与碰撞检查详见实施目录，真人键鼠验收另行记录。");
    }
    private static void Edge(Geometry geo,Vector3 a,Vector3 b,float nx,float nz,bool interior)
    {
        float ha=-1.5f,hb=-1.5f;
        if(interior && Supported(nx,nz)) { int zone=Zone(nx,nz); ha=Height(zone,a.x,a.z); hb=Height(zone,b.x,b.z); }
        if(Mathf.Abs(ha-a.y)<.0001f && Mathf.Abs(hb-b.y)<.0001f) return;
        Vector3 lowerA=new Vector3(a.x,ha,a.z), lowerB=new Vector3(b.x,hb,b.z);
        if(a.y+b.y>=ha+hb) geo.Quad(a,b,lowerB,lowerA,1);
        else geo.Quad(a,lowerA,lowerB,b,1);
    }

    [Serializable] public class Validation
    {
        public int starCount, meshCount, missingScripts;
        public string[] failures, changedOriginalComponents;
        public bool lowViewOccluded;
        public bool[] highViewsClear;
        public float maxTrailSlope, maxRidgeSlope, recoverySlope;
        public Vector3 steepestTrail, steepestRidge;
        public string realInputVerification="未验证：本报告为编辑器静态、Raycast与真实碰撞面检查。";
        public StarRecord[] stars;
    }
    private static float SurfaceAt(Vector3 p)
    {
        var hits=Physics.RaycastAll(new Vector3(p.x,30,p.z),Vector3.down,50,~0,QueryTriggerInteraction.Ignore);
        var ground=hits.Where(h=>h.collider.transform.GetComponentInParent<Transform>()!=null && IsGenerated(h.collider.transform)).OrderByDescending(h=>h.point.y).ToArray();
        return ground.Length>0?ground[0].point.y:float.NaN;
    }
    private static bool IsGenerated(Transform transform)
    { for(Transform t=transform;t!=null;t=t.parent) if(t.name=="MorningFogGreybox") return true; return false; }
    private static bool TerrainBlocks(Vector3 eye,Vector3 target)
    {
        Vector3 direction=target-eye;
        return Physics.RaycastAll(eye,direction.normalized,direction.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>IsGenerated(h.collider.transform));
    }
    private static void RestoreParticleSerialization()
    {
        Scene scene=RequireScene();
        if(scene.isDirty) throw new InvalidOperationException("必须先保存场景，才能保全已保存文件中的原粒子字段。");
        string original=File.ReadAllText(Output+"/备份/Assets/Scenes/game.unity");
        string current=File.ReadAllText(scene.path);
        var originalBlocks=SceneBlocks(original); var currentBlocks=SceneBlocks(current);
        var replacements=new Dictionary<string,string>(); var restored=new List<string>();
        foreach(string id in new[]{"51664883","1148040849","1515168229"})
        {
            if(!originalBlocks.TryGetValue(id,out string before)||!currentBlocks.TryGetValue(id,out string after))
                throw new InvalidOperationException("原灯塔粒子块缺失："+id);
            if(before==after) { restored.Add(id+"：原字段已保留。"); continue; }
            // Unity将单次Burst的间隔规范化到0.01；只允许这一种已确认的序列化差异。
            string normalized=before.Replace("repeatInterval: 0.0001","repeatInterval: 0.01");
            if(normalized==before||normalized!=after)
                throw new InvalidOperationException("粒子块存在其他差异，拒绝覆写："+id);
            replacements.Add(after,before); restored.Add(id+"：仅保全原repeatInterval=0.0001；cycleCount=1，运行时规范化不改变重复次数。");
        }
        foreach(var replacement in replacements) current=current.Replace(replacement.Key,replacement.Value);
        if(replacements.Count>0) File.WriteAllText(scene.path,current,new UTF8Encoding(false));
        File.WriteAllLines(Output+"/ParticleSerializationRestore.txt",restored);
    }
    private static Dictionary<string,string> SceneBlocks(string text)
    {
        MatchCollection headers=Regex.Matches(text,@"(?m)^--- !u!\d+ &(-?\d+)(?: stripped)?\r?$");
        var blocks=new Dictionary<string,string>();
        for(int i=0;i<headers.Count;i++)
        {
            int end=i+1<headers.Count?headers[i+1].Index:text.Length;
            blocks.Add(headers[i].Groups[1].Value,text.Substring(headers[i].Index,end-headers[i].Index));
        }
        return blocks;
    }
    private static void Validate()
    {
        Scene scene=RequireScene(); Physics.SyncTransforms();
        var objects=Objects(scene).ToArray(); var failures=new List<string>(); var stars=Stars(scene);
        if(stars.Length!=23) failures.Add("星尘数量不是23。");
        for(int i=0;i<5;i++)
        {
            var star=stars.Single(s=>s.memoryId==i+1); float floor=SurfaceAt(star.position);
            if(float.IsNaN(floor)||Mathf.Abs(star.position.y-floor-1.05f)>.02f) failures.Add("星尘"+(i+1)+"落脚面高度不符："+floor);
        }
        var changed=new List<string>();
        var current=objects.Where(g=>!IsGenerated(g.transform)).SelectMany(g=>g.GetComponents<Component>()).Where(c=>c!=null)
            .GroupBy(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()).ToDictionary(group=>group.Key,group=>group.First());
        foreach(string line in File.ReadAllLines(Output+"/备份/组件基线.tsv"))
        {
            string[] parts=line.Split('\t');
            if(!current.TryGetValue(parts[0],out Component component)) { failures.Add("原组件丢失："+parts[0]); continue; }
            if(Hash(EditorJsonUtility.ToJson(component))!=parts[2]) changed.Add(component.gameObject.name+"/"+parts[1]+"/"+parts[0]);
        }
        foreach(string change in changed)
        {
            string[] parts=change.Split('/');
            bool allowed=parts[0]=="Plane"&&(parts[1]=="MeshRenderer"||parts[1]=="MeshCollider")
                || parts[1]=="Transform"&&(parts[0]=="Player"||parts[0]=="SpawnPoint"||stars.Where(s=>s.memoryId<=5).Any(s=>s.name==parts[0]))
                || parts[0]=="Island1_Guide"&&parts[1]=="MeshRenderer";
            if(!allowed) failures.Add("超出允许范围的原组件改动："+change);
        }
        foreach(Pad pad in Pads)
            for(float dx=-pad.size.x/2+.1f;dx<=pad.size.x/2;dx+=1)
                for(float dz=-pad.size.y/2+.1f;dz<=pad.size.y/2;dz+=1)
                {
                    float height=SurfaceAt(pad.center+new Vector3(dx,0,dz));
                    if(float.IsNaN(height)||Mathf.Abs(height-pad.center.y)>.02f) failures.Add("平台全域平整检查失败："+pad.center);
                }
        Vector3 core=new Vector3(-3.943799f,5.24f,-1.5117153f),eye=new Vector3(-23,5.73f,16);
        bool[] high=new[]{-2f,0f,2f}.Select(offset=>!TerrainBlocks(eye+new Vector3(offset,0,0),core)).ToArray();
        bool low=TerrainBlocks(new Vector3(3,1.53f,-5),StarPositions[4]);
        if(!low) failures.Add("低眼位未遮挡05。"); if(high.Any(clear=>!clear)) failures.Add("05回望灯芯被灰盒遮挡。");
        float maxTrail=0,maxRidge=0; Vector3 steepTrail=Vector3.zero,steepRidge=Vector3.zero;
        foreach(MeshFilter filter in objects.Where(g=>IsGenerated(g.transform)).Select(g=>g.GetComponent<MeshFilter>()).Where(f=>f!=null))
        {
            Mesh mesh=filter.sharedMesh; Vector3[] vertices=mesh.vertices; int[] triangles=mesh.triangles;
            int[] trailTriangles=mesh.GetTriangles(2);
            for(int i=0;i<trailTriangles.Length;i+=3)
            {
                Vector3 a=vertices[trailTriangles[i]],b=vertices[trailTriangles[i+1]],c=vertices[trailTriangles[i+2]];
                Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                if(normal.y<=.01f) continue;
                float slope=Vector3.Angle(normal,Vector3.up);
                if(slope>maxTrail) { maxTrail=slope; steepTrail=(a+b+c)/3; }
            }
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                if(normal.y<=.01f) continue;
                float slope=Vector3.Angle(normal,Vector3.up); Vector3 p=(a+b+c)/3;
                RouteSample(XZ(p),out float distance,out float h,out int segment);
                float radius=Vector2.Distance(XZ(p),new Vector2(-14,10));
                if(radius>2.1f && radius<9.1f && distance>5.5f && slope>maxRidge) { maxRidge=slope; steepRidge=p; }
            }
        }
        if(maxTrail>20.1f) failures.Add("主路实际面坡超20度："+maxTrail);
        if(maxRidge>30.1f) failures.Add("中央丘实际面坡超30度："+maxRidge);
        float gapFloor=SurfaceAt(new Vector3(10,0,42));
        if(float.IsNaN(gapFloor)||Mathf.Abs(gapFloor)>.01f) failures.Add("浅沟被填平或缺少支撑。");
        for(float z=45.6f;z<=48.4f;z+=.7f)
        {
            float seam=SurfaceAt(new Vector3(14.01f,0,z));
            if(float.IsNaN(seam)||Mathf.Abs(seam-.7475f)>.02f) failures.Add("恢复坡边缘接缝错误。");
        }
        for(int i=0;i<18;i++)
        {
            if(i>=5&&i<=7) continue;
            Vector3 a=Route[i],b=Route[i+1];
            for(float t=0;t<=1;t+=.02f) { Vector3 p=Vector3.Lerp(a,b,t); if(float.IsNaN(SurfaceAt(p))) { failures.Add("主路无支撑：R"+i); break; } }
        }
        int missing=objects.Sum(g=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g));
        if(missing>0) failures.Add("缺失脚本："+missing);
        var report=new Validation {starCount=stars.Length,meshCount=objects.Count(g=>IsGenerated(g.transform)&&g.GetComponent<MeshCollider>()!=null),missingScripts=missing,
            failures=failures.ToArray(),changedOriginalComponents=changed.ToArray(),lowViewOccluded=low,highViewsClear=high,
            maxTrailSlope=maxTrail,maxRidgeSlope=maxRidge,steepestTrail=steepTrail,steepestRidge=steepRidge,
            recoverySlope=Mathf.Atan2(.75f,3)*Mathf.Rad2Deg,stars=stars};
        File.WriteAllText(Output+"/Validation.json",JsonUtility.ToJson(report,true));
        if(failures.Count>0) throw new InvalidOperationException(string.Join("\n",failures));
    }

    private static void RenderViews()
    {
        RequireScene(); Directory.CreateDirectory(Output+"/视图");
        Render("俯视",new Vector3(0,100,22),new Vector3(0,0,22),1600,1600,true,43);
        Render("出生_16比9",new Vector3(3,1.53f,-5),new Vector3(3,1.53f,-5)+Quaternion.Euler(0,-30,0)*Vector3.forward,1920,1080,false,0);
        Render("出生_16比10",new Vector3(3,1.53f,-5),new Vector3(3,1.53f,-5)+Quaternion.Euler(0,-30,0)*Vector3.forward,1920,1200,false,0);
        Render("浅沟",new Vector3(10,2.28f,39),new Vector3(10,1,46),1600,900,false,0);
        Render("05回望",new Vector3(-23,5.73f,16),new Vector3(-3.943799f,5.24f,-1.5117153f),1600,900,false,0);
        Render("岛屿全景",new Vector3(64,66,-58),new Vector3(0,1,22),1600,1000,false,0);
    }
    [Serializable] public class ConsoleReport {public int errors,warnings,logs;public string note="当前Console计数；已修复的工具异常详见Editor.log，新增运行期报错单独见RuntimeMonitor。";}
    private static void ConsoleCounts()
    {
        Type type=typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
        var method=type.GetMethod("GetCountsByType",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        object[] values={0,0,0}; method.Invoke(null,values);
        File.WriteAllText(Output+"/ConsoleCounts.json",JsonUtility.ToJson(new ConsoleReport {errors=(int)values[0],warnings=(int)values[1],logs=(int)values[2]},true));
    }

    [MenuItem("Tools/晨雾花谷/核查当前场景")]
    private static void InspectMenu() { Inspect(); }
    [MenuItem("Tools/晨雾花谷/搭建并保存第一岛灰盒")]
    private static void BuildMenu() { Build(); }
    [MenuItem("Tools/晨雾花谷/检查碰撞面与绑定")]
    private static void ValidateMenu() { Validate(); }
    [MenuItem("Tools/晨雾花谷/导出场景视图")]
    private static void RenderMenu() { RenderViews(); }

    [Serializable] public class CollisionResult
    {
        public string method="编辑模式 CharacterController.Move 受控碰撞核查，不是玩家真实WASD/鼠标/空格验收。";
        public string[] passed, failed;
    }
    private static void CollisionChecks()
    {
        Scene scene=RequireScene(); var failures=new List<string>(); var passed=new List<string>();
        CharacterController original=Objects(scene).Single(g=>g.name=="Player").GetComponent<CharacterController>();
        GameObject go=new GameObject("灰盒临时碰撞探针") {hideFlags=HideFlags.HideAndDontSave};
        CharacterController probe=go.AddComponent<CharacterController>();
        probe.height=original.height; probe.radius=original.radius; probe.center=original.center;
        probe.stepOffset=original.stepOffset; probe.slopeLimit=original.slopeLimit; probe.skinWidth=original.skinWidth; probe.minMoveDistance=original.minMoveDistance;
        try
        {
            var walk=Route.Take(6).Concat(new[]{new Vector3(18,0,47),new Vector3(17,0,47),new Vector3(13,.75f,47)}).Concat(Route.Skip(8)).ToArray();
            ResetProbe(probe,walk[0]);
            string result=WalkProbe(probe,walk.Skip(1).ToArray());
            if(result==null) passed.Add("完整推荐步行闭环（03由恢复坡绕行）"); else failures.Add("步行闭环："+result);
            foreach(float offset in new[]{-1f,0f,1f})
            {
                ResetProbe(probe,new Vector3(18,0,47+offset));
                result=WalkProbe(probe,new[]{new Vector3(13,.75f,47+offset),new Vector3(10,.75f,47)});
                if(result==null) passed.Add("恢复坡横向偏移"+offset+"米"); else failures.Add("恢复坡："+result);
            }
            for(int i=0;i<5;i++)
            {
                ResetProbe(probe,new Vector3(10,.75f,40.7f));
                float velocity=8;
                for(int frame=0;frame<65;frame++) { velocity-=20*.02f; probe.Move(new Vector3(0,velocity,5)*.02f); }
                if(probe.transform.position.z>43 && probe.transform.position.y>.65f && probe.isGrounded) passed.Add("试跳"+(i+1));
                else failures.Add("试跳失败："+probe.transform.position);
            }
            for(int i=0;i<3;i++)
            {
                ResetProbe(probe,new Vector3(10,.75f,41));
                for(int frame=0;frame<90;frame++) probe.Move(new Vector3(0,-2,1.2f)*.02f);
                Vector3 fallen=probe.transform.position;
                if(fallen.y>.25f || fallen.y<-.2f) { failures.Add("慢走落沟高度："+fallen); continue; }
                result=WalkProbe(probe,new[]{new Vector3(17.5f,0,43),new Vector3(18,0,47),new Vector3(13,.75f,47),new Vector3(10,.75f,47)});
                if(result==null) passed.Add("慢走落沟恢复"+(i+1)); else failures.Add("落沟恢复："+result);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        File.WriteAllText(Output+"/CollisionChecks.json",JsonUtility.ToJson(new CollisionResult {passed=passed.ToArray(),failed=failures.ToArray()},true));
        if(failures.Count>0) throw new InvalidOperationException(string.Join("\n",failures));
    }
    private static void ResetProbe(CharacterController probe,Vector3 surface)
    {
        probe.enabled=false; probe.transform.position=surface+Vector3.up*.03f; probe.enabled=true; Physics.SyncTransforms();
        for(int i=0;i<10;i++) probe.Move(Vector3.down*.04f);
    }
    private static string WalkProbe(CharacterController probe,Vector3[] targets)
    {
        float velocity=-2;
        foreach(Vector3 target in targets)
        {
            bool reached=false;
            for(int frame=0;frame<2000;frame++)
            {
                Vector3 delta=target-probe.transform.position; delta.y=0;
                if(delta.magnitude<.12f)
                {
                    float ground=SurfaceAt(target);
                    if(float.IsNaN(ground)||Mathf.Abs(probe.transform.position.y-ground)>.15f) return "目标点高度错误："+target+"，实际"+probe.transform.position;
                    reached=true; break;
                }
                velocity=probe.isGrounded?-2:velocity-20*.02f;
                probe.Move(delta.normalized*Mathf.Min(5*.02f,delta.magnitude)+Vector3.up*velocity*.02f);
                if(probe.transform.position.y < -3) return "跌出支撑："+target;
            }
            if(!reached) return "卡住：目标"+target+"，实际"+probe.transform.position;
        }
        return null;
    }

    [Serializable] public class RuntimeResult
    {
        public string method="后台播放模式稳定性观察；没有使用真人键鼠走通路线、操作菜单或传送。";
        public float elapsedSeconds;
        public float scaledTime,unscaledTime;
        public int frames;
        public Vector3 playerPosition;
        public int collected, firstIslandCount, newErrors, newWarnings;
        public bool firstTowerLit, playing;
        public string[] messages;
    }
    private static void StartRuntimeCheck()
    {
        Scene scene=RequireScene();
        if(scene.isDirty) throw new InvalidOperationException("播放核查前必须保存场景。");
        SessionState.SetBool("MorningFogObserve",true); SessionState.SetFloat("MorningFogStart",(float)EditorApplication.timeSinceStartup);
        SessionState.SetInt("MorningFogErrors",0); SessionState.SetInt("MorningFogWarnings",0);
        File.WriteAllText(Output+"/RuntimeMessages.txt","");
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    private static void RegisterRuntimeObserver()
    {
        Application.logMessageReceived+=ObserveLog;
        EditorApplication.update+=ObserveRuntime;
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("MorningFogObserve",false))
            { SessionState.SetFloat("MorningFogStart",(float)EditorApplication.timeSinceStartup); SessionState.SetInt("MorningFogStartFrame",Time.frameCount); Application.runInBackground=true; }
        };
    }
    private static void ObserveLog(string message,string stack,LogType type)
    {
        if(!SessionState.GetBool("MorningFogObserve",false)||!EditorApplication.isPlaying) return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) SessionState.SetInt("MorningFogErrors",SessionState.GetInt("MorningFogErrors",0)+1);
        if(type==LogType.Warning) SessionState.SetInt("MorningFogWarnings",SessionState.GetInt("MorningFogWarnings",0)+1);
        if(type!=LogType.Log) File.AppendAllText(Output+"/RuntimeMessages.txt",type+": "+message+"\n");
    }
    private static double nextObserve;
    private static void ObserveRuntime()
    {
        if(!SessionState.GetBool("MorningFogObserve",false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<nextObserve) return;
        nextObserve=EditorApplication.timeSinceStartup+1;
        GameObject player=GameObject.Find("Player"); if(player==null) return;
        StardustManager progress=StardustManager.Instance;
        LighthouseController tower=UnityEngine.Object.FindObjectsOfType<LighthouseController>().FirstOrDefault(t=>t.island==1);
        var result=new RuntimeResult {playing=true,elapsedSeconds=(float)EditorApplication.timeSinceStartup-SessionState.GetFloat("MorningFogStart",0),
            frames=Time.frameCount-SessionState.GetInt("MorningFogStartFrame",0),scaledTime=Time.time,unscaledTime=Time.unscaledTime,
            playerPosition=player.transform.position,collected=progress!=null?progress.Count:-1,firstIslandCount=progress!=null?progress.GetIslandCount(1):-1,
            firstTowerLit=tower!=null&&tower.IsLit,newErrors=SessionState.GetInt("MorningFogErrors",0),newWarnings=SessionState.GetInt("MorningFogWarnings",0),
            messages=File.ReadAllLines(Output+"/RuntimeMessages.txt")};
        bool finished=result.elapsedSeconds>=185;
        result.playing=!finished;
        File.WriteAllText(Output+"/RuntimeMonitor.json",JsonUtility.ToJson(result,true));
        if(finished) { SessionState.SetBool("MorningFogObserve",false); EditorApplication.isPlaying=false; }
    }
    private static void Render(string name,Vector3 position,Vector3 target,int width,int height,bool ortho,float size)
    {
        var go=new GameObject("灰盒临时核查摄像机") {hideFlags=HideFlags.HideAndDontSave};
        Camera camera=go.AddComponent<Camera>(); camera.transform.position=position; camera.transform.LookAt(target);
        camera.fieldOfView=60; camera.orthographic=ortho; camera.orthographicSize=size; camera.nearClipPlane=.05f; camera.farClipPlane=200;
        RenderTexture rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        camera.targetTexture=rt; RenderTexture previous=RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active=rt;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false); texture.ReadPixels(new UnityEngine.Rect(0,0,width,height),0,0); texture.Apply();
            File.WriteAllBytes(Output+"/视图/"+name+".png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { RenderTexture.active=previous; camera.targetTexture=null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(go); }
    }
}
