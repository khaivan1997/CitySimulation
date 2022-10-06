using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;

public static class JobLogger
{
    [BurstDiscard] public static void Log<T>(T segment) => Debug.Log(AppendToString(segment));
    [BurstDiscard] public static void Log<T1, T2>(T1 segment1, T2 segment2) => Debug.Log(AppendToString(segment1, segment2));
    [BurstDiscard] public static void Log<T1, T2, T3>(T1 segment1, T2 segment2, T3 segment3) => Debug.Log(AppendToString(segment1, segment2, segment3));
    [BurstDiscard] public static void Log<T1, T2, T3, T4>(T1 segment1, T2 segment2, T3 segment3, T4 segment4) => Debug.Log(AppendToString(segment1, segment2, segment3, segment4));
    [BurstDiscard] public static void Log<T1, T2, T3, T4, T5>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5) => Debug.Log(AppendToString(segment1, segment2, segment3, segment4, segment5));
    [BurstDiscard] public static void Log<T1, T2, T3, T4, T5, T6>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6) => Debug.Log(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6));
    [BurstDiscard] public static void Log<T1, T2, T3, T4, T5, T6, T7>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6, T7 segment7) => Debug.Log(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6, segment7));
    [BurstDiscard] public static void Log<T1, T2, T3, T4, T5, T6, T7, T8>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6, T7 segment7, T8 segment8) => Debug.Log(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6, segment7, segment8));

    [BurstDiscard] public static void LogWarning<T>(T segment) => Debug.LogWarning(AppendToString(segment));
    [BurstDiscard] public static void LogWarning<T1, T2>(T1 segment1, T2 segment2) => Debug.LogWarning(AppendToString(segment1, segment2));
    [BurstDiscard] public static void LogWarning<T1, T2, T3>(T1 segment1, T2 segment2, T3 segment3) => Debug.LogWarning(AppendToString(segment1, segment2, segment3));
    [BurstDiscard] public static void LogWarning<T1, T2, T3, T4>(T1 segment1, T2 segment2, T3 segment3, T4 segment4) => Debug.LogWarning(AppendToString(segment1, segment2, segment3, segment4));
    [BurstDiscard] public static void LogWarning<T1, T2, T3, T4, T5>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5) => Debug.LogWarning(AppendToString(segment1, segment2, segment3, segment4, segment5));
    [BurstDiscard] public static void LogWarning<T1, T2, T3, T4, T5, T6>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6) => Debug.LogWarning(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6));
    [BurstDiscard] public static void LogWarning<T1, T2, T3, T4, T5, T6, T7>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6, T7 segment7) => Debug.LogWarning(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6, segment7));
    [BurstDiscard] public static void LogWarning<T1, T2, T3, T4, T5, T6, T7, T8>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6, T7 segment7, T8 segment8) => Debug.LogWarning(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6, segment7, segment8));

    [BurstDiscard] public static void LogError<T>(T segment) => Debug.LogError(AppendToString(segment));
    [BurstDiscard] public static void LogError<T1, T2>(T1 segment1, T2 segment2) => Debug.LogError(AppendToString(segment1, segment2));
    [BurstDiscard] public static void LogError<T1, T2, T3>(T1 segment1, T2 segment2, T3 segment3) => Debug.LogError(AppendToString(segment1, segment2, segment3));
    [BurstDiscard] public static void LogError<T1, T2, T3, T4>(T1 segment1, T2 segment2, T3 segment3, T4 segment4) => Debug.LogError(AppendToString(segment1, segment2, segment3, segment4));
    [BurstDiscard] public static void LogError<T1, T2, T3, T4, T5>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5) => Debug.LogError(AppendToString(segment1, segment2, segment3, segment4, segment5));
    [BurstDiscard] public static void LogError<T1, T2, T3, T4, T5, T6>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6) => Debug.LogError(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6));
    [BurstDiscard] public static void LogError<T1, T2, T3, T4, T5, T6, T7>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6, T7 segment7) => Debug.LogError(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6, segment7));
    [BurstDiscard] public static void LogError<T1, T2, T3, T4, T5, T6, T7, T8>(T1 segment1, T2 segment2, T3 segment3, T4 segment4, T5 segment5, T6 segment6, T7 segment7, T8 segment8) => Debug.LogError(AppendToString(segment1, segment2, segment3, segment4, segment5, segment6, segment7, segment8));


    [BurstDiscard]
    public static string AppendToString(params object[] parts)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Clear();
        for (int i = 0, len = parts.Length; i < len; i++) sb.Append(parts[i].ToString()+" ");
        return sb.ToString();
    }
}
