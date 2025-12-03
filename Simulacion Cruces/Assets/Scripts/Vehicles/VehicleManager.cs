using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EntryConfig
{
    public string entryId;            // E1..E5
    [Range(0f, 1f)]
    public float probability;        // Probabilidad de elegir esta entrada
}

[System.Serializable]
public class ExitConfig
{
    public string exitId;            // S1..S6
    [Range(0f, 1f)]
    public float probability;        // Probabilidad base de esa salida
}

public class VehicleManager : MonoBehaviour
{
    public static VehicleManager Instance { get; private set; }

    [Header("Spawn global")]
    public bool autoSpawn = true;
    public float minSpawnInterval = 2f;   // segundos
    public float maxSpawnInterval = 5f;

    [Header("Límites de vehículos")]
    public int maxActiveVehicles = 50;
    public int minActiveVehicles = 0;     // por ahora solo informativo

    [SerializeField, Tooltip("Solo lectura, vehículos actuales")]
    private int currentActiveVehicles = 0;

    [Header("Multiplicadores globales de velocidad")]
    public float globalSpeedMultiplier = 1f;
    public float globalRotationMultiplier = 1f;

    [Header("Probabilidades de ENTRADA (E1..E5)")]
    public EntryConfig[] entries;

    [Header("Probabilidades de SALIDA (S1..S6)")]
    public ExitConfig[] exits;

    [Header("Spawners por entrada")]
    public VehicleSpawner[] spawners;

    // Diccionarios de acceso rápido
    private Dictionary<string, EntryConfig> entryById;
    private Dictionary<string, ExitConfig> exitById;
    private Dictionary<string, VehicleSpawner> spawnerByEntryId;

