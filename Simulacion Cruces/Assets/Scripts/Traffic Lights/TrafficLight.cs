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

    [SerializeField, Tooltip("Estado actual (solo lectura)")]
    private TrafficLightState currentState = TrafficLightState.Red;
    public TrafficLightState CurrentState => currentState;

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

        SetState(green ? TrafficLightState.Green : TrafficLightState.Red);
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
