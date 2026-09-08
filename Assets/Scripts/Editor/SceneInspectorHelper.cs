#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArtSimulator.Editor
{
    public static class SceneInspectorHelper
    {
        [MenuItem("Simulador/Diagnosticar Tableta y Pantalla")]
        public static void DiagnoseTablet()
        {
            GameObject tablet = GameObject.Find("Tableta");
            if (tablet == null)
            {
                Debug.LogError("[Diagnóstico] No se encontró el objeto 'Tableta'");
                return;
            }

            Debug.Log($"[Diagnóstico] Tableta encontrada en posición: {tablet.transform.position}, rotación: {tablet.transform.eulerAngles}, escala: {tablet.transform.lossyScale}");

            foreach (Transform child in tablet.GetComponentsInChildren<Transform>())
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                Collider col = child.GetComponent<Collider>();

                string info = $"Hijo: '{child.name}' | LocalPos: {child.localPosition} | LocalRot: {child.localEulerAngles} | LocalScale: {child.localScale}";
                if (mr != null) info += $" | Bounds center: {mr.bounds.center}, size: {mr.bounds.size}";
                if (col != null) info += $" | Collider: {col.GetType().Name} (isTrigger: {col.isTrigger})";
                Debug.Log($"[Diagnóstico] {info}");
            }
        }
    }
}
#endif
