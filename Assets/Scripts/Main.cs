using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using System.Data;
using System.Runtime.InteropServices;

namespace CHAPTER2
{
    public struct Padding
    {
        public Vector4 bar1;
        public Vector4 bar2;
        public Vector4 bar3;
        public Vector4 bar4;
    }

    // [StructLayout(LayoutKind.Sequential)]
    public class EnemyOOP
    {
        public Vector3 m_position;
        public Padding padding1;
        public Vector3 m_direction;
        public Padding padding2;
        public float m_velocity;
        public Padding padding3;

        public EnemyOOP()
        {
            m_position = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            m_direction = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            m_velocity = UnityEngine.Random.value;
        }

        public void Move()
        {
            m_position += m_direction * m_velocity;
        }
    }

    // [StructLayout(LayoutKind.Sequential)]
    public class EnemyOOP_DataLocality
    {
        public Vector3 m_position;
        public Vector3 m_direction;
        public float m_velocity;

        public Padding padding1;
        public Padding padding2;
        public Padding padding3;

        public EnemyOOP_DataLocality()
        {
            m_position = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            m_direction = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            m_velocity = UnityEngine.Random.value;
        }

        public void Move()
        {
            m_position += m_direction * m_velocity;
        }
    }

    // [StructLayout(LayoutKind.Sequential)]
    public class EnemyMove
    {
        public Vector3 m_position;
        public Vector3 m_direction;
        public float m_velocity;

        public EnemyMove()
        {
            m_position = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            m_direction = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            m_velocity = UnityEngine.Random.value;
        }

        public void Move()
        {
            m_position += m_direction * m_velocity;
        }
    }

    public class EnemyData
    {
        public Vector3[] Position;
        public Vector3[] Direction;
        public float[] Velocity;
    }

    public class Main : MonoBehaviour
    {
        public TextMeshProUGUI ResultText;

        EnemyOOP[] m_enemy1Pool;
        EnemyOOP_DataLocality[] m_enemy2Pool;
        EnemyMove[] m_enemyMovePool;

        EnemyData m_enemyData = new EnemyData();

        public int NumIterations = 10000;
        public int ArraySize = 10000;

        private void Awake()
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector4));
            Debug.Log("Vector4 " + size);

            ResultText.text = "Ready\n";

            m_enemy1Pool = new EnemyOOP[ArraySize];
            for (int i = 0; i < m_enemy1Pool.Length; i++)
                m_enemy1Pool[i] = new EnemyOOP();

            m_enemy2Pool = new EnemyOOP_DataLocality[ArraySize];
            for (int i = 0; i < m_enemy2Pool.Length; i++)
                m_enemy2Pool[i] = new EnemyOOP_DataLocality();

            m_enemyMovePool = new EnemyMove[ArraySize];
            for (int i = 0; i < m_enemyMovePool.Length; i++)
                m_enemyMovePool[i] = new EnemyMove();

            m_enemyData.Position = new Vector3[ArraySize];
            m_enemyData.Direction = new Vector3[ArraySize];
            m_enemyData.Velocity = new float[ArraySize];
            for (int i = 0; i < ArraySize; i++)
            {
                m_enemyData.Position[i] = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                m_enemyData.Direction[i] = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                m_enemyData.Velocity[i] = UnityEngine.Random.value;
            }
        }

        private void Start()
        {
            ResultText.text = "Ready to run\n";
        }

        public unsafe void RunTest()
        {
            double time1 = 0d;
            double time2 = 0d;
            double time3 = 0d;
            double time4 = 0d;

            for (int i = 0; i < NumIterations; i++)
            {
                double time = Time.realtimeSinceStartupAsDouble;
                for (int enemyIdx = 0; enemyIdx < m_enemy1Pool.Length; enemyIdx++)
                    m_enemy1Pool[enemyIdx].Move();
                time1 += Time.realtimeSinceStartupAsDouble - time;

                time = Time.realtimeSinceStartupAsDouble;
                for (int enemyIdx = 0; enemyIdx < m_enemy2Pool.Length; enemyIdx++)
                    m_enemy2Pool[enemyIdx].Move();
                time2 += Time.realtimeSinceStartupAsDouble - time;

                time = Time.realtimeSinceStartupAsDouble;
                for (int enemyIdx = 0; enemyIdx < m_enemyMovePool.Length; enemyIdx++)
                    m_enemyMovePool[enemyIdx].Move();
                time3 += Time.realtimeSinceStartupAsDouble - time;

                time = Time.realtimeSinceStartupAsDouble;
                for (int enemyIdx = 0; enemyIdx < ArraySize; enemyIdx++)
                    m_enemyData.Position[enemyIdx] += m_enemyData.Direction[enemyIdx] * m_enemyData.Velocity[enemyIdx];
                time4 += Time.realtimeSinceStartupAsDouble - time;
            }

            string s = "NumIterations " + NumIterations.ToString("N0") + "\n";
            s += "ArraySize " + ArraySize.ToString("N0") + "\n";
            s += "Padding size " + sizeof(Padding) + "\n";
            s += "Result:\n";
            s += "OOP \t\t\t\t" + time1.ToString("G4") + "\n";
            s += "OOP Data Locality \t" + time2.ToString("G4") + "\t" + (time1 / time2).ToString("G1") + "x\n";
            s += "Optimized OOP \t\t" + time3.ToString("G4") + "\t" + (time1 / time3).ToString("G1") + "x\n";
            s += "Arrays \t\t\t" + time4.ToString("G4") + "\t" + (time1 / time4).ToString("G1") + "x\n";
            ResultText.text = s;
            Debug.Log(s);
        }
    }

}