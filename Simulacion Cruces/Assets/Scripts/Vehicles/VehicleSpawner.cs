using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    [Header("Entrada que representa este spawner")]
    public string entryId = "E1";   // E1, E2, E3, E4, E5

    [Header("Prefabs de vehículos")]
    public GameObject[] vehiclePrefabs;

    public GameObject SpawnVehicle(Transform[] routePoints)
    {
        if (routePoints == null || routePoints.Length == 0 || routePoints[0] == null)
        {
            Debug.LogError($"VehicleSpawner ({entryId}): ruta inválida.");
            return null;
        }

        if (vehiclePrefabs == null || vehiclePrefabs.Length == 0)
        {
            Debug.LogError($"VehicleSpawner ({entryId}): no hay prefabs asignados.");
            return null;
        }

        GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];

        // Posición en el primer punto
        Vector3 spawnPos = routePoints[0].position;
        Quaternion spawnRot = Quaternion.identity;

        if (routePoints.Length > 1 && routePoints[1] != null)
        {
            Vector3 dir = (routePoints[1].position - spawnPos).normalized;
            if (dir.sqrMagnitude > 0.0001f)
                spawnRot = Quaternion.LookRotation(dir, Vector3.up);
        }

        GameObject vehicleObj = Object.Instantiate(prefab, spawnPos, spawnRot);

        VehicleController controller = vehicleObj.GetComponent<VehicleController>();
        if (controller == null)
        {
            Debug.LogError($"VehicleSpawner ({entryId}): el prefab {prefab.name} no tiene VehicleController.");
            return vehicleObj;
        }

        controller.SetRoute(routePoints);
        return vehicleObj;
    }
}
