using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class HLPR : MonoBehaviour
{
    public static HLPR instance;

    [SerializeField] private RectTransform m_Cursor;
    
    // ---- 사용자 튜닝 파라미터 ----
    [Header("Thresholds")]
    [SerializeField] private float m_CurlEnterThreshold = 0.70f; // 쥠 진입
    [SerializeField] private float m_CurlExitThreshold = 0.80f; // 쥠 이탈
    [SerializeField] private int m_EnterNeededCount = 3;     // 4지(검중약소) 중 최소 몇 개가 말려야 쥠으로 볼지
    [SerializeField] private int m_ExitNeededCount = 2;     // 펴짐 판정 완화

    [Header("Thumb Assist")]
    [SerializeField] private bool m_UseThumbAssist = true;
    [SerializeField] private float m_ThumbNearPalmK = 0.85f; // |TIP4-palm| < k*|MCP5-palm|

    [Header("Smoothing")]
    [SerializeField] private float m_EmaAlpha = 0.35f; // 0(느림)~1(빠름)
    [SerializeField] private int m_MinStableFrames = 2;     // 동일 판정 최소 지속 프레임

    // ---- 내부 상태 ----
    private float m_EmaIndex, m_EmaMiddle, m_EmaRing, m_EmaPinky;
    private int m_StableCounter = 0;
    private bool m_CurrentIsFist = false;

    private void OnEnable()
    {
        HandResultBus.OnHandResult += OnHandResult;
        HandLandmarkPointBus.OnHandResult += OnPalmPoint;
    }
    private void OnDisable()
    {
        HandResultBus.OnHandResult -= OnHandResult;
        HandLandmarkPointBus.OnHandResult -= OnPalmPoint;
    }

    private void OnPalmPoint(List<Transform> r)
    {
        Vector3 pos = Vector3.zero;
        foreach (Transform t in r)
        {
            // 일단 임시로 이렇게만
            if (!t.gameObject.activeInHierarchy)
            {
                m_Cursor.gameObject.SetActive(false);
            }
            pos += t.position;
        }

        Debug.Log(pos / 5f);
        m_Cursor.gameObject.SetActive(true);
        m_Cursor.position = pos / 5f;
    }
    private void OnHandResult(HandLandmarkerResult r)
    {
        if (r.handLandmarks == null) return;

        foreach (var item in r.handLandmarks)
        {
            int i = r.handLandmarks.IndexOf(item);
            //foreach (var item2 in item.landmarks)
            //{
            //    Debug.Log($"{i} : {item2.x},{item2.y},{item2.z}");
            //}
            UpdateAndIsFist(item.landmarks);
        }
    }

    /// <summary>
    /// 외부에서 매 프레임 호출: 21개 landmarks 전달
    /// </summary>
    public bool UpdateAndIsFist(List<NormalizedLandmark> _lm)
    {
        if (_lm == null || _lm.Count < 21)
            return false;

        // NormalizedLandmark → Vector3 변환
        Vector3[] pts = _lm.Select(l => new Vector3(l.x, l.y, l.z)).ToArray();

        // 1) 손바닥 중심
        Vector3 palm = Average(
            pts[0], pts[5], pts[9], pts[13], pts[17]
        );

        // 2) 각 손가락 ratio 산출
        float idxRatio = Ratio(pts[8], pts[5], palm);
        float midRatio = Ratio(pts[12], pts[9], palm);
        float ringRatio = Ratio(pts[16], pts[13], palm);
        float pkyRatio = Ratio(pts[20], pts[17], palm);

        // 3) EMA 스무딩
        m_EmaIndex = Ema(m_EmaIndex, idxRatio, m_EmaAlpha);
        m_EmaMiddle = Ema(m_EmaMiddle, midRatio, m_EmaAlpha);
        m_EmaRing = Ema(m_EmaRing, ringRatio, m_EmaAlpha);
        m_EmaPinky = Ema(m_EmaPinky, pkyRatio, m_EmaAlpha);

        // 4) 카운트
        int curledEnterCount = CountBelow(m_CurlEnterThreshold, m_EmaIndex, m_EmaMiddle, m_EmaRing, m_EmaPinky);
        int extendedExitCount = CountAbove(m_CurlExitThreshold, m_EmaIndex, m_EmaMiddle, m_EmaRing, m_EmaPinky);

        bool wantFist = false;

        // 기본 판정
        if (!m_CurrentIsFist)
        {
            if (curledEnterCount >= m_EnterNeededCount)
                wantFist = true;
        }
        else
        {
            if (extendedExitCount >= m_ExitNeededCount)
                wantFist = false;
            else
                wantFist = true;
        }

        // 엄지 보조
        if (m_UseThumbAssist)
        {
            float thumbTipPalm = (pts[4] - palm).magnitude;
            float refLen = (pts[5] - palm).magnitude + 1e-6f;
            bool thumbNearPalm = thumbTipPalm < m_ThumbNearPalmK * refLen;

            if (!m_CurrentIsFist && wantFist)
            {
                if (!thumbNearPalm) wantFist = false;
            }
            else if (m_CurrentIsFist && !wantFist)
            {
                // if (thumbNearPalm) wantFist = true;
            }
        }

        // 5) 안정 프레임 보장
        if (wantFist == m_CurrentIsFist)
        {
            m_StableCounter = 0;
        }
        else
        {
            m_StableCounter++;
            if (m_StableCounter >= m_MinStableFrames)
            {
                m_CurrentIsFist = wantFist;
                m_StableCounter = 0;

                // ✅ 상태 변화가 확정된 순간만 출력
                Debug.Log(m_CurrentIsFist ? "손을 쥐었습니다." : "손을 폈습니다.");
            }
        }

        //Debug.Log($"{m_Cursor.position}, {palm}");
        return m_CurrentIsFist;
    }

    // --- 유틸 ---
    private static Vector3 Average(params Vector3[] pts)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < pts.Length; i++) sum += pts[i];
        return sum / Mathf.Max(1, pts.Length);
    }

    private static float Ratio(in Vector3 tip, in Vector3 mcp, in Vector3 palm)
    {
        float num = (tip - palm).magnitude;
        float den = (mcp - palm).magnitude + 1e-6f;
        return num / den;
    }

    private static float Ema(float prev, float x, float a) => (1f - a) * prev + a * x;

    private static int CountBelow(float th, params float[] vals) => vals.Count(v => v < th);
    private static int CountAbove(float th, params float[] vals) => vals.Count(v => v > th);
}
