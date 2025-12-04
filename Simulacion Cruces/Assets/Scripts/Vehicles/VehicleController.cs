using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [Header("Interacciones con otros vehículos")]
    [Tooltip("Layer donde están los coches (ej. 'Vehicle').")]
    public LayerMask vehicleLayer;

    [Tooltip("Offset vertical para los raycasts hacia adelante.")]
    public float sensorHeightOffset = 0.5f;

    [Header("Movimiento base")]
    public float baseSpeed = 8f;
    public float baseRotationSpeed = 5f;

    [Header("Semáforos")]
    [Tooltip("Distancia al punto de alto a la cual el vehículo se detiene si el semáforo está en rojo/amarillo.")]
    public float stopDistanceToLight = 2f;

    [Header("Debug / Visualización")]
    [Tooltip("Si está activado, dibuja el área de detección de vehículos en la escena.")]
    public bool debugShowSensors = false;

    [Tooltip("Escala del gizmo del sensor (1 = tamaño real, >1 más grande, <1 más pequeño).")]
    public float debugSensorScale = 1f;

    // Collider del coche
    private Collider vehicleCollider;

    // Datos del collider para cálculos
    private Vector3 colliderLocalCenter;
    private Vector3 colliderHalfExtents;

    private bool isWaiting = false;
    private string waitingLightId = null;

    public bool IsWaiting => isWaiting;
    public string WaitingLightId => waitingLightId;


    // Espera por otro vehículo
    private bool isBlockedByVehicle = false;

    private Transform[] route;
    private int currentIndex = 0;
    private bool initialized = false;

    /// Revisa si en la posición 'nextPosition' hay espacio para avanzar
    /// sin chocarse con otro carro. Usa un OverlapSphere/Box.
    private bool NimodoQueNoFrene(Vector3 nextPosition)
    {
        // Si no tenemos collider, usamos el método viejito con esfera
        float fallbackRadius = VehicleManager.Instance != null
            ? VehicleManager.Instance.mergeCheckRadius
            : 1.5f;

        if (vehicleCollider == null)
        {
            Collider[] hitsFallback = Physics.OverlapSphere(nextPosition, fallbackRadius, vehicleLayer);

            foreach (var hit in hitsFallback)
            {
                if (hit.transform.root == transform.root)
                    continue;

                return false;
            }

            return true;
        }

        // Con collider: usamos una OverlapBox con el tamaño real del coche
        Quaternion rot = transform.rotation;

        // Centro futuro del collider: el coche en nextPosition con su offset de centro
        Vector3 futureCenter = nextPosition + rot * colliderLocalCenter;

        Collider[] hits = Physics.OverlapBox(futureCenter, colliderHalfExtents, rot, vehicleLayer);

        foreach (var hit in hits)
        {
            if (hit.transform.root == transform.root)
                continue;

            // Hay otro coche ocupando el volumen donde estaría nuestro collider
            return false;
        }

        return true;
    }

    /// Detecta si hay un vehículo adelante en la dirección de marcha,
    /// incluyendo coches que se estén incorporando al carril.
    private bool HayVehiculoAdelante(float distance)
    {
        if (distance <= 0f)
            return false;

        // -------------------------
        // 1) Calcular origen en el "parachoques" delantero
        // -------------------------
        Vector3 origin;

        if (vehicleCollider != null)
        {
            // Centro del collider en mundo
            Vector3 worldCenter = transform.TransformPoint(colliderLocalCenter);

            // Punto aproximado del "parachoques" delantero
            origin = worldCenter + transform.forward * colliderHalfExtents.z;
        }
        else
        {
            // Fallback si no hay collider
            origin = transform.position + transform.forward * 1f;
        }

        // Subimos un poco el origen para no chocar con el piso
        origin += Vector3.up * sensorHeightOffset;

        // -------------------------
        // 2) Definir el "tubo" de detección
        // -------------------------
        Vector3 end = origin + transform.forward * distance;

        // Radio del tubo: basado en el tamaño del coche, un poco inflado
        float radius = 0.8f;
        if (vehicleCollider != null)
        {
            float lateral = Mathf.Max(colliderHalfExtents.x, colliderHalfExtents.z);
            radius = Mathf.Max(0.8f, lateral * 1.2f);
        }

        // -------------------------
        // 3) OverlapCapsule para detectar cualquier coche en el tubo
        // -------------------------
        Collider[] hits = Physics.OverlapCapsule(origin, end, radius, vehicleLayer);

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            // Ignoramos nuestro propio coche
            if (hit.transform.root == transform.root)
                continue;

            // Si hay otro coche en este volumen, lo consideramos "adelante"
            return true;
        }

        return false;
    }

    // ================================
    //   GIZMOS DE DEBUG DEL SENSOR
    // ================================
    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!debugShowSensors)
            return;

        // Distancia que usa el sensor (misma que en Update)
        float safeDistance = VehicleManager.Instance != null
            ? VehicleManager.Instance.safeHeadwayDistance
            : 4f;

        // Escala solo visual
        safeDistance *= debugSensorScale;

        // Intentar usar el collider ya cacheado; si no, buscar uno
        Collider col = vehicleCollider != null
            ? vehicleCollider
            : GetComponentInChildren<Collider>();

        Vector3 origin;

        if (col != null)
        {
            Vector3 worldCenter = col.bounds.center;
            origin = worldCenter + transform.forward * col.bounds.extents.z;
        }
        else
        {
            origin = transform.position + transform.forward * 1f;
        }

        origin += Vector3.up * sensorHeightOffset;
        Vector3 end = origin + transform.forward * safeDistance;

        float radius = 0.8f;
        if (col != null)
        {
            Vector3 ext = col.bounds.extents;
            float lateral = Mathf.Max(ext.x, ext.z);
            radius = Mathf.Max(0.8f, lateral * 1.2f);
        }

        // Escala solo visual
        radius *= debugSensorScale;

        Gizmos.color = Color.cyan;
        DrawCapsule(origin, end, radius);