    // Rutas por par (entrada, salida)
    private Dictionary<string, List<string[]>> routesByEntryExit;
    // Salidas alcanzables por entrada (para normalizar probabilidades)
    private Dictionary<string, List<string>> exitsByEntry;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildConfigs();
        BuildRouteIndex();
    }

    private void Start()
    {
        if (autoSpawn)
        {
            StartCoroutine(SpawnLoop());
        }
    }

    // =========================================================
    //   API para VehicleController
    // =========================================================

    public void RegisterVehicle(VehicleController vc)
    {
        currentActiveVehicles++;
    }

    public void UnregisterVehicle(VehicleController vc)
    {
        currentActiveVehicles = Mathf.Max(0, currentActiveVehicles - 1);
    }

    // =========================================================
    //   Configs
    // =========================================================

    private void BuildConfigs()
    {
        entryById = new Dictionary<string, EntryConfig>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.entryId)) continue;
            if (!entryById.ContainsKey(e.entryId))
                entryById.Add(e.entryId, e);
        }

        exitById = new Dictionary<string, ExitConfig>();
        foreach (var s in exits)
        {
            if (s == null || string.IsNullOrEmpty(s.exitId)) continue;
            if (!exitById.ContainsKey(s.exitId))
                exitById.Add(s.exitId, s);
        }

        spawnerByEntryId = new Dictionary<string, VehicleSpawner>();
        foreach (var sp in spawners)
        {
            if (sp == null || string.IsNullOrEmpty(sp.entryId)) continue;
            if (!spawnerByEntryId.ContainsKey(sp.entryId))
                spawnerByEntryId.Add(sp.entryId, sp);
        }
    }

    private string MakeKey(string entryId, string exitId)
        => entryId + "->" + exitId;

    private void BuildRouteIndex()
    {
        routesByEntryExit = new Dictionary<string, List<string[]>>();
        exitsByEntry = new Dictionary<string, List<string>>();

        AddEntryRoutes("E1", TrafficRoutes.FromE1);
        AddEntryRoutes("E2", TrafficRoutes.FromE2);
        AddEntryRoutes("E3", TrafficRoutes.FromE3);
        AddEntryRoutes("E4", TrafficRoutes.FromE4);
        AddEntryRoutes("E5", TrafficRoutes.FromE5);
    }

    private void AddEntryRoutes(string entryId, string[][] routes)
    {
        if (routes == null) return;

        foreach (var r in routes)
        {
            if (r == null || r.Length == 0) continue;

            string exitId = r[r.Length - 1];   // último punto es S1..S6
            string key = MakeKey(entryId, exitId);

            if (!routesByEntryExit.TryGetValue(key, out var list))
            {
                list = new List<string[]>();
                routesByEntryExit[key] = list;
            }
            list.Add(r);

            if (!exitsByEntry.TryGetValue(entryId, out var exitList))
            {
                exitList = new List<string>();
                exitsByEntry[entryId] = exitList;
            }
            if (!exitList.Contains(exitId))
            {
                exitList.Add(exitId);
            }
        }
    }

    // =========================================================
    //   Spawning loop
    // =========================================================

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (currentActiveVehicles < maxActiveVehicles)
            {
                SpawnOneVehicle();
            }

            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(wait);
        }
    }

    private void SpawnOneVehicle()
    {
        // 1. Elegir entrada según probabilidades E1..E5
        string entryId = SampleEntryId();
        if (string.IsNullOrEmpty(entryId))
            return;

        // 2. Elegir salida según probabilidades S1..S6 (solo salidas alcanzables desde esa entrada)
        string exitId = SampleExitForEntry(entryId);
        if (string.IsNullOrEmpty(exitId))
            return;

        // 3. Elegir ruta concreta para ese par entrada-salida
        if (!TryChooseRoute(entryId, exitId, out string[] routeIds))
            return;

        // 4. Convertir IDs -> Transforms
        Transform[] routePoints = WaypointManager.Instance.GetRouteFromIds(routeIds);
        if (routePoints == null || routePoints.Length == 0 || routePoints[0] == null)
        {
            Debug.LogError($"VehicleManager: ruta inválida para {entryId}->{exitId}");
            return;
        }

        // 5. Buscar spawner de esa entrada
        if (!spawnerByEntryId.TryGetValue(entryId, out var spawner) || spawner == null)
        {
            Debug.LogError($"VehicleManager: no hay spawner configurado para entrada '{entryId}'.");
            return;
        }

        // 6. Spawnear vehículo
        spawner.SpawnVehicle(routePoints);
    }

    // =========================================================
    //   Selección por probabilidad
    // =========================================================

    private string SampleEntryId()
    {
        if (entries == null || entries.Length == 0)
            return null;

        float total = 0f;
        foreach (var e in entries)
        {
            if (e == null) continue;
            total += Mathf.Max(e.probability, 0f);
        }

        if (total <= 0f)
            return entries[0].entryId;

        float r = Random.value * total;

        foreach (var e in entries)
        {
            if (e == null) continue;
            float w = Mathf.Max(e.probability, 0f);
            if (r < w)
                return e.entryId;
            r -= w;
        }

        return entries[entries.Length - 1].entryId;
    }

    private string SampleExitForEntry(string entryId)
    {
        if (!exitsByEntry.TryGetValue(entryId, out var reachableExits) ||
            reachableExits == null || reachableExits.Count == 0)
        {
            Debug.LogError($"VehicleManager: la entrada '{entryId}' no tiene salidas alcanzables.");
            return null;
        }

        float total = 0f;

        foreach (var exitId in reachableExits)
        {
            if (!exitById.TryGetValue(exitId, out var cfg)) continue;
            total += Mathf.Max(cfg.probability, 0f);
        }

        if (total <= 0f)
        {
            // Si algo raro pasa, elegir uniforme entre las alcanzables
            int idx = Random.Range(0, reachableExits.Count);
            return reachableExits[idx];
        }

        float r = Random.value * total;

        foreach (var exitId in reachableExits)
        {
            if (!exitById.TryGetValue(exitId, out var cfg)) continue;

            float w = Mathf.Max(cfg.probability, 0f);
            if (r < w)
                return exitId;

            r -= w;
        }

        return reachableExits[reachableExits.Count - 1];
    }

    private bool TryChooseRoute(string entryId, string exitId, out string[] routeIds)
    {
        string key = MakeKey(entryId, exitId);
        if (routesByEntryExit.TryGetValue(key, out var list) && list != null && list.Count > 0)
        {
            int idx = Random.Range(0, list.Count);
            routeIds = list[idx];
            return true;
        }

        routeIds = null;
        return false;
    }
}
