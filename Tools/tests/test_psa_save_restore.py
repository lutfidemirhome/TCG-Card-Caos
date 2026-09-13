"""Run the production PSA save/seat-selection methods with a tiny Unity test double.

Requires csc and mono. Does not launch Unity or touch player saves.
"""
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def method(path, signature):
    source = (ROOT / path).read_text()
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]


lookup = method("Assets/Scripts/Core/Save/GameSaveRestore.cs",
                "static PsaCabinetSlot FindPsaSlot(")
collect = method("Assets/Scripts/Core/Save/GameSaveWorldCollector.cs",
                 "static void ApplyPsaRecord(")
restore = method("Assets/Scripts/Interaction/PsaCabinetSlot.cs",
                 "public bool RestoreOccupiedCard(WorldCard card, bool playPlacementFeedback)")

source = r'''
using System;
using UnityEngine;
namespace UnityEngine {
    public struct Vector3 {
        public float x;
        public Vector3(float value) { x = value; }
        public float sqrMagnitude => x * x;
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x);
    }
    public struct Quaternion { }
    public class Transform { public string path; }
    public enum FindObjectsInactive { Exclude }
    public enum FindObjectsSortMode { None }
    public static class Mathf { public static float Abs(float f) => Math.Abs(f); }
    public class Object {
        internal static PsaCabinetSlot[] slots;
        public static T[] FindObjectsByType<T>(FindObjectsInactive a, FindObjectsSortMode b)
            => (T[])(object)slots;
    }
}
class PersistentId {
    public static object GetOrCreate(object o) => o;
    public static string BuildPathFallback(Transform t) => t.path;
}
enum CardRuntimeLocation { PsaCabinet }
class CardSaveRecord {
    public string psaSlotPath, psaCabinetId;
    public int psaCabinetSlot;
    public CardRuntimeLocation location;
    public Vector3 Position;
    public void SetPosition(Vector3 p) { Position = p; }
    public void SetRotation(Quaternion q) { }
}
class WorldCard {
    public void PlaceOnPsaCabinetSlot(Transform p, Vector3 v, Quaternion q, Vector3 s) { }
    public void NotifyShelfPlacement(bool correct) { }
}
class PsaCabinet {
    public object gameObject = new object();
    public Transform transform = new Transform();
}
class PsaCabinetSlot {
    public Transform transform = new Transform();
    public int SlotNumber;
    public Vector3 position;
    public WorldCard occupiedCard;
    public PsaCabinet cabinet;
    public bool IsEmpty => occupiedCard == null;
    public void Occupy(WorldCard c) { occupiedCard = c; }
    public bool IsCorrectPlacement(WorldCard c) => true;
    public void GetPlacementPose(out Vector3 p, out Quaternion q) { p=position; q=default; }
    void BuildPlacementLocalPose(out Transform t, out Vector3 p, out Quaternion q,
        out Vector3 s, out Vector3 wp, out Quaternion wq) {
        t=transform; p=position; q=default; s=default; wp=p; wq=q;
    }
    RESTORE
}
class Test {
    LOOKUP
    COLLECT
    static PsaCabinet ResolvePsaCabinet(PsaCabinetSlot s) => s.cabinet;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    static PsaCabinetSlot Seat(string path, float x, int grade=7) => new PsaCabinetSlot {
        transform=new Transform { path=path }, position=new Vector3(x), SlotNumber=grade,
        cabinet=new PsaCabinet { transform=new Transform { path=path.Split('/')[0] } }
    };
    static void Main() {
        // Same grade, multiple seats per cabinet, multiple cabinets; load in reverse order.
        var slots = new PsaCabinetSlot[180];
        var records = new CardSaveRecord[180];
        for (int i=0; i<slots.Length; i++) {
            slots[i]=Seat("cabinet"+(i/10)+"/holder"+(i%10), i*0.2f, 7+(i/10)%4);
            records[i]=new CardSaveRecord { Position=new Vector3(-100) };
            ApplyPsaRecord(records[i], slots[i]);
            Check(records[i].Position.x==slots[i].position.x, "save during placement uses target pose");
        }
        UnityEngine.Object.slots=slots;
        for (int i=179; i>=0; i--) {
            var seat=FindPsaSlot(records[i], slots[i].SlotNumber);
            Check(seat==slots[i], "exact seat round trip "+i);
            var card=new WorldCard();
            Check(seat.RestoreOccupiedCard(card,false), "restore");
            Check(seat.RestoreOccupiedCard(card,false), "same-card finalization");
            Check(!seat.RestoreOccupiedCard(new WorldCard(),false), "never overwrite occupied seat");
            Check(FindPsaSlot(records[i],7)==null, "duplicate destination rejected");
        }
        foreach (var slot in slots) slot.occupiedCard=null;
        Check(FindPsaSlot(records[25],10)==slots[25], "preserve wrong-grade player placement");
        records[25].psaSlotPath="missing";
        Check(FindPsaSlot(records[25],7)==null, "missing exact path must not choose another seat");
        var a=Seat("a",0); var b=Seat("b",0.2f);
        UnityEngine.Object.slots=new[] { a,b };
        var old=new CardSaveRecord { psaCabinetId="shared-prefab-id", Position=new Vector3(0.2f) };
        Check(FindPsaSlot(old,7)==b, "legacy same-grade position recovery");
        b.occupiedCard=new WorldCard();
        Check(FindPsaSlot(old,7)==null, "legacy occupied destination must not move to neighbor");
        b.occupiedCard=null;
        old.Position=new Vector3(20);
        Check(FindPsaSlot(old,7)==null, "legacy unknown position");
        old.Position=new Vector3(0.1f);
        Check(FindPsaSlot(old,7)==null, "legacy equidistant seats");
        b.transform.path="a";
        old.psaSlotPath="a";
        Check(FindPsaSlot(old,7)==null, "ambiguous exact path");
        Console.WriteLine("PASS: 180 PSA seat round trips, placement pose, occupancy, legacy recovery and ambiguity checks.");
    }
}
'''.replace("LOOKUP", lookup).replace("COLLECT", collect).replace("RESTORE", restore)

with tempfile.TemporaryDirectory(prefix="tcg-psa-test-") as folder:
    folder = Path(folder)
    (folder / "Test.cs").write_text(source)
    subprocess.run(["csc", "-nologo", "-langversion:latest", "-out:" + str(folder / "Test.exe"),
                    str(folder / "Test.cs")], check=True)
    subprocess.run(["mono", str(folder / "Test.exe")], check=True)
