using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class PrefabAssemblerSaveProcess : UnityEditor.AssetModificationProcessor
{
	public static string[] OnWillSaveAssets(string[] paths)
	{
		if(paths.Contains(EditorApplication.currentScene))
		{
			EditorApplication.delayCall += OnSavedScene;
		}
		
		return paths;
	}
	
	static void OnSavedScene ()
	{
		var behaviours = GameObject.FindObjectsOfType(typeof(MonoBehaviour));
		var assemblers = new HashSet<PrefabAssembler>();
		foreach(MonoBehaviour b in behaviours)
		{
			if(b.GetType().GetCustomAttributes(typeof(AssembleOnSave), true).Length == 0)
			{
				continue;
			}
			Transform target = b.transform;
			while(target)
			{
				var a = target.GetComponent<PrefabAssembler>();
				if(a)
				{
					assemblers.Add(a);
				}
				target = target.parent;
			}
		}
		if(assemblers.Count != 0)
		{
			PrefabAssembler.IsSaving = true;
			PrefabAssemblerUtility.Assemble(assemblers.ToArray());
			PrefabAssembler.IsSaving = false;
		}
	}
	
}