#endif
    }

    private void DrawCapsule(Vector3 start, Vector3 end, float radius)
    {
        // Esferas en los extremos
        Gizmos.DrawWireSphere(start, radius);
        Gizmos.DrawWireSphere(end, radius);

        // Líneas laterales para sugerir el tubo
        Vector3 dir = (end - start).normalized;
        Vector3 up = Vector3.up * radius;
        Vector3 right = Vector3.Cross(dir, Vector3.up).normalized * radius;

        Gizmos.DrawLine(start + up, end + up);
        Gizmos.DrawLine(start - up, end - up);
        Gizmos.DrawLine(start + right, end + right);
        Gizmos.DrawLine(start - right, end - right);
    }

    // ================================
    //   RESTO DE TU CÓDIGO
    // ================================

    public void SetRoute(Transform[] routePoints)
    {
        route = routePoints;
        currentIndex = 0;
        initialized = route != null && route.Length > 0;

        if (initialized)
        {
            transform.position = route[0].position;

            if (route.Length > 1)
            {
                Vector3 dir = (route[1].position - route[0].position).normalized;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }

    private void Awake()
    {
        vehicleCollider = GetComponentInChildren<Collider>();

        if (vehicleCollider != null)
        {
            // Centro del collider en espacio local del coche
            colliderLocalCenter = transform.InverseTransformPoint(vehicleCollider.bounds.center);

            // Mitad de cada dimensión del collider (tamaño / 2)
            colliderHalfExtents = vehicleCollider.bounds.extents;
        }
    }

    private void Start()
    {
        if (VehicleManager.Instance != null)
            VehicleManager.Instance.RegisterVehicle(this);
    }

    private void OnDestroy()
    {
        if (VehicleManager.Instance != null)
        {
            VehicleManager.Instance.UnregisterVehicle(this);

            if (isWaiting)
                VehicleManager.Instance.UnregisterWaitingVehicle(waitingLightId);
        }
    }

    private void Update()
    {
        if (!initialized || route == null || route.Length == 0)
            return;

        // Multiplicadores globales de velocidad/rotación
        float speedMultiplier = 1f;
        float rotationMultiplier = 1f;

        if (VehicleManager.Instance != null)
        {
            speedMultiplier = VehicleManager.Instance.globalSpeedMultiplier;
            rotationMultiplier = VehicleManager.Instance.globalRotationMultiplier;
        }

        float speed = baseSpeed * speedMultiplier;
        float rotationSpeed = baseRotationSpeed * rotationMultiplier;

        // Fin de ruta
        if (currentIndex >= route.Length)
        {
            Destroy(gameObject);
            return;
        }

        Transform target = route[currentIndex];
        Vector3 toTarget = target.position - transform.position;

        // ================================
        // 1) Lógica de semáforo
        // ================================
        Waypoint wp = target.GetComponent<Waypoint>();
        bool mustStopForLight = false;
        string targetLightId = null;

        if (wp != null &&
            wp.isStopPoint &&
            !string.IsNullOrEmpty(wp.trafficLightId))
        {
            targetLightId = wp.trafficLightId;

            var lightState = TrafficLightManager.GetLightStateGlobal(targetLightId);

            if ((lightState == TrafficLightState.Red ||
                lightState == TrafficLightState.Yellow) &&
                toTarget.magnitude <= stopDistanceToLight)
            {
                mustStopForLight = true;
            }
        }

        // Gestionar estado de espera por semáforo
        if (mustStopForLight)
        {
            if (!isWaiting)
            {
                isWaiting = true;
                waitingLightId = targetLightId;

                if (VehicleManager.Instance != null)
                    VehicleManager.Instance.RegisterWaitingVehicle(waitingLightId);
            }

            return; // no avanzamos este frame
        }
        else
        {
            if (isWaiting)
            {
                isWaiting = false;

                if (VehicleManager.Instance != null)
                    VehicleManager.Instance.UnregisterWaitingVehicle(waitingLightId);

                waitingLightId = null;
            }
        }

        // ================================
        // 2) Lógica de coche adelante
        // ================================
        float safeDistance = VehicleManager.Instance != null
            ? VehicleManager.Instance.safeHeadwayDistance
            : 4f;

        if (HayVehiculoAdelante(safeDistance))
        {
            isBlockedByVehicle = true;
            return; // hay alguien enfrente, no nos movemos
        }
        else
        {
            isBlockedByVehicle = false;
        }

        // ================================
        // 3) Movimiento + NimodoQueNoFrene
        // ================================
        float distanceThisFrame = speed * Time.deltaTime;

        if (toTarget.magnitude <= distanceThisFrame)
        {
            // Estamos a punto de llegar al punto objetivo
            Vector3 candidatePos = target.position;

            // Revisar si 'quepo' en el punto antes de avanzar
            if (!NimodoQueNoFrene(candidatePos))
            {
                return; // hay otro coche ocupando el espacio, esperamos
            }

            transform.position = candidatePos;
            currentIndex++;

            if (currentIndex < route.Length)
            {
                Vector3 nextDir = (route[currentIndex].position - transform.position).normalized;
                if (nextDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(nextDir, Vector3.up);
            }
        }
        else
        {
            // Movimiento parcial hacia el target
            Vector3 moveDir = toTarget.normalized;
            Vector3 candidatePos = transform.position + moveDir * distanceThisFrame;

            // Revisar si 'quepo' en la posición siguiente
            if (!NimodoQueNoFrene(candidatePos))
            {
                return; // espacio ocupado → esperamos
            }

            transform.position = candidatePos;

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
