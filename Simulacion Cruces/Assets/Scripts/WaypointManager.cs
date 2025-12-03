using System.Collections.Generic;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager Instance { get; private set; }

    private Dictionary<string, Transform> waypoints = new Dictionary<string, Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildDictionary();
    }

    private void OnValidate()
    {
        // Se actualiza el diccionario cuando cambias cosas en el editor
        if (Application.isPlaying) return;
        BuildDictionary();
    }

    private void BuildDictionary()
    {
        waypoints.Clear();

        // Busca TODOS los Waypoint de la escena (aunque estén inactivos)
        Waypoint[] all = FindObjectsOfType<Waypoint>(true);

        foreach (var wp in all)
        {
            if (string.IsNullOrEmpty(wp.id))
                continue;

            if (waypoints.ContainsKey(wp.id))
            {
                Debug.LogWarning($"WaypointManager: ID duplicado '{wp.id}' en {wp.name}");
                continue;
            }

            waypoints.Add(wp.id, wp.transform);
        }
    }

    public Transform Get(string id)
    {
        if (waypoints.TryGetValue(id, out Transform t))
            return t;

        Debug.LogError($"WaypointManager: no se encontró waypoint con id '{id}'");
        return null;
    }

    // Convierte ["E1","P2","P3"] -> array de Transforms
    public Transform[] GetRouteFromIds(string[] ids)
    {
        Transform[] result = new Transform[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            result[i] = Get(ids[i]);
        }
        return result;
    }
}
