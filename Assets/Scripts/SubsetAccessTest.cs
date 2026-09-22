using UnityEngine;
using TMPro;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace AppendixA
{
    // ------------------------------------------------------------------
    // NARROW entity: exactly the three fields the movement loop touches.
    // 28 bytes. This is the control case.
    // ------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct EnemyNarrow
    {
        public Vector3 Position;          // 12
        public Vector3 Direction;         // 12
        public float   Velocity;          //  4   -> 28 bytes
    }

    // ------------------------------------------------------------------
    // WIDE entity: the same three hot fields, plus the baggage that every
    // real enemy actually carries. Sized to exactly 128 bytes, which is
    // one cache line on an Apple-silicon P-core (two lines on most x86).
    // This is not padding-for-the-sake-of-it: it is what an Enemy looks
    // like once it has health, a state machine, and an animation handle.
    // ------------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct EnemyWide
    {
        public Vector3 Position;          // 12   <- the query reads ONLY this
        public Vector3 Direction;         // 12
        public float   Velocity;          //  4   -> 28
        public float   Health;            //  4
        public float   MaxHealth;         //  4
        public int     State;             //  4
        public int     TargetId;          //  4
        public float   StateTimer;        //  4
        public float   AttackCooldown;    //  4
        public float   DamageMultiplier;  //  4
        public int     AnimationId;       //  4
        public int     TeamId;            //  4   -> 64
        public Vector4 Slot0;             // 16
        public Vector4 Slot1;             // 16
        public Vector4 Slot2;             // 16
        public Vector4 Slot3;             // 16   -> 128 bytes
    }

    // Structure-of-arrays form of EnemyWide. The cold arrays are allocated
    // so the total footprint matches the AoS case honestly -- they are just
    // never touched by the loops below. That is the entire point.
    public class EnemyWideSoA
    {
        public Vector3[] Position;
        public Vector3[] Direction;
        public float[]   Velocity;
        public float[]   Health;
        public float[]   MaxHealth;
        public int[]     State;
        public int[]     TargetId;
        public float[]   StateTimer;
        public float[]   AttackCooldown;
        public float[]   DamageMultiplier;
        public int[]     AnimationId;
        public int[]     TeamId;
        public Vector4[] Slot0, Slot1, Slot2, Slot3;
    }

    public class SubsetAccessTest : MonoBehaviour
    {
        public TextMeshProUGUI ResultText;

        [Tooltip("2,000,000 keeps every case out of L2 on an M3. Costs ~620 MB.")]
        public int ArraySize     = 2_000_000;
        public int NumIterations = 100;
        public int Repeats       = 5;

        EnemyNarrow[]  m_narrowAoS;
        Vector3[]      m_narrowPos, m_narrowDir;
        float[]        m_narrowVel;

        EnemyWide[]    m_wideAoS;
        EnemyWideSoA   m_wideSoA = new EnemyWideSoA();

        // Consumed at the end so nothing here can be optimised away.
        public float Checksum;

        // Hoisted so the timed section allocates literally nothing. A method
        // group conversion on an INSTANCE method captures `this` and cannot be
        // cached by the compiler, so `Bench(NarrowMoveAoS)` at the call site
        // would allocate a fresh Action every time.
        System.Action m_narrowMoveAoS, m_narrowMoveSoA;
        System.Action m_wideSumAoS,    m_wideSumSoA;
        System.Action m_wideMoveAoS,   m_wideMoveSoA;
        readonly Stopwatch m_sw = new Stopwatch();

        void Awake()
        {
            m_narrowMoveAoS = NarrowMoveAoS;   m_narrowMoveSoA = NarrowMoveSoA;
            m_wideSumAoS    = WideSumPositionsAoS; m_wideSumSoA  = WideSumPositionsSoA;
            m_wideMoveAoS   = WideMoveAoS;     m_wideMoveSoA   = WideMoveSoA;

            var rnd = new System.Random(12345);
            System.Func<float> R = () => (float)rnd.NextDouble();

            m_narrowAoS = new EnemyNarrow[ArraySize];
            m_narrowPos = new Vector3[ArraySize];
            m_narrowDir = new Vector3[ArraySize];
            m_narrowVel = new float[ArraySize];

            m_wideAoS = new EnemyWide[ArraySize];
            m_wideSoA.Position  = new Vector3[ArraySize];
            m_wideSoA.Direction = new Vector3[ArraySize];
            m_wideSoA.Velocity  = new float[ArraySize];
            m_wideSoA.Health    = new float[ArraySize];
            m_wideSoA.MaxHealth = new float[ArraySize];
            m_wideSoA.State     = new int[ArraySize];
            m_wideSoA.TargetId  = new int[ArraySize];
            m_wideSoA.StateTimer       = new float[ArraySize];
            m_wideSoA.AttackCooldown   = new float[ArraySize];
            m_wideSoA.DamageMultiplier = new float[ArraySize];
            m_wideSoA.AnimationId = new int[ArraySize];
            m_wideSoA.TeamId      = new int[ArraySize];
            m_wideSoA.Slot0 = new Vector4[ArraySize];
            m_wideSoA.Slot1 = new Vector4[ArraySize];
            m_wideSoA.Slot2 = new Vector4[ArraySize];
            m_wideSoA.Slot3 = new Vector4[ArraySize];

            for (int i = 0; i < ArraySize; i++)
            {
                var p = new Vector3(R(), R(), R());
                var d = new Vector3(R(), R(), R());
                var v = R();

                m_narrowAoS[i].Position  = p;
                m_narrowAoS[i].Direction = d;
                m_narrowAoS[i].Velocity  = v;
                m_narrowPos[i] = p; m_narrowDir[i] = d; m_narrowVel[i] = v;

                m_wideAoS[i].Position  = p;
                m_wideAoS[i].Direction = d;
                m_wideAoS[i].Velocity  = v;
                m_wideSoA.Position[i]  = p;
                m_wideSoA.Direction[i] = d;
                m_wideSoA.Velocity[i]  = v;
            }

            ResultText.text = "Ready to run\n";
        }

        // Best-of-N. Memory benchmarks are noisy upward only, so the
        // minimum is the honest estimate, not the mean.
        double Bench(System.Action pass)
        {
            pass();                                   // warm-up, discarded
            double best = double.MaxValue;
            for (int r = 0; r < Repeats; r++)
            {
                m_sw.Restart();
                for (int i = 0; i < NumIterations; i++) pass();
                m_sw.Stop();
                best = System.Math.Min(best, m_sw.Elapsed.TotalSeconds);
            }
            return best;
        }

        // ---- Test A: narrow entity, loop touches 100% of every byte ----

        void NarrowMoveAoS()
        {
            var a = m_narrowAoS;
            for (int i = 0; i < a.Length; i++)
                a[i].Position += a[i].Direction * a[i].Velocity;
        }

        void NarrowMoveSoA()
        {
            var pos = m_narrowPos; var dir = m_narrowDir; var vel = m_narrowVel;
            for (int i = 0; i < pos.Length; i++)
                pos[i] += dir[i] * vel[i];
        }

        // ---- Test B: wide entity, loop touches 12 of 128 bytes ----

        float m_sink;

        void WideSumPositionsAoS()
        {
            var a = m_wideAoS;
            float sx = 0f, sy = 0f, sz = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                sx += a[i].Position.x; sy += a[i].Position.y; sz += a[i].Position.z;
            }
            m_sink += sx + sy + sz;
        }

        void WideSumPositionsSoA()
        {
            var pos = m_wideSoA.Position;
            float sx = 0f, sy = 0f, sz = 0f;
            for (int i = 0; i < pos.Length; i++)
            {
                sx += pos[i].x; sy += pos[i].y; sz += pos[i].z;
            }
            m_sink += sx + sy + sz;
        }

        // ---- Test B': wide entity, loop touches 28 of 128 bytes ----

        void WideMoveAoS()
        {
            var a = m_wideAoS;
            for (int i = 0; i < a.Length; i++)
                a[i].Position += a[i].Direction * a[i].Velocity;
        }

        void WideMoveSoA()
        {
            var pos = m_wideSoA.Position;
            var dir = m_wideSoA.Direction;
            var vel = m_wideSoA.Velocity;
            for (int i = 0; i < pos.Length; i++)
                pos[i] += dir[i] * vel[i];
        }

        public unsafe void RunTest()
        {
            // Settle the heap, then park the collector. Safe because the timed
            // loops allocate nothing -- if that ever stops being true, the
            // "GC delta" line below will say so.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            double aAoS, aSoA, bAoS, bSoA, cAoS, cSoA;
            long heapBefore = System.GC.GetTotalMemory(false);
            var priorMode = GarbageCollector.GCMode;
            GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
            try
            {
                aAoS = Bench(m_narrowMoveAoS);
                aSoA = Bench(m_narrowMoveSoA);
                bAoS = Bench(m_wideSumAoS);
                bSoA = Bench(m_wideSumSoA);
                cAoS = Bench(m_wideMoveAoS);
                cSoA = Bench(m_wideMoveSoA);
            }
            finally
            {
                GarbageCollector.GCMode = priorMode;
            }
            long heapDelta = System.GC.GetTotalMemory(false) - heapBefore;

            long elems = (long)ArraySize * NumIterations;
            System.Func<double, string> ns =
                t => (t / elems * 1e9).ToString("G3") + " ns";

            string s  = "ArraySize " + ArraySize.ToString("N0")
                      + "   Iterations " + NumIterations + "   Best of " + Repeats + "\n";
            s += "sizeof(EnemyNarrow) " + sizeof(EnemyNarrow)
               +  "   sizeof(EnemyWide) " + sizeof(EnemyWide) + "\n\n";

            s += "A. NARROW entity, all 28 bytes read\n";
            s += "   AoS struct[]  " + aAoS.ToString("G4") + "  " + ns(aAoS) + "\n";
            s += "   SoA arrays    " + aSoA.ToString("G4") + "  " + ns(aSoA)
               +  "   " + (aAoS / aSoA).ToString("G3") + "x\n\n";

            s += "B. WIDE entity, 12 of 128 bytes read\n";
            s += "   AoS struct[]  " + bAoS.ToString("G4") + "  " + ns(bAoS) + "\n";
            s += "   SoA arrays    " + bSoA.ToString("G4") + "  " + ns(bSoA)
               +  "   " + (bAoS / bSoA).ToString("G3") + "x\n\n";

            s += "B'. WIDE entity, 28 of 128 bytes touched\n";
            s += "   AoS struct[]  " + cAoS.ToString("G4") + "  " + ns(cAoS) + "\n";
            s += "   SoA arrays    " + cSoA.ToString("G4") + "  " + ns(cSoA)
               +  "   " + (cAoS / cSoA).ToString("G3") + "x\n\n";

            s += "GC delta across timed section: " + heapDelta + " bytes"
               + (heapDelta == 0 ? "  (clean)" : "  <-- SOMETHING ALLOCATED") + "\n";

            Checksum = m_sink;
            ResultText.text = s;
            Debug.Log(s);
        }
    }
}