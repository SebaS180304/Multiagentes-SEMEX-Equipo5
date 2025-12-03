using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TrafficLightManager : MonoBehaviour
{
    // ==== REGISTROS ESTÁTICOS (GLOBAL) ====

    // Managers por ID de Q-learning (MAIN, ROUND, etc.)
    private static Dictionary<string, TrafficLightManager> managersById =
        new Dictionary<string, TrafficLightManager>();

    // Semáforos por ID de luz (TL1, TL2, etc.)
    private static Dictionary<string, TrafficLight> lightsGlobal =
        new Dictionary<string, TrafficLight>();

    [Header("Persistencia")]
    [Tooltip("Cargar Q-table al iniciar")]
    public bool loadOnStart = true;

    [Tooltip("Guardar Q-table al cerrarse")]
    public bool saveOnQuit = true;


    // ======== CLASE PARA GUARDAR Q-TABLE ========

    [System.Serializable]
    private class QLearningSaveData
    {
        public string qLearningId;
        public int numStates;
        public int numActions;
        public float[] values;
    }


    public static TrafficLightManager GetManagerById(string qLearningId)
    {
        if (string.IsNullOrEmpty(qLearningId))
            return null;

        managersById.TryGetValue(qLearningId, out var mgr);
        return mgr;
    }

    public static void RegisterLightGlobal(TrafficLight tl)
    {
        if (tl == null || string.IsNullOrEmpty(tl.lightId))
            return;

        lightsGlobal[tl.lightId] = tl;
    }

    public static void UnregisterLightGlobal(TrafficLight tl)
    {
        if (tl == null || string.IsNullOrEmpty(tl.lightId))
            return;

        if (lightsGlobal.TryGetValue(tl.lightId, out var existing) && existing == tl)
        {
            lightsGlobal.Remove(tl.lightId);
        }
    }

    public static TrafficLightState GetLightStateGlobal(string lightId)
    {
        if (string.IsNullOrEmpty(lightId))
            return TrafficLightState.Green; // si no hay id, consideramos que pasa

        if (lightsGlobal.TryGetValue(lightId, out var tl) && tl != null)
            return tl.CurrentState;

        return TrafficLightState.Green;
    }

    // ==== CONFIG DE ESTE MANAGER (INSTANCIA) ====

    [Header("ID de este controlador/Q-learning")]
    [Tooltip("Ejemplos: MAIN, ROUND. Debe coincidir con qLearningId de los semáforos que va a controlar.")]
    public string qLearningId = "MAIN";

    [Header("Control de fases")]
    [Tooltip("Número de combinaciones de luces (fases) que este manager puede elegir.")]
    public int numPhases = 2; // fases 0..numPhases-1

    [Header("Duraciones posibles (segundos) para Q-Learning")]
    [Tooltip("Cada acción será una combinación (fase, duración).")]
    public float[] durationOptions = new float[] { 8f, 12f };

    [Header("Q-Learning (por grupo)")]
    [Range(0f, 1f)] public float alpha = 0.1f;
    [Range(0f, 1f)] public float gamma = 0.9f;
    [Range(0f, 1f)] public float epsilon = 0.1f;

    [Header("Bucle de control")]
    public bool autoControl = true;
    public float initialDelay = 1f;

    [SerializeField, Tooltip("Q-values del estado actual (solo lectura).")]
    private float[] qDebug;  // Q[state=0, action]
    public IReadOnlyList<float> QDebug => qDebug;

    private float[,] qTable;   // Q[state, action]
    private int currentState = 0;
    private int lastAction = -1;

    // número total de acciones = numPhases * durationOptions.Length
    private int numActions = 0;

    // Semáforos que este manager controla (los que tienen el mismo qLearningId)
    private List<TrafficLight> lightsLocal = new List<TrafficLight>();

    // =========================================================
    //   Ciclo de vida del manager
    // =========================================================
    private void Awake()
    {
        // Registrar este manager por su qLearningId
        if (!string.IsNullOrEmpty(qLearningId))
        {
            if (managersById.ContainsKey(qLearningId))
            {
                managersById[qLearningId] = this;
            }
            else
            {
                managersById.Add(qLearningId, this);
            }
        }

        EnsureDurationOptions();
        InitializeQTable();

        // 🔧 FIX: enganchar todos los TrafficLight existentes con este qLearningId
        AttachExistingLights();

        if (loadOnStart)
            LoadQTable();
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(qLearningId) &&
            managersById.TryGetValue(qLearningId, out var mgr) &&
            mgr == this)
        {
            managersById.Remove(qLearningId);
        }

        if (saveOnQuit)
            SaveQTable();
    }

    private void EnsureDurationOptions()
    {
        if (durationOptions == null || durationOptions.Length == 0)
        {
            durationOptions = new float[1];
            durationOptions[0] = 10f; // default
        }
    }

    private void InitializeQTable()
    {
        if (numPhases <= 0) numPhases = 1;

        int numStates = 1;   // un estado por ahora
        int numDurations = Mathf.Max(1, durationOptions.Length);

        numActions = numPhases * numDurations;
        qTable = new float[numStates, numActions];
        qDebug = new float[numActions];
    }

    /// <summary>
    /// Busca todos los TrafficLight de la escena cuyo qLearningId coincide
    /// con el de este manager y los registra en lightsLocal.
    /// </summary>
    private void AttachExistingLights()
    {
        var allLights = FindObjectsOfType<TrafficLight>(true);
        foreach (var tl in allLights)
        {
            if (tl == null) continue;
            if (tl.qLearningId == qLearningId)
            {
                RegisterLightLocal(tl);
            }
        }
    }

    private void Start()
    {
        if (autoControl)
            StartCoroutine(ControlLoop());
    }

    // =========================================================
    //   Registro local de semáforos (de este manager)
    // =========================================================
    public void RegisterLightLocal(TrafficLight tl)
    {
        if (tl == null) return;

        if (!lightsLocal.Contains(tl))
            lightsLocal.Add(tl);
    }

    public void UnregisterLightLocal(TrafficLight tl)
    {
        if (tl == null) return;
        lightsLocal.Remove(tl);
    }

    // =========================================================
    //   Bucle de control + Q-Learning
    // =========================================================
    private IEnumerator ControlLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float waitTime = StepLearningAndSetAction();
            if (waitTime <= 0f) waitTime = 1f; // por seguridad

            yield return new WaitForSeconds(waitTime);
        }
    }

    /// <summary>
    /// Actualiza Q de la acción previa, elige una nueva acción (fase+duración),
    /// aplica la fase y devuelve el tiempo que debe durar.
    /// </summary>
    private float StepLearningAndSetAction()
    {
        int s = currentState;

        // 1. Actualizar Q de la acción anterior (si hubo)
        if (lastAction != -1)
        {
            float reward = ComputeReward();
            UpdateQ(s, lastAction, reward, s);
        }

        // 2. Elegir nueva acción con epsilon-greedy (fase + duración)
        int a = SelectActionEpsilonGreedy(s);

        // 3. Decodificar acción -> fase y duración
        DecodeAction(a, out int phaseIndex, out float chosenDuration);

        // 4. Aplicar fase SOLO a los semáforos locales
        ApplyPhase(phaseIndex);

        // 5. Guardar acción y actualizar vector de debug
        lastAction = a;
        for (int i = 0; i < numActions; i++)
        {
            qDebug[i] = qTable[s, i];
        }

        // 6. Devolver duración elegida
        return chosenDuration;
    }


    // Reward simple: minimizar vehículos activos (globales)
    [Header("Recompensa")]
    [Tooltip("Peso extra para penalizar vehículos esperando en semáforo.")]
    public float waitingWeight = 2f;

    private float ComputeReward()
    {
        if (VehicleManager.Instance != null)
        {
            float total = VehicleManager.Instance.CurrentActiveVehicles;
            float waiting = VehicleManager.Instance.CurrentWaitingVehicles;

            // Mientras más coches haya, peor. Mientras más esperando, MUCHO peor.
            float cost = total + waitingWeight * waiting;

            return -cost;
        }
        return 0f;
    }


    private int SelectActionEpsilonGreedy(int state)
    {
        if (numActions <= 0)
            return 0;

        if (Random.value < epsilon)
        {
            // Exploración
            return Random.Range(0, numActions);
        }

        // Explotación
        float bestQ = float.NegativeInfinity;
        int bestAction = 0;

        for (int a = 0; a < numActions; a++)
        {
            float q = qTable[state, a];
            if (q > bestQ)
            {
                bestQ = q;
                bestAction = a;
            }
        }

        return bestAction;
    }

    private void UpdateQ(int s, int a, float reward, int nextState)
    {
        float maxNextQ = float.NegativeInfinity;
        for (int ap = 0; ap < numActions; ap++)
        {
            float q = qTable[nextState, ap];
            if (q > maxNextQ)
                maxNextQ = q;
        }

        float oldQ = qTable[s, a];
        float newQ = (1f - alpha) * oldQ + alpha * (reward + gamma * maxNextQ);
        qTable[s, a] = newQ;
    }

    /// <summary>
    /// Convierte un índice de acción en (fase, duración).
    /// </summary>
    private void DecodeAction(int actionIndex, out int phaseIndex, out float duration)
    {
        int numDurations = Mathf.Max(1, durationOptions.Length);
        if (numPhases <= 0) numPhases = 1;

        // Ejemplo: numPhases = 2, numDurations = 3
        // acciones 0..5:
        //   0 -> fase 0, dur 0
        //   1 -> fase 1, dur 0
        //   2 -> fase 0, dur 1
        //   3 -> fase 1, dur 1
        //   4 -> fase 0, dur 2
        //   5 -> fase 1, dur 2

        phaseIndex = actionIndex % numPhases;
        int durIndex = actionIndex / numPhases;
        durIndex = Mathf.Clamp(durIndex, 0, numDurations - 1);

        duration = durationOptions[durIndex];
    }

    private void ApplyPhase(int phaseIndex)
    {
        foreach (var tl in lightsLocal)
        {
            if (tl == null) continue;
            tl.SetPhase(phaseIndex);
        }
    }

    // ======== PERSISTENCIA Q-TABLE ========

    private string GetSavePath()
    {
        // Carpeta dentro de Assets donde vamos a guardar los JSON
        string dir = Path.Combine(Application.dataPath, "QLearningData");

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string fileName = string.IsNullOrEmpty(qLearningId)
            ? "qlearning_default.json"
            : $"qlearning_{qLearningId}.json";

        return Path.Combine(dir, fileName);
    }


    private void SaveQTable()
    {
        if (qTable == null)
            return;

        int numStates = qTable.GetLength(0);
        int numActs = qTable.GetLength(1);

        var data = new QLearningSaveData
        {
            qLearningId = qLearningId,
            numStates = numStates,
            numActions = numActs,
            values = new float[numStates * numActs]
        };

        int idx = 0;
        for (int s = 0; s < numStates; s++)
        {
            for (int a = 0; a < numActs; a++)
            {
                data.values[idx++] = qTable[s, a];
            }
        }

        string json = JsonUtility.ToJson(data, true);
        string path = GetSavePath();

        try
        {
            File.WriteAllText(path, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error guardando Q-table ({qLearningId}): {e}");
        }
    }

    private void LoadQTable()
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<QLearningSaveData>(json);
            if (data == null || data.values == null)
                return;

            int numStates = qTable.GetLength(0);
            int numActs = qTable.GetLength(1);

            if (data.numStates != numStates || data.numActions != numActs)
            {
                Debug.LogWarning($"Q-table guardada para '{qLearningId}' no coincide en tamaño. Ignorando archivo.");
                return;
            }

            int idx = 0;
            for (int s = 0; s < numStates; s++)
            {
                for (int a = 0; a < numActs; a++)
                {
                    qTable[s, a] = data.values[idx++];
                }
            }

            // Actualizar qDebug para ver los valores en el inspector
            for (int a = 0; a < numActs; a++)
            {
                qDebug[a] = qTable[0, a];
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error cargando Q-table ({qLearningId}): {e}");
        }
    }
}
