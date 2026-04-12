using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CombatLinkAddToScene : MonoBehaviour
{
    // Add menu named "My Window" to the Window menu
    [MenuItem("Combat-Link SDK/Add Combat-Link Combat System Wrapper world prefab into scene")]
    static void CombatSystem_Init()
    {
        var GO = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.snipeslow.sdk.combatlink/Runtime/Prefabs/CombatLink_CombatSystem.prefab");
        if (GO != null)
        {
            Selection.activeObject = PrefabUtility.InstantiatePrefab(GO);
            Selection.activeGameObject.transform.position = Vector3.zero;
            Selection.activeGameObject.transform.rotation = Quaternion.identity;
        }
        else
        {
            Debug.LogError("Combat-Link prefab failed to be added to scene!");
        }
    }
    // Add menu named "My Window" to the Window menu
    [MenuItem("Combat-Link SDK/Add Combat-Link UDON-Sharp world prefab into scene")]
    static void UDONSharp_Init()
    {
        var GO = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.snipeslow.sdk.combatlink/Runtime/Prefabs/CombatLink_UDONSHARPWithWrapper.prefab");
        if (GO != null)
        {
            Selection.activeObject = PrefabUtility.InstantiatePrefab(GO);
            Selection.activeGameObject.transform.position = Vector3.zero;
            Selection.activeGameObject.transform.rotation = Quaternion.identity;
        }
        else
        {
            Debug.LogError("Combat-Link prefab failed to be added to scene!");
        }
    }
    // Add menu named "My Window" to the Window menu
    [MenuItem("Combat-Link SDK/Add Combat-Link UDON-Sharp with wrapper world prefab into scene")]
    static void UDONSharpWrapper_Init()
    {
        var GO = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.snipeslow.sdk.combatlink/Runtime/Prefabs/CombatLink_UDONSHARP.prefab");
        if (GO != null)
        {
            Selection.activeObject = PrefabUtility.InstantiatePrefab(GO);
            Selection.activeGameObject.transform.position = Vector3.zero;
            Selection.activeGameObject.transform.rotation = Quaternion.identity;
        }
        else
        {
            Debug.LogError("Combat-Link prefab failed to be added to scene!");
        }
    }
}
