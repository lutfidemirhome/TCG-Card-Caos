// Isolated test doubles: production achievement sources are compiled unchanged beside this file.
// No Steam library, network, Unity scene, save file or real account is accessed.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object x)=>x!=null; }
 public class MonoBehaviour:Object { public bool enabled=true; public static void DontDestroyOnLoad(Object x){} }
 public class GameObject:Object { public GameObject(string x){} public T AddComponent<T>() where T:new()=>new T(); }
 public enum RuntimeInitializeLoadType { SubsystemRegistration, BeforeSceneLoad }
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
 public static class Application { public static bool isEditor; }
 public static class Time { public static float realtimeSinceStartup; }
 public static class Mathf { public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(b,v)); public static int Max(int a,int b)=>Math.Max(a,b); public static float Max(float a,float b)=>Math.Max(a,b); }
 public static class Debug { public static readonly List<string> Logs = new List<string>(); public static void LogWarning(object x){} public static void Log(object x){Logs.Add(x.ToString());} }
 public static class PlayerPrefs { public static Dictionary<string,int> Data=new Dictionary<string,int>(); public static int Saves; public static int GetInt(string k,int d)=>Data.TryGetValue(k,out var v)?v:d; public static void SetInt(string k,int v)=>Data[k]=v; public static void Save()=>Saves++; }
}
public static class GameBuildVariant { public static bool IsDemo; }
public static class SteamFullGameStore { public const uint FullGameAppId=5125130; }
namespace Steamworks {
 public enum EResult { k_EResultOK, k_EResultFail }
 public struct UserStatsStored_t { public ulong m_nGameID; public EResult m_eResult; }
 public struct UserAchievementStored_t { public ulong m_nGameID; public uint m_nCurProgress,m_nMaxProgress; public string m_rgchAchievementName; }
 public sealed class Callback<T>:IDisposable { public static Action<T> Handler; public static Callback<T> Create(Action<T> h){Handler=h;return new Callback<T>();} public void Dispose(){Handler=null;} }
 public struct AppId_t { public uint m_AppId; }
 public struct CSteamID { public ulong m_SteamID; }
 public static class SteamUtils { public static uint App=5125130; public static AppId_t GetAppID()=>new AppId_t{m_AppId=App}; }
 public static class SteamUser { public static ulong User=10; public static CSteamID GetSteamID()=>new CSteamID{m_SteamID=User}; }
 public static class SteamAPI { public static bool Available=true; public static int InitCalls,Pumps,Shutdowns; public static bool ThrowPump; public static bool Init(){InitCalls++;return Available;} public static void RunCallbacks(){Pumps++;if(ThrowPump)throw new Exception("offline");} public static void Shutdown(){Shutdowns++;} }
 public static class SteamUserStats {
  public static Dictionary<string,bool> State=new Dictionary<string,bool>(); public static List<string> Set=new List<string>(); public static bool Definitions=true, StoreOK=true; public static int Stores,Reads;
  public static bool GetAchievement(string s,out bool b){Reads++;b=State.TryGetValue(s,out var v)&&v;return Definitions;}
  public static bool SetAchievement(string s){Set.Add(s);State[s]=true;return true;}
  public static bool StoreStats(){Stores++;return StoreOK;}
 }
}
static class Harness {
 static int checks;
 static void Check(bool x,string message){if(!x)throw new Exception(message);checks++;}
 static object Call(object x,string n,params object[] a)=>x.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(x,a);
 static void ResetBridge()=>typeof(SteamSkillAchievements).GetMethod("ResetSession",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
 static void Tick(object p,float t){UnityEngine.Time.realtimeSinceStartup=t;Call(p,"Update");}
 static SteamSkillAchievementsProvider Fresh(bool clearPrefs=true){
  ResetBridge();GameBuildVariant.IsDemo=false;UnityEngine.Application.isEditor=false;UnityEngine.Time.realtimeSinceStartup=0;
  Steamworks.SteamAPI.Available=true;Steamworks.SteamAPI.ThrowPump=false;Steamworks.SteamAPI.InitCalls=Steamworks.SteamAPI.Pumps=Steamworks.SteamAPI.Shutdowns=0;
  Steamworks.SteamUtils.App=5125130;Steamworks.SteamUser.User=10;Steamworks.SteamUserStats.State.Clear();Steamworks.SteamUserStats.Set.Clear();Steamworks.SteamUserStats.Stores=Steamworks.SteamUserStats.Reads=0;Steamworks.SteamUserStats.Definitions=Steamworks.SteamUserStats.StoreOK=true;
  if(clearPrefs)UnityEngine.PlayerPrefs.Data.Clear();
  var p=new SteamSkillAchievementsProvider();Call(p,"Awake");return p;
 }
 static void Dispose(object p)=>Call(p,"OnDestroy");
 static void Main(){
  var expected=new[]{2,3,4,5,6,7,8,9,10,11,12,14,16,18,21,24,27,31,35,39,44,49,55,61,68,75,83,91,100,109,119,129,140,152,165,179,194,214,239,269,309,359,409,459,509};
  Check(SteamSkillAchievements.Count==46 && SteamSkillAchievements.MilestoneCount==45,"45 milestones plus completion; no extra 13/15/17 achievements");
  for(int i=0;i<expected.Length;i++){Check(SteamSkillAchievements.RequiredRows(i)==expected[i],"goal "+i);Check(SteamSkillAchievements.ApiName(i)=="SKILL_ROWS_"+expected[i].ToString("D3"),"API "+i);}
  foreach(int completed in new[]{12,13,14,15,16,17,18,509}){
   var boundary=Fresh();Tick(boundary,0);SteamSkillAchievements.NotifyProgress(completed);Tick(boundary,2);
   Check(Steamworks.SteamUserStats.Set.SequenceEqual(expected.Where(n=>n<=completed).Select(n=>"SKILL_ROWS_"+n.ToString("D3"))),"only permitted achievements at "+completed+" completions");
   Dispose(boundary);
  }
  ResetBridge();int events=0;SteamSkillAchievements.ProgressChanged+=_=>events++;
  foreach(int n in new[]{-5,0,1,1,0,2,999,510})SteamSkillAchievements.NotifyProgress(n);
  Check(events==3&&SteamSkillAchievements.HighestCompletedRows==509,"monotonic clamped progress");
  ResetBridge();GameBuildVariant.IsDemo=true;SteamSkillAchievements.NotifyProgress(509);Check(SteamSkillAchievements.HighestCompletedRows==0,"demo disabled");
  var p=Fresh();Tick(p,0);SteamSkillAchievements.NotifyProgress(3);Tick(p,1);Check(Steamworks.SteamUserStats.Stores==0,"batched after init");Tick(p,2);
  Check(Steamworks.SteamUserStats.Set.SequenceEqual(new[]{"SKILL_ROWS_002","SKILL_ROWS_003"}),"only earned achievements");
  var read=Steamworks.SteamUserStats.Reads;for(int i=0;i<1000;i++)Tick(p,2.01f+i/1000f);Check(Steamworks.SteamUserStats.Reads==read,"no per-frame achievement scans");
  Call(p,"OnStatsStored",new Steamworks.UserStatsStored_t{m_nGameID=5125130,m_eResult=Steamworks.EResult.k_EResultFail});Tick(p,100);Check(Steamworks.SteamUserStats.Stores==1,"failure backoff");Tick(p,124);Check(Steamworks.SteamUserStats.Stores==2,"failed upload retries even local GetAchievement true");
  Call(p,"OnAchievementStored",new Steamworks.UserAchievementStored_t{m_nGameID=5125130,m_rgchAchievementName="SKILL_ROWS_002"});Call(p,"OnStatsStored",new Steamworks.UserStatsStored_t{m_nGameID=5125130,m_eResult=Steamworks.EResult.k_EResultOK});
  Steamworks.SteamUserStats.Set.Clear();Tick(p,139);Check(Steamworks.SteamUserStats.Set.SequenceEqual(new[]{"SKILL_ROWS_003"}),"only explicitly acknowledged ID retired");Dispose(p);
  p=Fresh();Tick(p,0);SteamSkillAchievements.NotifyProgress(2);Tick(p,2);Tick(p,63);Check(Steamworks.SteamUserStats.Stores==1,"missing callback waits then backs off");Tick(p,183);Check(Steamworks.SteamUserStats.Stores==2,"lost callback retry");Dispose(p);
  p=Fresh();Steamworks.SteamAPI.Available=false;SteamSkillAchievements.NotifyProgress(21);Tick(p,2);Tick(p,31);Check(Steamworks.SteamAPI.InitCalls==1,"Steam absent retry rate");Steamworks.SteamAPI.Available=true;Tick(p,32);Tick(p,34);Check(Steamworks.SteamUserStats.Set.Count==15,"late Steam catches up");
  Steamworks.SteamUser.User=20;SteamSkillAchievements.NotifyProgress(509);Tick(p,200);Check(!UnityEngine.PlayerPrefs.Data.ContainsKey("SteamSkillRows.5125130.20"),"account switch does not leak pending progress");Check(Steamworks.SteamUserStats.Set.Count==15,"account switch prevents sends");Dispose(p);
  p=Fresh();Steamworks.SteamUtils.App=5144220;Tick(p,0);Check(!p.enabled&&Steamworks.SteamAPI.Shutdowns==1&&Steamworks.SteamUserStats.Stores==0,"wrong App ID disabled");Dispose(p);
  p=Fresh();Tick(p,0);Steamworks.SteamUserStats.Definitions=false;SteamSkillAchievements.NotifyProgress(2);Tick(p,2);Check(Steamworks.SteamUserStats.Stores==0,"missing definition no invalid send");Steamworks.SteamUserStats.Definitions=true;Tick(p,122);Check(Steamworks.SteamUserStats.Stores==1,"missing definitions recover");Dispose(p);
  p=Fresh();Tick(p,0);SteamSkillAchievements.NotifyProgress(7);Call(p,"OnApplicationQuit");Dispose(p);p=Fresh(false);Tick(p,0);Tick(p,2);Check(Steamworks.SteamUserStats.Set.Count==6,"pending earned progress survives restart");Dispose(p);
  p=Fresh();Tick(p,0);SteamSkillAchievements.NotifyProgress(2);Steamworks.SteamAPI.ThrowPump=true;Tick(p,2);var pumps=Steamworks.SteamAPI.Pumps;Tick(p,10);Check(Steamworks.SteamAPI.Pumps==pumps,"exception pump backoff");Dispose(p);
  p=Fresh();UnityEngine.Debug.Logs.Clear();Tick(p,0);
  Check(Steamworks.SteamUserStats.Reads==0 && UnityEngine.Debug.Logs.Count==0,"release has no test diagnostics or extra API reads");
  Dispose(p);
  p=Fresh();Tick(p,0);SteamSkillAchievements.NotifyProgress(509);Tick(p,2);
  Check(!Steamworks.SteamUserStats.Set.Contains("SHOP_COMPLETE"),"509 tasks alone do not complete the shop");Dispose(p);
  p=Fresh();Tick(p,0);int completionEvents=0;SteamSkillAchievements.CompletionEarned+=()=>completionEvents++;
  SteamSkillAchievements.NotifyGameCompleted();SteamSkillAchievements.NotifyGameCompleted();Tick(p,2);
  Check(completionEvents==1 && Steamworks.SteamUserStats.Set.SequenceEqual(new[]{"SHOP_COMPLETE"}),"completion emits once, independently of upgrades");
  Check(UnityEngine.PlayerPrefs.GetInt("SteamShopComplete.5125130.10",0)==1,"completion journal is per account");
  Call(p,"OnApplicationQuit");Dispose(p);p=Fresh(false);Tick(p,0);Tick(p,2);
  Check(Steamworks.SteamUserStats.Set.Contains("SHOP_COMPLETE"),"pending completion survives restart");Dispose(p);
  p=Fresh();Tick(p,0);GameBuildVariant.IsDemo=true;SteamSkillAchievements.NotifyGameCompleted();Tick(p,2);
  Check(!Steamworks.SteamUserStats.Set.Contains("SHOP_COMPLETE"),"demo cannot earn full game ending");GameBuildVariant.IsDemo=false;Dispose(p);
  p=Fresh();Steamworks.SteamAPI.Available=false;SteamSkillAchievements.NotifyGameCompleted();Tick(p,0);
  Steamworks.SteamAPI.Available=true;Tick(p,30);Tick(p,32);
  Check(Steamworks.SteamUserStats.Set.Contains("SHOP_COMPLETE"),"completion earned offline catches up");Dispose(p);
  p=Fresh();Tick(p,0);Steamworks.SteamUser.User=20;SteamSkillAchievements.NotifyGameCompleted();Tick(p,2);
  Check(Steamworks.SteamUserStats.Set.Count==0 && UnityEngine.PlayerPrefs.GetInt("SteamShopComplete.5125130.10",0)==0,"completion does not leak after account switch");Dispose(p);
  Console.WriteLine("PASS: "+checks+" checks; actual catalogue, bridge and provider sources; fake SDK only; no real account changes.");
 }
}
