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
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class LighthouseVfxSyncBuilder
{
    public const string Output="Temp/EditorTools/灯塔粒子同步二三岛_20261003";
    private const string Command="Library/LighthouseVfxSync.command";
    private static double nextPoll,nextObserve;
    static LighthouseVfxSyncBuilder()
    { EditorApplication.update+=Poll; EditorApplication.update+=Observe; Application.logMessageReceived+=OnLog; }
    private static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextPoll) return;
        nextPoll=EditorApplication.timeSinceStartup+.5;
        if(!File.Exists(Command)) return; string command=File.ReadAllText(Command).Trim(); if(command.Length==0) return;
        File.WriteAllText(Command,""); Directory.CreateDirectory(Output);
        try
        {
            if(command=="backup") Backup(); else if(command=="sync") Sync(); else if(command=="validate") Validate();
            else if(command=="save")
            {
                Scene scene=Scene(); Validate();
                if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存失败。");
                File.WriteAllText("Library/MorningFogGreybox.command","restore-particles");
            }
            else if(command=="restore-intervals") PreserveBurstIntervals();
            else if(command=="test") StartTest();
            else if(command=="stop") { SessionState.SetBool("SyncVfxObserve",false); EditorApplication.isPlaying=false; }
            else throw new InvalidOperationException("未知同步命令："+command);
            File.WriteAllText(Output+"/last-result.txt",command+" OK "+DateTime.UtcNow.ToString("O"));
        }
        catch(Exception e) { File.WriteAllText(Output+"/last-result.txt",command+" FAILED "+e); Debug.LogException(e); }
    }
    private static Scene Scene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("必须先退出播放模式。");
        Scene scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/game.unity"||SceneManager.sceneCount!=1) throw new InvalidOperationException("仅允许操作正式game场景。");
        return scene;
    }
    private static GameObject[] Objects(Scene scene)
    { return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray(); }
    private static LighthouseController[] Towers(Scene scene)
    { return Objects(scene).SelectMany(g=>g.GetComponents<LighthouseController>()).OrderBy(t=>t.island).ToArray(); }
    private static string Hash(string s)
    { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-",""); }
    private static ParticleSystem Column(LighthouseController t,bool ignition=false)
    {
        string name=t.island==1?(ignition?"FirstIslandIgnitionLift":"FirstIslandGoldColumn"):("Island"+t.island+(ignition?"IgnitionLift":"GoldColumn"));
        Transform child=(ignition?t.ignitionParticles:t.steadyParticles).transform.Find(name);
        return child!=null?child.GetComponent<ParticleSystem>():null;
    }
    private static void Backup()
    {
        Scene scene=Scene(); if(scene.isDirty) throw new InvalidOperationException("当前场景有未保存改动，禁止覆盖。");
        Directory.CreateDirectory(Output+"/备份");
        if(File.Exists(Output+"/备份/game.unity")) throw new InvalidOperationException("本轮备份已经存在，不能覆盖。");
        File.Copy(scene.path,Output+"/备份/game.unity");
        if(!EditorSceneManager.SaveScene(scene,Output+"/备份/game_内存场景.unity",true)) throw new InvalidOperationException("内存备份失败。");
        File.WriteAllLines(Output+"/备份/组件基线.tsv",Objects(scene).SelectMany(g=>g.GetComponents<Component>()).Where(c=>c!=null)
            .Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c)+"\t"+c.GetType().Name+"\t"+Hash(EditorJsonUtility.ToJson(c))));
        string[] files=new[]{"Assets/Scripts","Assets/MorningFogGreybox","Assets/Materials/IslandPrototype","Assets/Shaders","Assets/FirstIslandLighthouseVfx"}
            .Where(folder=>folder!="Assets/MorningFogGreybox"||Directory.Exists(folder)).SelectMany(folder=>Directory.GetFiles(folder,"*",SearchOption.AllDirectories)).Concat(new[]{"Assets/Scenes/start.unity"}).ToArray();
        var list=new FirstIslandLighthouseVfxBuilder.SourceList {files=files.Select(p=>new FirstIslandLighthouseVfxBuilder.SourceRecord {path=p.Replace('\\','/'),hash=Hash(File.ReadAllText(p))}).ToArray()};
        File.WriteAllText(Output+"/备份/源文件基线.json",JsonUtility.ToJson(list,true));
    }
    private static void CopyColumn(ParticleSystem source,LighthouseController target,bool ignition)
    {
        Transform parent=(ignition?target.ignitionParticles:target.steadyParticles).transform;
        string name="Island"+target.island+(ignition?"IgnitionLift":"GoldColumn"); Transform child=parent.Find(name);
        if(child==null)
        {
            GameObject go=UnityEngine.Object.Instantiate(source.gameObject,parent,false); go.name=name;
            Undo.RegisterCreatedObjectUndo(go,"同步灯塔粒子柱"); child=go.transform;
        }
        ParticleSystem p=child.GetComponent<ParticleSystem>(); p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        EditorUtility.CopySerialized(source,p);
        EditorUtility.CopySerialized(source.GetComponent<ParticleSystemRenderer>(),p.GetComponent<ParticleSystemRenderer>());
        // 发射中心采用目标灯芯原位置，保持世界向上；不能照搬第一岛的世界坐标。
        child.SetPositionAndRotation(target.ignitionParticles.transform.position,source.transform.rotation);
        child.localScale=source.transform.localScale; EditorUtility.SetDirty(p);
    }
    private static void Sync()
    {
        Scene scene=Scene(); if(scene.isDirty) throw new InvalidOperationException("禁止覆盖当前未保存的修改。");
        if(!File.Exists(Output+"/备份/game.unity")) throw new InvalidOperationException("缺少本轮备份。");
        LighthouseController[] towers=Towers(scene); if(towers.Length!=3) throw new InvalidOperationException("必须存在三个唯一灯塔。");
        LighthouseController source=towers.Single(t=>t.island==1);
        foreach(LighthouseController t in towers.Where(t=>t.island>1))
        {
            CopyColumn(Column(source),t,false); CopyColumn(Column(source,true),t,true);
            Undo.RecordObject(t.core,"同步灯芯材质"); t.core.sharedMaterial=source.core.sharedMaterial;
            Undo.RecordObject(t.ring,"同步扩散环材质与宽度"); t.ring.sharedMaterial=source.ring.sharedMaterial; t.ring.widthMultiplier=source.ring.widthMultiplier;
            var main=t.steadyParticles.main; main.playOnAwake=false;
        }
        EditorSceneManager.MarkSceneDirty(scene); Validate();
        if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("同步后保存失败。");
        File.WriteAllText("Library/MorningFogGreybox.command","restore-particles");
    }
    private static string ParticleSettings(ParticleSystem p)
    { return Regex.Replace(EditorJsonUtility.ToJson(p),"\"m_GameObject\":\\{[^}]*\\}","\"m_GameObject\":{}"); }
    private static Dictionary<string,string> SceneBlocks(string text)
    {
        var headers=Regex.Matches(text,@"(?m)^--- !u!\d+ &(-?\d+)(?: stripped)?\r?$"); var blocks=new Dictionary<string,string>();
        for(int i=0;i<headers.Count;i++) { int end=i+1<headers.Count?headers[i+1].Index:text.Length;
            blocks.Add(headers[i].Groups[1].Value,text.Substring(headers[i].Index,end-headers[i].Index)); }
        return blocks;
    }
    private static void PreserveBurstIntervals()
    {
        Scene scene=Scene(); if(scene.isDirty) throw new InvalidOperationException("必须先保存才能保全原粒子序列化字段。");
        string original=File.ReadAllText(Output+"/备份/game.unity"),current=File.ReadAllText(scene.path);
        var a=SceneBlocks(original); var b=SceneBlocks(current); var restored=new List<string>();
        foreach(var item in a.Where(x=>x.Value.StartsWith("--- !u!198 ")))
        {
            if(!b.TryGetValue(item.Key,out string after)||item.Value==after) continue;
            // 只保全完全等同于Unity单字段规范化的原块，其他差异仍交给范围核查拒绝。
            string normalized=item.Value.Replace("repeatInterval: 0.0001","repeatInterval: 0.01");
            if(normalized==item.Value||normalized!=after) continue;
            current=current.Replace(after,item.Value); restored.Add(item.Key+"：保全原repeatInterval=0.0001。");
        }
        if(restored.Count>0) File.WriteAllText(scene.path,current,new UTF8Encoding(false));
        File.WriteAllLines(Output+"/SerializedIntervalRestore.txt",restored);
    }
    [Serializable] public class ValidationReport {public string[] failures,changed;public int towers,columns,ignitionLifts,protectedFiles;public Vector3[] centers;}
    private static void Validate()
    {
        Scene scene=Scene(); var failures=new List<string>(); var changed=new List<string>(); var towers=Towers(scene);
        var current=Objects(scene).SelectMany(g=>g.GetComponents<Component>()).Where(c=>c!=null).GroupBy(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()).ToDictionary(g=>g.Key,g=>g.First());
        var allowed=new HashSet<string>(towers.Where(t=>t.island>1).SelectMany(t=>new Component[]{t.core,t.ring,t.steadyParticles.transform,t.ignitionParticles.transform}).Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).ToString()));
        foreach(string line in File.ReadAllLines(Output+"/备份/组件基线.tsv"))
        {
            string[] parts=line.Split('\t'); if(!current.TryGetValue(parts[0],out Component c)) { failures.Add("原组件丢失："+parts[0]); continue; }
            string json=EditorJsonUtility.ToJson(c);
            if(Hash(json)==parts[2]) continue;
            changed.Add(c.name+"/"+parts[1]+"/"+parts[0]);
            bool onlyAwake=towers.Where(t=>t.island>1).Any(t=>c==t.steadyParticles)&&Hash(json.Replace("\"playOnAwake\":false","\"playOnAwake\":true"))==parts[2];
            if(!allowed.Contains(parts[0])&&!onlyAwake) failures.Add("原组件越界变化："+parts[0]);
        }
        var source=towers.Single(t=>t.island==1);
        foreach(var t in towers)
        {
            foreach(bool ignition in new[]{false,true})
            {
                ParticleSystem p=Column(t,ignition);
                if(p==null) { failures.Add("缺少粒子子系统："+t.island+"/"+ignition); continue; }
                if(ParticleSettings(p)!=ParticleSettings(Column(source,ignition))) failures.Add("粒子参数未完整同步："+t.island+"/"+ignition);
                if(Vector3.Distance(p.transform.position,t.ignitionParticles.transform.position)>.001f) failures.Add("发射中心错误："+t.island);
                if(Vector3.Dot(p.transform.forward,Vector3.up)<.999f) failures.Add("发射方向不是世界向上："+t.island);
                if(p.GetComponent<ParticleSystemRenderer>().sharedMaterial!=Column(source,ignition).GetComponent<ParticleSystemRenderer>().sharedMaterial) failures.Add("粒子材质不同："+t.island);
            }
            if(t.core.sharedMaterial!=source.core.sharedMaterial||t.ring.sharedMaterial!=source.ring.sharedMaterial||Mathf.Abs(t.ring.widthMultiplier-source.ring.widthMultiplier)>.0001f) failures.Add("灯芯或环参数不同："+t.island);
        }
        var sources=JsonUtility.FromJson<FirstIslandLighthouseVfxBuilder.SourceList>(File.ReadAllText(Output+"/备份/源文件基线.json"));
        foreach(var f in sources.files) if(!File.Exists(f.path)||Hash(File.ReadAllText(f.path))!=f.hash) failures.Add("受保护文件改变："+f.path);
        if(Objects(scene).SelectMany(g=>g.GetComponents<StardustPickup>()).Count()!=23) failures.Add("星尘数量改变。");
        var report=new ValidationReport {failures=failures.ToArray(),changed=changed.ToArray(),towers=towers.Length,columns=towers.Count(t=>Column(t)!=null),
            ignitionLifts=towers.Count(t=>Column(t,true)!=null),protectedFiles=sources.files.Length,centers=towers.Select(t=>Column(t).transform.position).ToArray()};
        File.WriteAllText(Output+"/Validation.json",JsonUtility.ToJson(report,true));
        if(failures.Count>0) throw new InvalidOperationException(string.Join("\n",failures));
    }
    private static void Check(bool pass,string name)
    { File.AppendAllText(Output+"/RuntimeChecks.txt",(pass?"PASS ":"FAIL ")+name+"\n"); if(!pass) throw new InvalidOperationException(name); }
    private static void StartTest()
    {
        Scene scene=Scene(); if(scene.isDirty) throw new InvalidOperationException("必须保存后播放。");
        SessionState.SetBool("SyncVfxObserve",true); SessionState.SetInt("SyncVfxStage",0);
        SessionState.SetBool("SyncVfxHalf",false); SessionState.SetInt("SyncVfxErrors",0); SessionState.SetInt("SyncVfxWarnings",0);
        File.WriteAllText(Output+"/RuntimeChecks.txt",""); File.WriteAllText(Output+"/RuntimeMessages.txt",""); EditorApplication.isPlaying=true;
    }
    private static void OnLog(string message,string stack,LogType type)
    {
        if(!SessionState.GetBool("SyncVfxObserve",false)||!EditorApplication.isPlaying) return;
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) SessionState.SetInt("SyncVfxErrors",SessionState.GetInt("SyncVfxErrors",0)+1);
        if(type==LogType.Warning) SessionState.SetInt("SyncVfxWarnings",SessionState.GetInt("SyncVfxWarnings",0)+1);
        if(type!=LogType.Log) File.AppendAllText(Output+"/RuntimeMessages.txt",type+": "+message+"\n");
    }
    private static string Signature(ParticleSystem p)
    {
        var particles=new ParticleSystem.Particle[p.main.maxParticles]; int n=p.GetParticles(particles); var text=new StringBuilder();
        for(int i=0;i<n;i++) text.Append(particles[i].position.ToString("F6")).Append('/').Append(particles[i].remainingLifetime.ToString("F6"));
        return Hash(text.ToString());
    }
    [Serializable] public class RuntimeReport
    {
        public string method="后台Play接口收集、暂停与相机渲染；非真人按键验收。"; public float elapsedSeconds,gameSeconds;
        public int frames,errors,warnings; public bool playing; public int[] islandCounts,ignitionCounts,particles; public string[] checks;
    }
    private static void Observe()
    {
        if(!SessionState.GetBool("SyncVfxObserve",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<nextObserve) return;
        nextObserve=EditorApplication.timeSinceStartup+.1;
        try
        {
            var ts=UnityEngine.Object.FindObjectsOfType<LighthouseController>().OrderBy(t=>t.island).ToArray(); var manager=StardustManager.Instance;
            if(ts.Length!=3||manager==null||Time.frameCount<5) return; Application.runInBackground=true;
            int stage=SessionState.GetInt("SyncVfxStage",0),island=SessionState.GetInt("SyncVfxIsland",2);
            if(stage==0)
            {
                SessionState.SetFloat("SyncVfxWall",(float)EditorApplication.timeSinceStartup); SessionState.SetInt("SyncVfxFrame",Time.frameCount);
                Check(manager.Count==0&&ts.All(t=>!t.IsLit&&!Column(t).isPlaying&&!Column(t,true).isPlaying),"新局三个灯塔及新子粒子全部未点亮");
                RenderViews(ts[1],"未点亮"); RenderViews(ts[2],"未点亮");
                for(int id=1;id<=5;id++) manager.TryCollect(manager.memories.Single(m=>m.id==id));
                Check(ts[0].IsLit&&ts[0].IgnitionCount==1&&!ts[1].IsLit&&!ts[2].IsLit,"第一岛原效果及独立点亮条件保持");
                Prepare(manager,ts[1]); SessionState.SetInt("SyncVfxIsland",2); SessionState.SetInt("SyncVfxStage",1);
            }
            else if(stage==1&&Time.time-SessionState.GetFloat("SyncVfxGame",0)>.3f)
            {
                int last=island==2?13:23; Check(manager.TryCollect(manager.memories.Single(m=>m.id==last)),"第"+island+"岛最后一颗接口收集");
                LighthouseController t=ts[island-1]; Check(t.IsLit&&t.IgnitionCount==1&&Column(t).isPlaying&&Column(t,true).isPlaying,"第"+island+"岛集齐后两个子系统首次启动");
                manager.dialogue?.CloseCurrent(); SessionState.SetFloat("SyncVfxLitTime",Time.time); SessionState.SetBool("SyncVfxHalf",false); SessionState.SetInt("SyncVfxStage",2);
            }
            else if(stage==2)
            {
                var t=ts[island-1]; float elapsed=Time.time-SessionState.GetFloat("SyncVfxLitTime",0);
                if(elapsed>.5f&&!SessionState.GetBool("SyncVfxHalf",false)) { Render(t,"点亮0.5秒",false); SessionState.SetBool("SyncVfxHalf",true); }
                if(elapsed>5)
                {
                    Check(!t.ring.enabled&&Column(t).particleCount>200&&Column(t).particleCount<=500&&!Column(t,true).IsAlive(true),"第"+island+"岛环与爆发结束、常驻柱持续");
                    RenderViews(t,"常驻5秒"); Check(!manager.TryCollect(manager.memories.Single(m=>m.id==(island==2?13:23))),"第"+island+"岛重复收集拒绝");
                    typeof(LighthouseController).GetMethod("OnProgress",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(t,new object[]{island==2?13:23});
                    Check(t.IgnitionCount==1&&!Column(t,true).IsAlive(true),"第"+island+"岛重复通知不再次爆发");
                    if(island==2) { Check(!ts[2].IsLit,"第二岛点亮不提前点亮第三島"); Prepare(manager,ts[2]); SessionState.SetInt("SyncVfxIsland",3); SessionState.SetInt("SyncVfxStage",1); }
                    else
                    {
                        var owner=new GameObject("三塔同步暂停核查") {hideFlags=HideFlags.HideAndDontSave}; GameplayPause.Set(owner,true);
                        foreach(var tower in ts) { SessionState.SetString("SyncVfxSignature"+tower.island,Signature(Column(tower))); SessionState.SetFloat("SyncVfxTime"+tower.island,Column(tower).time); }
                        SessionState.SetFloat("SyncVfxPauseWall",(float)EditorApplication.timeSinceStartup); SessionState.SetInt("SyncVfxStage",3);
                    }
                }
            }
            else if(stage==3&&EditorApplication.timeSinceStartup-SessionState.GetFloat("SyncVfxPauseWall",0)>2)
            {
                Check(GameplayPause.IsPaused&&Time.timeScale==0&&ts.All(t=>Signature(Column(t))==SessionState.GetString("SyncVfxSignature"+t.island,"")
                    &&Mathf.Abs(Column(t).time-SessionState.GetFloat("SyncVfxTime"+t.island,0))<.0001f),"暂停2秒三座塔粒子时间、位置与寿命均冻结");
                var owner=GameObject.Find("三塔同步暂停核查"); GameplayPause.Set(owner,false); UnityEngine.Object.Destroy(owner);
                SessionState.SetFloat("SyncVfxGame",Time.time); SessionState.SetInt("SyncVfxStage",4);
            }
            else if(stage==4&&Time.time-SessionState.GetFloat("SyncVfxGame",0)>1)
            {
                Check(!GameplayPause.IsPaused&&ts.All(t=>Column(t).isPlaying&&Signature(Column(t))!=SessionState.GetString("SyncVfxSignature"+t.island,"")),"恢复后三座塔粒子继续运动");
                SessionState.SetInt("SyncVfxStage",5);
            }
            float duration=(float)EditorApplication.timeSinceStartup-SessionState.GetFloat("SyncVfxWall",(float)EditorApplication.timeSinceStartup); bool finished=stage==5&&duration>=190;
            if(finished) Check(ts.All(t=>t.IgnitionCount==1&&Column(t).isPlaying&&Column(t).particleCount>200),"三塔同时常驻190秒稳定运行");
            var result=new RuntimeReport {elapsedSeconds=duration,gameSeconds=Time.time,frames=Time.frameCount-SessionState.GetInt("SyncVfxFrame",Time.frameCount),
                errors=SessionState.GetInt("SyncVfxErrors",0),warnings=SessionState.GetInt("SyncVfxWarnings",0),playing=!finished,
                islandCounts=ts.Select(t=>manager.GetIslandCount(t.island)).ToArray(),ignitionCounts=ts.Select(t=>t.IgnitionCount).ToArray(),particles=ts.Select(t=>Column(t).particleCount).ToArray(),checks=File.ReadAllLines(Output+"/RuntimeChecks.txt")};
            File.WriteAllText(Output+"/RuntimeMonitor.json",JsonUtility.ToJson(result,true));
            if(finished) { SessionState.SetBool("SyncVfxObserve",false); EditorApplication.isPlaying=false; }
        }
        catch(Exception e) { File.AppendAllText(Output+"/RuntimeChecks.txt","FAILED "+e+"\n"); SessionState.SetBool("SyncVfxObserve",false); EditorApplication.isPlaying=false; Debug.LogException(e); }
    }
    private static void Prepare(StardustManager manager,LighthouseController t)
    {
        int first=t.island==2?6:14,last=t.island==2?13:23;
        for(int id=first;id<last;id++) manager.TryCollect(manager.memories.Single(m=>m.id==id));
        Check(manager.GetIslandCount(t.island)==StardustManager.IslandTotal(t.island)-1&&!t.IsLit&&!Column(t).isPlaying,"第"+t.island+"岛差一颗不点亮（"+(last-first)+"/"+(last-first+1)+"）");
        SessionState.SetFloat("SyncVfxGame",Time.time);
    }
    private static void RenderViews(LighthouseController t,string phase) { Render(t,phase,false); Render(t,phase,true); }
    private static void Render(LighthouseController t,string phase,bool far)
    {
        Directory.CreateDirectory(Output+"/视图"); Vector3 center=t.ignitionParticles.transform.position;
        var go=new GameObject("同步粒子临时核查相机") {hideFlags=HideFlags.HideAndDontSave}; Camera c=go.AddComponent<Camera>(); c.CopyFrom(Camera.main);
        c.transform.position=center+(far?new Vector3(-24,-3.71f,50):new Vector3(7,-3.71f,-3.5f)); c.transform.LookAt(center+Vector3.up*(far?3.5f:4.8f));
        c.enabled=false; c.fieldOfView=60; c.nearClipPlane=.05f; c.farClipPlane=400;
        var data=c.GetUniversalAdditionalCameraData(); var original=Camera.main.GetUniversalAdditionalCameraData(); data.renderPostProcessing=original.renderPostProcessing; data.volumeLayerMask=original.volumeLayerMask; data.volumeTrigger=go.transform;
        RenderTexture rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGBHalf); var previous=RenderTexture.active;
        try { c.targetTexture=rt; c.Render(); RenderTexture.active=rt; var texture=new Texture2D(1600,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply();
            File.WriteAllBytes(Output+"/视图/第"+t.island+"岛_"+(far?"远处":"近塔")+"_"+phase+".png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); }
        finally { RenderTexture.active=previous; c.targetTexture=null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(go); }
    }
    [MenuItem("Tools/灯塔粒子/同步第一塔效果到二三塔")]
    private static void SyncMenu() { Sync(); }
}
