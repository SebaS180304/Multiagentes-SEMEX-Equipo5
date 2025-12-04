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

    [Header("Interacciones entre vehículos")]
    [Tooltip("Distancia mínima que se debe mantener con el coche de adelante.")]
    public float safeHeadwayDistance = 4f;

    [Tooltip("Radio para checar si 'quepo' al avanzar hacia el siguiente punto.")]
    public float mergeCheckRadius = 1.5f;


    [Header("Spawn global")]
    public bool autoSpawn = true;
    public float minSpawnInterval = 2f;   // segundos
    public float maxSpawnInterval = 5f;

    [Header("Límites de vehículos")]
    public int maxActiveVehicles = 50;
    public int minActiveVehicles = 0;     // por ahora solo informativo

    private List<VehicleController> activeVehicles = new List<VehicleController>();

    [SerializeField, Tooltip("Solo lectura, vehículos actuales")]
    private int currentActiveVehicles = 0;
    public int CurrentActiveVehicles => currentActiveVehicles;

    [SerializeField, Tooltip("Solo lectura, vehículos esperando en semáforo (total global)")]
    private int currentWaitingVehicles = 0;
    public int CurrentWaitingVehicles => currentWaitingVehicles;

    // Conteo por semáforo: lightId -> número de coches esperando en ese semáforo
    private Dictionary<string, int> waitingByLight = new Dictionary<string, int>();


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

    public void RegisterVehicle(VehicleController v)
    {
        if (!activeVehicles.Contains(v))
            activeVehicles.Add(v);

        currentActiveVehicles = activeVehicles.Count;
    }

    public void UnregisterVehicle(VehicleController v)
    {
        if (activeVehicles.Remove(v))
            currentActiveVehicles = activeVehicles.Count;
    }

    // ====================================
    //  Método para resetear la simulación
    // ====================================

    public void ResetSimulation()
    {
        // Destruir todos los coches actuales
        var copy = new List<VehicleController>(activeVehicles);
        foreach (var v in copy)
        {
            if (v != null)
                Destroy(v.gameObject);
        }

        activeVehicles.Clear();
        currentActiveVehicles = 0;
        currentWaitingVehicles = 0;
        waitingByLight.Clear();
    }


    // =======================
    //  Espera en semáforos
    // =======================

    /// <summary>
    /// Registra que un vehículo empezó a esperar en el semáforo con ese ID.
    /// </summary>
    public void RegisterWaitingVehicle(string lightId)
    {
        currentWaitingVehicles++;

        if (string.IsNullOrEmpty(lightId))
            return;

        if (!waitingByLight.TryGetValue(lightId, out int count))
            count = 0;

        waitingByLight[lightId] = count + 1;
    }

    /// <summary>
    /// Registra que un vehículo dejó de esperar en el semáforo con ese ID.
    /// </summary>
    public void UnregisterWaitingVehicle(string lightId)
    {
        currentWaitingVehicles = Mathf.Max(0, currentWaitingVehicles - 1);

        if (string.IsNullOrEmpty(lightId))
            return;

        if (waitingByLight.TryGetValue(lightId, out int count))
        {
            count = Mathf.Max(0, count - 1);
            if (count == 0)
            {
                waitingByLight.Remove(lightId);
            }
            else
            {
                waitingByLight[lightId] = count;
            }
        }
    }

    

    /// <summary>
    /// Número de vehículos esperando en un semáforo específico.
    /// </summary>
    public int GetWaitingVehiclesForLight(string lightId)
    {
        if (string.IsNullOrEmpty(lightId))
            return 0;

        int count = 0;

        foreach (var v in activeVehicles)
        {
            if (v == null) 
                continue;

            if (v.IsWaiting && v.WaitingLightId == lightId)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Suma de vehículos esperando en una colección de semáforos.
    /// </summary>
    public int GetWaitingVehiclesForLights(IReadOnlyList<TrafficLight> lights)
    {
        if (lights == null) return 0;

        int total = 0;
        foreach (var tl in lights)
        {
            if (tl == null) continue;
            total += GetWaitingVehiclesForLight(tl.lightId);
        }
        return total;
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
