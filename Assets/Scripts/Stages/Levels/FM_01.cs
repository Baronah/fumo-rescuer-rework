using NavMeshPlus.Components;
using UnityEngine;
using UnityEngine.AI;

public class FM_01 : StageManager
{
    [SerializeField] NavMeshSurface Nav_Surf;
    [SerializeField] NavMeshData Nav_NM, Nav_CM;
    [SerializeField] private GameObject[] CM_EnableObjs, CM_DisableObjs;

    public override void EnableChallengeMode()
    {
        NavMesh.RemoveAllNavMeshData();
        NavMeshDataInstance currentInstance = NavMesh.AddNavMeshData(Nav_NM);

        if (CharacterPrefabsStorage.EnableChallengeMode)
        {
            NavMesh.RemoveNavMeshData(currentInstance);
            NavMesh.AddNavMeshData(Nav_CM);

            base.EnableChallengeMode();

            foreach (GameObject obj in CM_EnableObjs) obj.SetActive(true);
            for (int i = 0; i < CM_DisableObjs.Length; i++) Destroy(CM_DisableObjs[i]);
        }
        else
        {
            for (int i = 0; i < CM_EnableObjs.Length; i++) Destroy(CM_EnableObjs[i]);
            foreach (GameObject obj in CM_DisableObjs) obj.SetActive(true);
        }
    }
}