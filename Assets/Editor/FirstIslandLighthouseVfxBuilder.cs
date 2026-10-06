using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class FirstIslandLighthouseVfxBuilder
{
    public const string Output="Temp/EditorTools/灯塔粒子强化_20261003";
    private const string AssetsPath="Assets/FirstIslandLighthouseVfx";
    private const string CommandFile="Library/FirstIslandLighthouseVfx.command";
    private static double nextPoll,nextObserve;
    private static readonly Vector3 CorePosition=new Vector3(-3.943799f,5.24f,-1.5117153f);
    static FirstIslandLighthouseVfxBuilder()
    {
        EditorApplication.update+=Poll;
        EditorApplication.update+=Observe;
        Application.logMessageReceived+=OnLog;
    }
    private static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextPoll) return;
        nextPoll=EditorApplication.timeSinceStartup+.5;
        if(!File.Exists(CommandFile)) return;
        string command=File.ReadAllText(CommandFile).Trim(); if(command.Length==0) return;
        File.WriteAllText(CommandFile,""); Directory.CreateDirectory(Output);
        try
        {
            if(command=="backup") Backup();
            else if(command=="build") Build();
            else if(command=="validate") Validate();
            else if(command=="diagnose")
            {
                Scene scene=RequireScene();
                EditorSceneManager.SaveScene(scene,Output+"/诊断_内存场景.unity",true);
                File.WriteAllText(Output+"/诊断_常驻粒子.json",EditorJsonUtility.ToJson(FirstTower(scene).steadyParticles,true));
            }
            else if(command=="save")
            {
                Scene scene=RequireScene();
                Material core=AssetDatabase.LoadAssetAtPath<Material>(AssetsPath+"/第一岛_灯芯.mat");
                if(FirstTower(scene).core.sharedMaterial!=core) throw new InvalidOperationException("当前第一塔没有绑定本轮专用灯芯材质。");
                ConfigureEmission(core); AssetDatabase.SaveAssets(); Validate();
                if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存失败。");
                File.WriteAllText(Output+"/配置后.json",JsonUtility.ToJson(Describe(scene),true));
                File.WriteAllText("Library/MorningFogGreybox.command","restore-particles");
            }
            else if(command=="test") StartChecks();
            else if(command=="stop") { SessionState.SetBool("FirstVfxObserve",false); EditorApplication.isPlaying=false; }
            else throw new InvalidOperationException("未知灯塔粒子命令："+command);
            File.WriteAllText(Output+"/last-result.txt",command+" OK "+DateTime.UtcNow.ToString("O"));
        }
        catch(Exception error) { File.WriteAllText(Output+"/last-result.txt",command+" FAILED "+error); Debug.LogException(error); }
    }
    private static Scene RequireScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("必须退出播放状态。");
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/game.unity"||SceneManager.sceneCount!=1) throw new InvalidOperationException("仅允许操作真实game场景。");
        return scene;
    }
    private static IEnumerable<GameObject> Objects(Scene scene)
    { return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject); }
    private static LighthouseController FirstTower(Scene scene)
    { return Objects(scene).SelectMany(g=>g.GetComponents<LighthouseController>()).Single(t=>t.island==1); }
    private static string Hash(string value)
    { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",""); }
    [Serializable] public class SourceRecord { public string path,hash; }
    [Serializable] public class SourceList { public SourceRecord[] files; }
    private static void Backup()
    {
        Scene scene=RequireScene(); if(scene.isDirty) throw new InvalidOperationException("当前场景有未保存修改，先保护用户工作，不能直接搭建。");
        string folder=Output+"/备份"; Directory.CreateDirectory(folder);
        if(File.Exists(folder+"/game.unity")) throw new InvalidOperationException("开工备份已存在，不覆盖原件。");
        File.Copy(scene.path,folder+"/game.unity");
        if(!EditorSceneManager.SaveScene(scene,folder+"/game_内存场景.unity",true)) throw new InvalidOperationException("内存场景备份失败。");
        File.WriteAllLines(folder+"/组件基线.tsv",Objects(scene).SelectMany(g=>g.GetComponents<Component>()).Where(c=>c!=null)
            .Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c)+"\t"+c.GetType().Name+"\t"+Hash(EditorJsonUtility.ToJson(c))));
        string[] protectedFiles=Directory.GetFiles("Assets/Scripts","*.cs",SearchOption.AllDirectories)
            .Concat(Directory.Exists("Assets/MorningFogGreybox") ? Directory.GetFiles("Assets/MorningFogGreybox","*",SearchOption.AllDirectories) : Array.Empty<string>())
            .Concat(Directory.GetFiles("Assets/Materials/IslandPrototype","*",SearchOption.AllDirectories))
            .Concat(Directory.GetFiles("Assets/Shaders","*",SearchOption.AllDirectories))
            .Concat(new[]{"Assets/Scenes/start.unity"}).ToArray();
        File.WriteAllText(folder+"/源文件基线.json",JsonUtility.ToJson(new SourceList {files=protectedFiles.Select(path=>
            new SourceRecord {path=path.Replace('\\','/'),hash=Hash(File.ReadAllText(path))}).ToArray()},true));
        File.WriteAllText(Output+"/配置前.json",JsonUtility.ToJson(Describe(scene),true));
    }
    [Serializable] public class ParticleRecord
    {
        public string id,name,material; public Vector3 position;
        public float size,rate,lifetime; public int capacity; public bool loop,useUnscaledTime,playOnAwake;
    }
    [Serializable] public class Description {public ParticleRecord[] particles; public float ringWidth; public string coreMaterial;}
    private static Description Describe(Scene scene)
    {
        LighthouseController tower=FirstTower(scene);
        return new Description {ringWidth=tower.ring.widthMultiplier,coreMaterial=AssetDatabase.GetAssetPath(tower.core.sharedMaterial),
            particles=tower.GetComponentsInChildren<ParticleSystem>(true).Select(p=>new ParticleRecord {
                id=GlobalObjectId.GetGlobalObjectIdSlow(p).ToString(),name=p.name,position=p.transform.position,
                material=AssetDatabase.GetAssetPath(p.GetComponent<ParticleSystemRenderer>().sharedMaterial),
                size=p.main.startSize.constantMax,rate=p.emission.rateOverTime.constantMax,lifetime=p.main.startLifetime.constantMax,
                capacity=p.main.maxParticles,loop=p.main.loop,useUnscaledTime=p.main.useUnscaledTime,playOnAwake=p.main.playOnAwake}).ToArray()};
    }
    private static Material PrivateMaterial(string name,Material source,Color color,bool emission=false)
    {
        string path=AssetsPath+"/"+name+".mat"; Material result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(result==null) { result=new Material(source) {name=name}; AssetDatabase.CreateAsset(result,path); }
        else EditorUtility.CopySerialized(source,result);
        result.name=name; result.SetColor("_BaseColor",color);
        if(emission) ConfigureEmission(result);
        EditorUtility.SetDirty(result); return result;
    }
    private static void ConfigureEmission(Material material)
    {
        // 资产保留极低非零值以免URP导入关闭关键词；原Start会把未点亮实例的发光色归零。
        material.SetColor("_EmissionColor",new Color(.02f,.019f,.016f));
        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
        material.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(material);
    }
    private static ParticleSystem Column(Transform parent,string name,Material material,bool ignition)
    {
        Transform child=parent.Find(name);
        if(child==null) { var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"新增第一岛灯塔粒子柱"); go.transform.SetParent(parent,false); child=go.transform; }
        child.position=CorePosition; child.rotation=Quaternion.Euler(-90,0,0); child.localScale=Vector3.one;
        ParticleSystem p=child.GetComponent<ParticleSystem>(); if(p==null) p=child.gameObject.AddComponent<ParticleSystem>();
        p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=p.main; main.duration=2; main.loop=!ignition; main.playOnAwake=false; main.prewarm=false;
        main.useUnscaledTime=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate; main.maxParticles=ignition?250:500;
        main.startDelay=0; main.startLifetime=ignition?new ParticleSystem.MinMaxCurve(1.25f,1.75f):new ParticleSystem.MinMaxCurve(2f,2.4f);
        main.startSpeed=ignition?new ParticleSystem.MinMaxCurve(7.5f,10f):new ParticleSystem.MinMaxCurve(6.5f,7.2f);
        main.startSize=ignition?new ParticleSystem.MinMaxCurve(.3f,.5f):new ParticleSystem.MinMaxCurve(.24f,.4f);
        main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2); main.gravityModifier=0;
        main.startColor=Color.white;
        var emissionModule=p.emission; emissionModule.enabled=true; emissionModule.rateOverTime=ignition?55:140;
        emissionModule.SetBursts(ignition?new[]{new ParticleSystem.Burst(0,36)}:new ParticleSystem.Burst[0]);
        var shape=p.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Cone;
        shape.angle=1.2f; shape.radius=ignition?.7f:.8f; shape.length=0; shape.radiusThickness=1;
        var colors=p.colorOverLifetime; colors.enabled=true;
        var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.96f,.82f),0),
            new GradientColorKey(new Color(1,.88f,.56f),1)},new[]{new GradientAlphaKey(0,0),
            new GradientAlphaKey(.9f,.08f),new GradientAlphaKey(.8f,.7f),new GradientAlphaKey(0,1)});
        colors.color=gradient;
        var size=p.sizeOverLifetime; size.enabled=true;
        size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.55f),new Keyframe(.12f,1),new Keyframe(.8f,.85f),new Keyframe(1,.3f)));
        var noise=p.noise; noise.enabled=false; var velocity=p.velocityOverLifetime; velocity.enabled=false;
        var trails=p.trails; trails.enabled=false;
        ParticleSystemRenderer renderer=p.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Billboard; renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; renderer.maxParticleSize=.5f;
        renderer.minParticleSize=0; renderer.sortMode=ParticleSystemSortMode.Distance;
        p.useAutoRandomSeed=false; p.randomSeed=ignition?2163u:1568u; EditorUtility.SetDirty(p); return p;
    }
    private static void Build()
    {
        Scene scene=RequireScene(); if(scene.isDirty) throw new InvalidOperationException("不能覆盖当前未保存改动。");
        if(!File.Exists(Output+"/备份/game.unity")) throw new InvalidOperationException("缺少本轮开工备份。");
        Directory.CreateDirectory(AssetsPath); AssetDatabase.Refresh(); LighthouseController tower=FirstTower(scene);
        Material soft=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IslandPrototype/SoftGold.mat");
        Material core=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IslandPrototype/LampCore.mat");
        Material column=PrivateMaterial("第一岛_金白粒子柱",soft,new Color(3.2f,2.95f,2.25f,1));
        Material burst=PrivateMaterial("第一岛_点亮上升光粒",soft,new Color(3.8f,3.5f,2.7f,1));
        Material ringMaterial=PrivateMaterial("第一岛_扩散环",soft,new Color(2.5f,2.25f,1.65f,1));
        ringMaterial.SetFloat("_Radial",0);
        Material coreMaterial=PrivateMaterial("第一岛_灯芯",core,new Color(.13f,.11f,.08f),true);
        Undo.RecordObject(tower.core,"启用第一岛灯芯专用自发光材质"); tower.core.sharedMaterial=coreMaterial;
        Undo.RecordObject(tower.ring,"强化第一岛点亮扩散环"); tower.ring.sharedMaterial=ringMaterial; tower.ring.widthMultiplier=.3f;
        Column(tower.steadyParticles.transform,"FirstIslandGoldColumn",column,false);
        Column(tower.ignitionParticles.transform,"FirstIslandIgnitionLift",burst,true);
        // Unity对子树同步playOnAwake；第一塔改为显式由原Start/OnProgress启动，避免未集齐闪现。
        var steadyMain=tower.steadyParticles.main; steadyMain.playOnAwake=false;
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); Validate();
        if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("灯塔粒子保存失败。");
        File.WriteAllText(Output+"/配置后.json",JsonUtility.ToJson(Describe(scene),true));
        // 旧工具严格保全三个原单次Burst的序列化字段，不能夹带修改后两塔。
        File.WriteAllText("Library/MorningFogGreybox.command","restore-particles");
    }
    [Serializable] public class ValidationReport {public string[] failures,changed;public int towers,columnCount;public int testedStandingPoints,occludedStandingPoints;public float targetHeight;}
    private static void Validate()
    {
        Scene scene=RequireScene(); var failures=new List<string>(); var changed=new List<string>();
        LighthouseController tower=FirstTower(scene); var all=Objects(scene).ToArray();
        var current=all.SelectMany(g=>g.GetComponents<Component>()).Where(c=>c!=null).GroupBy(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()).ToDictionary(g=>g.Key,g=>g.First());
        var allowed=new HashSet<string>(new Component[]{tower.core,tower.ring,tower.steadyParticles.transform,tower.ignitionParticles.transform}.Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()));
        foreach(string line in File.ReadAllLines(Output+"/备份/组件基线.tsv"))
        {
            string[] parts=line.Split('\t'); if(!current.TryGetValue(parts[0],out Component c)) { failures.Add("原组件丢失："+parts[0]); continue; }
            string serialized=EditorJsonUtility.ToJson(c);
            if(Hash(serialized)!=parts[2])
            {
                changed.Add(c.name+"/"+parts[1]+"/"+parts[0]);
                bool onlyAwake=c==tower.steadyParticles&&Hash(serialized.Replace("\"playOnAwake\":false","\"playOnAwake\":true"))==parts[2];
                if(!allowed.Contains(parts[0])&&!onlyAwake) failures.Add("越界组件变化："+parts[0]);
            }
        }
        SourceList sources=JsonUtility.FromJson<SourceList>(File.ReadAllText(Output+"/备份/源文件基线.json"));
        foreach(SourceRecord source in sources.files)
            if(!File.Exists(source.path)||Hash(File.ReadAllText(source.path))!=source.hash) failures.Add("受保护源文件改变："+source.path);
        Transform column=tower.steadyParticles.transform.Find("FirstIslandGoldColumn");
        Transform ignition=tower.ignitionParticles.transform.Find("FirstIslandIgnitionLift");
        if(column==null||ignition==null) failures.Add("两个第一岛粒子子系统未创建完整。");
        else foreach(ParticleSystem p in new[]{column.GetComponent<ParticleSystem>(),ignition.GetComponent<ParticleSystem>()})
            if(p.main.playOnAwake||p.main.useUnscaledTime||p.main.cullingMode!=ParticleSystemCullingMode.AlwaysSimulate) failures.Add("启动、暂停或离屏参数不符："+p.name);
        if(!tower.core.sharedMaterial.IsKeywordEnabled("_EMISSION")) failures.Add("第一岛灯芯自发光关键词未启用。");
        if(all.SelectMany(g=>g.GetComponents<StardustPickup>()).Count()!=23) failures.Add("星尘数量改变。");
        Physics.SyncTransforms(); int standing=0,occluded=0;
        for(float x=-37;x<=37;x+=2) for(float z=-17;z<=61;z+=2)
        {
            var floors=Physics.RaycastAll(new Vector3(x,30,z),Vector3.down,40,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="MorningFogGreybox")).OrderByDescending(h=>h.point.y).ToArray();
            if(floors.Length==0) continue;
            Vector3 eye=floors[0].point+Vector3.up*1.53f;
            Vector3 target=new Vector3(CorePosition.x,14,CorePosition.z),direction=target-eye; standing++;
            bool blocked=Physics.RaycastAll(eye,direction.normalized,direction.magnitude,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>h.collider.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="MorningFogGreybox"));
            if(blocked) occluded++;
        }
        if(occluded>0) failures.Add("地形遮挡世界Y14柱体的站位数："+occluded);
        var result=new ValidationReport {failures=failures.ToArray(),changed=changed.ToArray(),towers=all.SelectMany(g=>g.GetComponents<LighthouseController>()).Count(),
            columnCount=all.Count(g=>g.name=="FirstIslandGoldColumn"),testedStandingPoints=standing,occludedStandingPoints=occluded,targetHeight=14};
        File.WriteAllText(Output+"/Validation.json",JsonUtility.ToJson(result,true));
        if(failures.Count>0) throw new InvalidOperationException(string.Join("\n",failures));
    }
    private static void StartChecks()
    {
        Scene scene=RequireScene(); if(scene.isDirty) throw new InvalidOperationException("必须先保存再开始后台播放核查。");
        SessionState.SetBool("FirstVfxObserve",true); SessionState.SetInt("FirstVfxStage",0);
        SessionState.SetBool("FirstVfxHalfRendered",false); SessionState.SetBool("FirstVfxTwoRendered",false);
        SessionState.SetInt("FirstVfxErrors",0); SessionState.SetInt("FirstVfxWarnings",0);
        File.WriteAllText(Output+"/RuntimeChecks.txt",""); File.WriteAllText(Output+"/RuntimeMessages.txt","");
        EditorApplication.isPlaying=true;
    }
    private static void Check(bool success,string name)
    {
        File.AppendAllText(Output+"/RuntimeChecks.txt",(success?"PASS ":"FAIL ")+name+"\n");
        if(!success) throw new InvalidOperationException("运行检查失败："+name);
    }
    private static void OnLog(string message,string stack,LogType type)
    {
        if(!SessionState.GetBool("FirstVfxObserve",false)||!EditorApplication.isPlaying) return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) SessionState.SetInt("FirstVfxErrors",SessionState.GetInt("FirstVfxErrors",0)+1);
        if(type==LogType.Warning) SessionState.SetInt("FirstVfxWarnings",SessionState.GetInt("FirstVfxWarnings",0)+1);
        if(type!=LogType.Log) File.AppendAllText(Output+"/RuntimeMessages.txt",type+": "+message+"\n");
    }
    private static string ParticleSignature(ParticleSystem p)
    {
        var particles=new ParticleSystem.Particle[p.main.maxParticles]; int count=p.GetParticles(particles); var b=new StringBuilder();
        for(int i=0;i<count;i++) b.Append(particles[i].position.ToString("F6")).Append('/').Append(particles[i].remainingLifetime.ToString("F6"));
        return Hash(b.ToString());
    }
    [Serializable] public class RuntimeReport
    {
        public string method="后台Play自动接口验证与相机渲染，不是人工键鼠拾取、F传送或Esc操作。";
        public float elapsedSeconds,gameSeconds,columnTime; public int frames,count,ignitionCount,particles,errors,warnings;
        public bool playing,lit; public string[] checks;
    }
    private static void Observe()
    {
        if(!SessionState.GetBool("FirstVfxObserve",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextObserve) return;
        nextObserve=EditorApplication.timeSinceStartup+.05;
        try
        {
            LighthouseController tower=UnityEngine.Object.FindObjectsOfType<LighthouseController>().SingleOrDefault(t=>t.island==1);
            StardustManager manager=StardustManager.Instance; if(tower==null||manager==null||Time.frameCount<5) return;
            ParticleSystem column=tower.steadyParticles.transform.Find("FirstIslandGoldColumn").GetComponent<ParticleSystem>();
            ParticleSystem lift=tower.ignitionParticles.transform.Find("FirstIslandIgnitionLift").GetComponent<ParticleSystem>();
            int stage=SessionState.GetInt("FirstVfxStage",0); Application.runInBackground=true;
            if(stage==0)
            {
                SessionState.SetFloat("FirstVfxWall",(float)EditorApplication.timeSinceStartup); SessionState.SetInt("FirstVfxFrame",Time.frameCount);
                Check(manager.Count==0&&!tower.IsLit&&!column.isPlaying&&column.particleCount==0&&!lift.isPlaying&&lift.particleCount==0,"未集齐时常驻柱和点亮柱关闭");
                RenderViews("未点亮");
                for(int id=1;id<=4;id++) Check(manager.TryCollect(manager.memories.Single(m=>m.id==id)),"自动接口收集"+id);
                Check(manager.GetIslandCount(1)==4&&!tower.IsLit&&!column.isPlaying,"4/5不点亮且无光柱");
                SessionState.SetInt("FirstVfxStage",1); SessionState.SetFloat("FirstVfxGame",Time.time);
            }
            else if(stage==1&&Time.time-SessionState.GetFloat("FirstVfxGame",0)>.3f)
            {
                Check(manager.TryCollect(manager.memories.Single(m=>m.id==5)),"自动接口收集第五颗");
                Check(tower.IsLit&&tower.IgnitionCount==1&&column.isPlaying&&lift.isPlaying,"5/5首次点亮并启动两个子粒子系统");
                manager.dialogue?.CloseCurrent(); SessionState.SetFloat("FirstVfxLitTime",Time.time); SessionState.SetInt("FirstVfxStage",2);
            }
            else if(stage==2)
            {
                float elapsed=Time.time-SessionState.GetFloat("FirstVfxLitTime",0);
                if(elapsed>.5f&&!SessionState.GetBool("FirstVfxHalfRendered",false))
                { RenderViews("点亮0.5秒"); SessionState.SetBool("FirstVfxHalfRendered",true); }
                if(elapsed>2.1f&&!SessionState.GetBool("FirstVfxTwoRendered",false))
                { Check(tower.ring.enabled==false,"约2秒点亮过程结束且扩散环关闭"); RenderViews("点亮2秒"); SessionState.SetBool("FirstVfxTwoRendered",true); }
                if(elapsed>5)
                {
                    Check(column.particleCount>200&&column.particleCount<=500,"常驻粒子柱密度及容量");
                    Check(!lift.IsAlive(true),"一次性点亮上升光粒已结束"); RenderViews("常驻5秒");
                    var owner=new GameObject("灯塔后台暂停核查") {hideFlags=HideFlags.HideAndDontSave};
                    GameplayPause.Set(owner,true); SessionState.SetFloat("FirstVfxPauseWall",(float)EditorApplication.timeSinceStartup);
                    SessionState.SetFloat("FirstVfxPauseTime",column.time); SessionState.SetString("FirstVfxPauseParticles",ParticleSignature(column));
                    SessionState.SetInt("FirstVfxStage",3);
                }
            }
            else if(stage==3&&EditorApplication.timeSinceStartup-SessionState.GetFloat("FirstVfxPauseWall",0)>2)
            {
                Check(GameplayPause.IsPaused&&Time.timeScale==0&&Mathf.Abs(column.time-SessionState.GetFloat("FirstVfxPauseTime",0))<.0001f
                    &&ParticleSignature(column)==SessionState.GetString("FirstVfxPauseParticles",""),"暂停2秒时粒子时间、位置和寿命冻结");
                GameObject owner=GameObject.Find("灯塔后台暂停核查"); GameplayPause.Set(owner,false); UnityEngine.Object.Destroy(owner);
                SessionState.SetFloat("FirstVfxResumeTime",Time.time); SessionState.SetInt("FirstVfxStage",4);
            }
            else if(stage==4&&Time.time-SessionState.GetFloat("FirstVfxResumeTime",0)>1)
            {
                Check(!GameplayPause.IsPaused&&Time.timeScale>0&&column.isPlaying&&column.particleCount>200
                    &&ParticleSignature(column)!=SessionState.GetString("FirstVfxPauseParticles",""),"暂停恢复后粒子继续常驻并重新运动");
                Check(!manager.TryCollect(manager.memories.Single(m=>m.id==5)),"重复收集被原管理器拒绝");
                typeof(LighthouseController).GetMethod("OnProgress",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(tower,new object[]{5});
                Check(tower.IgnitionCount==1&&!lift.IsAlive(true),"重复进度通知不重复点亮爆发");
                Check(UnityEngine.Object.FindObjectsOfType<LighthouseController>().Where(t=>t.island>1).All(t=>!t.IsLit&&t.IgnitionCount==0
                    &&t.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.name!="FirstIslandGoldColumn"&&p.name!="FirstIslandIgnitionLift")),"后两塔保持未点亮且无新光柱");
                SessionState.SetInt("FirstVfxStage",5);
            }
            float duration=(float)EditorApplication.timeSinceStartup-SessionState.GetFloat("FirstVfxWall",(float)EditorApplication.timeSinceStartup);
            bool finished=stage==5&&duration>=190;
            if(finished) Check(column.isPlaying&&column.particleCount>200&&tower.IgnitionCount==1,"190秒含点亮与暂停后的持续稳定运行");
            var report=new RuntimeReport {elapsedSeconds=duration,gameSeconds=Time.time,frames=Time.frameCount-SessionState.GetInt("FirstVfxFrame",Time.frameCount),
                count=manager.Count,lit=tower.IsLit,ignitionCount=tower.IgnitionCount,particles=column.particleCount,columnTime=column.time,
                errors=SessionState.GetInt("FirstVfxErrors",0),warnings=SessionState.GetInt("FirstVfxWarnings",0),playing=!finished,
                checks=File.ReadAllLines(Output+"/RuntimeChecks.txt")};
            File.WriteAllText(Output+"/RuntimeMonitor.json",JsonUtility.ToJson(report,true));
            if(finished) { SessionState.SetBool("FirstVfxObserve",false); SessionState.SetBool("FirstVfxHalfRendered",false); SessionState.SetBool("FirstVfxTwoRendered",false); EditorApplication.isPlaying=false; }
        }
        catch(Exception e) { File.AppendAllText(Output+"/RuntimeChecks.txt","FAILED "+e+"\n"); SessionState.SetBool("FirstVfxObserve",false); EditorApplication.isPlaying=false; Debug.LogException(e); }
    }
    private static void RenderViews(string suffix)
    {
        Render("05回望_"+suffix,new Vector3(-23,5.73f,16),CorePosition);
        Render("北西低地_"+suffix,new Vector3(-26,1.56f,48),new Vector3(CorePosition.x,9,CorePosition.z));
        Render("最远北端_"+suffix,new Vector3(-28,1.53f,61),new Vector3(CorePosition.x,9,CorePosition.z));
        Render("近塔_"+suffix,new Vector3(3,1.53f,-5),new Vector3(CorePosition.x,10,CorePosition.z));
    }
    private static void Render(string name,Vector3 eye,Vector3 target)
    {
        Directory.CreateDirectory(Output+"/视图"); GameObject go=new GameObject("灯塔核查临时摄像机") {hideFlags=HideFlags.HideAndDontSave};
        Camera camera=go.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.transform.position=eye; camera.transform.LookAt(target);
        camera.enabled=false; camera.fieldOfView=60; camera.nearClipPlane=.05f; camera.farClipPlane=300;
        var data=camera.GetUniversalAdditionalCameraData(); var original=Camera.main.GetUniversalAdditionalCameraData();
        data.renderPostProcessing=original.renderPostProcessing; data.volumeLayerMask=original.volumeLayerMask; data.volumeTrigger=go.transform;
        data.antialiasing=original.antialiasing;
        RenderTexture rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGBHalf); RenderTexture previous=RenderTexture.active;
        try
        {
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply();
            File.WriteAllBytes(Output+"/视图/"+name+".png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { RenderTexture.active=previous; camera.targetTexture=null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(go); }
    }
    [MenuItem("Tools/第一岛灯塔/强化粒子并保存")]
    private static void BuildMenu() { Build(); }
}
