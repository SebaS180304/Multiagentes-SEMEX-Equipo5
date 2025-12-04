using UnityEngine;

public enum TrafficLightState
{
    Red,
    Yellow,
    Green
}

public class TrafficLight : MonoBehaviour
{
    [Tooltip("ID único del semáforo (ej. TL1). Debe coincidir con los Waypoints que controla.")]
    public string lightId;

    [Tooltip("ID del controlador/Q-learning al que pertenece este semáforo (ej. MAIN, ROUND).")]
    public string qLearningId = "MAIN";

    [Header("Renderers de cada foco (hijos R, Y, G)")]
    public Renderer redRenderer;     // hijo "R"
    public Renderer yellowRenderer;  // hijo "Y"
    public Renderer greenRenderer;   // hijo "G"

    [Header("Materiales")]
    public Material redMaterial;
    public Material yellowMaterial;
    public Material greenMaterial;
    public Material offMaterial;

    [Header("Fases (para Q-Learning)")]
    [Tooltip("En qué fases este semáforo está en verde. Debe tener el mismo tamaño que numPhases en el TrafficLightManager correspondiente.")]
    public bool[] greenInPhase;

    [Header("Tiempos de transición")]
    [Tooltip("Tiempo que espera al pasar de ROJO a VERDE (todos rojos).")]
    public float redToGreenDelay = 1.5f;

    [Tooltip("Duración del AMARILLO antes de pasar de VERDE a ROJO.")]
    public float yellowBeforeRedTime = 0.5f;

    [SerializeField, Tooltip("Estado actual (solo lectura)")]
    private TrafficLightState currentState = TrafficLightState.Red;
    public TrafficLightState CurrentState => currentState;

    [Header("Debug cola")]
    [SerializeField]
    private int debugQueueCount;
    public int DebugQueueCount => debugQueueCount;


    // Corrutina que maneja las transiciones
    private Coroutine transitionRoutine;

    private void OnEnable()
    {
        // Registro GLOBAL (para que los coches puedan preguntar el estado por lightId)
        TrafficLightManager.RegisterLightGlobal(this);

        // Registro LOCAL en el manager que tenga este qLearningId (si ya existe)
        var manager = TrafficLightManager.GetManagerById(qLearningId);
        if (manager != null)
        {
            manager.RegisterLightLocal(this);
        }

        UpdateVisuals();
    }

    private void OnDisable()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        TrafficLightManager.UnregisterLightGlobal(this);

        var manager = TrafficLightManager.GetManagerById(qLearningId);
        if (manager != null)
        {
            manager.UnregisterLightLocal(this);
        }
    }

    public void SetState(TrafficLightState newState)
    {
        currentState = newState;
        UpdateVisuals();
    }

    // El manager llama esto cuando cambia de fase
    public void SetPhase(int phaseIndex)
    {
        bool green = false;

        if (greenInPhase != null &&
            phaseIndex >= 0 &&
            phaseIndex < greenInPhase.Length)
        {
            green = greenInPhase[phaseIndex];
        }

        TrafficLightState targetState = green ? TrafficLightState.Green : TrafficLightState.Red;

        // En vez de cambiar directo, usamos la corrutina de transición
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(HandleTransition(targetState));
    }

    /// <summary>
    /// Maneja la transición entre estados con:
    /// - Amarillo antes de pasar de Verde a Rojo.
    /// - Delay con todos en rojo antes de pasar de Rojo a Verde.
    /// </summary>
    private System.Collections.IEnumerator HandleTransition(TrafficLightState targetState)
    {
        TrafficLightState oldState = currentState;

        // VERDE -> ROJO: primero amarillo, luego rojo
        if (oldState == TrafficLightState.Green && targetState == TrafficLightState.Red)
        {
            SetState(TrafficLightState.Yellow);
            yield return new WaitForSeconds(yellowBeforeRedTime);
            SetState(TrafficLightState.Red);
            yield break;
        }

        // ROJO -> VERDE: esperamos un tiempo en rojo, luego verde
        if (oldState == TrafficLightState.Red && targetState == TrafficLightState.Green)
        {
            // Nos aseguramos de estar en rojo durante el delay
            SetState(TrafficLightState.Red);
            yield return new WaitForSeconds(redToGreenDelay);
            SetState(TrafficLightState.Green);
            yield break;
        }

        // Cualquier otra combinación (por si acaso) cambia directo
        SetState(targetState);
    }

        private void Update()
    {
        if (VehicleManager.Instance != null && !string.IsNullOrEmpty(lightId))
            debugQueueCount = VehicleManager.Instance.GetWaitingVehiclesForLight(lightId);
        else
            debugQueueCount = 0;
    }



    private void UpdateVisuals()
    {
        // RED
        if (redRenderer != null)
        {
            redRenderer.material =
                (currentState == TrafficLightState.Red && redMaterial != null)
                ? redMaterial
                : offMaterial;
        }

        // YELLOW
        if (yellowRenderer != null)
        {
            yellowRenderer.material =
                (currentState == TrafficLightState.Yellow && yellowMaterial != null)
                ? yellowMaterial
                : offMaterial;
        }

        // GREEN
        if (greenRenderer != null)
        {
            greenRenderer.material =
                (currentState == TrafficLightState.Green && greenMaterial != null)
                ? greenMaterial
                : offMaterial;
        }
    }
}
