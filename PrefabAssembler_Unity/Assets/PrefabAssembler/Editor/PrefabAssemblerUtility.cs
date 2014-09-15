using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System;

public static class PrefabAssemblerUtility
{
	
	public static void SetAssemblerTarget (PrefabAssembler assembler, string path)
	{
		var t = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
		if(t)
		{
			assembler.prefab = t;
		}
		else
		{
			var go = new GameObject();
			assembler.prefab = PrefabUtility.CreatePrefab(path, go);
			PrefabAssemblerUtility.Assemble(assembler);
			EditorUtility.SetDirty(assembler);
			AssetDatabase.Refresh();
			GameObject.DestroyImmediate(go);
		}
	}
	
	static PrefabAssembler[] GetSelectedAssemblers ()
	{
		HashSet<PrefabAssembler> assemblers = new HashSet<PrefabAssembler>();
		foreach(var go in Selection.gameObjects)
		{
			if(EditorUtility.IsPersistent(go))
			{
				continue;
			}
			
			// Seek assemblers in children
			foreach(var a in go.GetComponentsInChildren<PrefabAssembler>())
			{
				assemblers.Add(a);
			}
			
			// Seek assemblers in parents
			Transform t = go.transform.parent;
			while(t)
			{
				var a = t.GetComponent<PrefabAssembler>();
				if(a)
				{
					assemblers.Add(a);
				}
				t = t.parent;
			}
		}
		return assemblers.ToArray();
	}
	
	[MenuItem("GameObject/Prefab Assembly/Assemble Dependencies %k")]
	static void AssembleDependenciesMenuItem ()
	{		
		AssembleDependencies(GetSelectedAssemblers());
	}
	
	[MenuItem("GameObject/Prefab Assembly/Assemble All %#k")]
	static void AssembleAllMenuItem ()
	{
		AssembleAll();
	}
	
	[MenuItem("GameObject/Prefab Assembly/Assemble Selected %&k")]
	static void AssembleSelectedMenuItem ()
	{
		AssembleHierarchy(GetSelectedAssemblers());
	}
	
	[MenuItem("GameObject/Prefab Assembly/Assemble All Scenes")]
	static void AssembleAllScenesMenuItem ()
	{
		AssembleAllScenes();
	}
	
	[MenuItem("GameObject/Prefab Assembly/Instance, %i")]
	static void InstanceAssemblerMenuItem ()
	{
		if(Selection.objects.Length != 1)
		{
			return;
		}
		
		if(!Selection.activeGameObject)
		{
			return;
		}
		
		PrefabAssembler assembler = Selection.activeGameObject.GetComponent<PrefabAssembler>();
		if(!assembler)
		{
			var path = EditorUtility.SaveFilePanelInProject("Pick Prefab Path", Selection.activeGameObject.name, "prefab", "Pick a location to save the prefab.");
			if(path == null || path == "") return;
			
			assembler = Selection.activeGameObject.AddComponent<PrefabAssembler>();
			
			var t = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
			if(t)
			{
				assembler.prefab = t;
				PrefabAssemblerUtility.Assemble(assembler);
			}
			else
			{
				var go = new GameObject();
				assembler.prefab = PrefabUtility.CreatePrefab(path, go);
				PrefabAssemblerUtility.Assemble(assembler);
				GameObject.DestroyImmediate(go);
			}
		}
		
		var instance = PrefabUtility.InstantiatePrefab(assembler.prefab) as GameObject;
		instance.transform.position = assembler.transform.position;
		instance.transform.rotation = assembler.transform.rotation;
		instance.transform.parent = assembler.transform.parent;
		
		Selection.activeObject = assembler.gameObject;
	}
	
	[MenuItem("GameObject/Prefab Assembly/New Staging Scene")]
	static void NewStagingScene ()
	{
		if(!EditorApplication.SaveCurrentSceneIfUserWantsTo())
		{
			return;
		}
		
		var path = EditorUtility.SaveFilePanelInProject("Pick Prefab Path", Selection.activeGameObject.name, "prefab", "Pick a location to save the prefab.");
		if(path == null || path == "") return;
		
		EditorApplication.NewScene();
		GameObject.DestroyImmediate(Camera.main.gameObject);
		
		var file = new FileInfo(path);
		var name = file.Name;
		var gameObject = new GameObject(name);
		var assembler = gameObject.AddComponent<PrefabAssembler>();
		
		var t = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) as GameObject;
		if(t)
		{
			assembler.prefab = t;
			PrefabAssemblerUtility.Assemble(assembler);
		}
		else
		{
			var go = new GameObject();
			assembler.prefab = PrefabUtility.CreatePrefab(path, go);
			PrefabAssemblerUtility.Assemble(assembler);
			GameObject.DestroyImmediate(go);
		}
		
		Selection.activeGameObject = gameObject;
	}
	
	public static void AssembleAllScenes ()
	{
		if(!EditorApplication.SaveCurrentSceneIfUserWantsTo())
		{
			return;
		}
		
		try
		{
			string originalScene = EditorApplication.currentScene;
			
			var allSceneFiles = new DirectoryInfo("Assets/").GetFiles("*.unity", SearchOption.AllDirectories);
			for(int i = 0; i < allSceneFiles.Length; i++)
			{
				var sceneFile = allSceneFiles[i];
				int assetsIndex = sceneFile.FullName.IndexOf("Assets\\");
				var path = sceneFile.FullName.Substring(assetsIndex, sceneFile.FullName.Length - assetsIndex);
				
				if(EditorUtility.DisplayCancelableProgressBar("Assembling project", "Assembling all prefabs in all scenes.", (float)i/allSceneFiles.Length))
				{
					EditorUtility.ClearProgressBar();
					break;
				}
				
				EditorApplication.OpenScene(path);
				AssembleAll();
			}
			
			EditorApplication.OpenScene(originalScene);
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}
	}
	
	public static void AssembleAll ()
	{
		var assemblers = GameObject.FindObjectsOfType(typeof(PrefabAssembler)) as PrefabAssembler[];
		Assemble(assemblers);
	}
	
	public static void AssembleHierarchy (params PrefabAssembler[] assemblers)
	{
		var assemblerHierarchy = GetAssemblerHierarchy(assemblers).ToArray();
		Assemble(assemblerHierarchy);
	}
	
	public static void AssembleDependencies (params PrefabAssembler[] assemblers)
	{
		var assemblerDependencies = GetAssemblerDependencies(assemblers).ToArray();
		Assemble(assemblerDependencies);
	}
	
	/// <summary>
	/// Assemble an array of prefabs. Sorted by priority.
	/// </summary>
	/// <param name="assemblers">The array of prefabs to assemble.</param>
	public static void Assemble (PrefabAssembler[] assemblers)
	{
		if(assemblers.Length == 0)
		{
			Debug.Log("No prefabs to assemble.");
			return;
		}
		
		Array.Sort<PrefabAssembler>(assemblers, (x,y) => 
		                            {
			return x.priority - y.priority; 
		});
		
		try
		{
			var log = new System.Text.StringBuilder("Assembling prefabs: ");
			
			var errors = new Dictionary<PrefabAssembler, Exception>();
			
			float assemblyLength = 1f/assemblers.Length;
			
			for(int i = 0; i < assemblers.Length; i++)
			{
				var assembler = assemblers[i];
				
				if(!assembler)
				{
					continue;
				}
				
				if(!assembler.prefab)
				{
					continue;
				}
				
				string assemblyDescription = "Assembling " + assembler.name + " into " + assembler.prefab.name + ".prefab ({0})";
				float assemblyPos = (float)i/assemblers.Length;
				string message = null;
				
				if(i != 0) log.Append(", ");
				log.Append(assembler.name);
				
				try
				{
					var asyncAssembly = AssembleInternalAsync(assembler);
					while(asyncAssembly.MoveNext())
					{
						var progress = asyncAssembly.Current;
						if(message == null || progress.message != null)
						{
							message = string.Format(assemblyDescription, progress.message);
						}
						EditorUtility.DisplayProgressBar("Assembling Prefabs...", message, assemblyPos + (progress.progress * assemblyLength));
					}
				}
				catch(Exception e)
				{
					errors.Add(assembler, e);
				}
			}
			
			Debug.Log(log.ToString());
			
			if(errors.Count != 0)
			{
				foreach(var kvp in errors)
				{
					Debug.LogException(kvp.Value, kvp.Key);
				}
				Debug.LogError("Prefab assembly completed with errors (see above)");
			}
		}
		finally 
		{
			EditorUtility.ClearProgressBar();
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}
	}
	
	/// <summary>
	/// Assemble a single prefab.
	/// </summary>
	/// <param name="assembler">The prefab to assemble.</param>
	public static void Assemble (PrefabAssembler assembler)
	{
		try
		{
			AssembleInternal(assembler);
		}
		catch(Exception e)
		{
			Debug.LogError(e, assembler);
		}
	}
	
	/// <summary>
	/// Assembles a single prefab, without exception handling
	/// </summary>
	/// <param name="assembler">The prefab to assemble.</param>
	static void AssembleInternal (PrefabAssembler assembler)
	{
		var asyncAssemble = AssembleInternalAsync(assembler);
		
		bool moveNext = true;
		while(moveNext)
		{
			try
			{
				moveNext = asyncAssemble.MoveNext();
			}
			catch(Exception e)
			{
				Debug.LogError(e);
				return;
			}
		}
	}
	
	static IEnumerator<PrefabAssembleProgress> AssembleInternalAsync (PrefabAssembler assembler)
	{
		if(!assembler.prefab)
		{
			yield break;
		}
		
		PrefabAssembler.Current = assembler;
		
		yield return new PrefabAssembleProgress(0f, "Pre-assembly");
		
		object[] parameters = new object[0];
		
		var allBehaviours = GetBehavioursHierarchial(assembler.gameObject);
		foreach(var mb in allBehaviours)
		{
			if(mb == null) continue;
			var mbType = mb.GetType();
			if(mbType == null) continue;
			
			var method = GetMethod(mbType, "OnPreAssemble");
			if(method != null && method.GetParameters().Length == 0)
			{
				if(method.ReturnType == typeof(IEnumerator<PrefabAssembleProgress>))
				{
					var asyncMethod = (IEnumerator<PrefabAssembleProgress>)method.Invoke(mb, parameters);
					if(asyncMethod != null)
					{
						while(asyncMethod.MoveNext())
						{
							var progress = asyncMethod.Current;
							progress.progress = Mathf.Lerp(0f, 0.2f, progress.progress);
							yield return progress;
						}
					}
				}
				else
				{
					method.Invoke(mb, parameters);
				}
			}
		}
		
		yield return new PrefabAssembleProgress(0.2f, "Cloning");
		
		var instance = GameObject.Instantiate(assembler) as PrefabAssembler;
		instance.name = assembler.prefab.name;
		
		var gameObject = instance.gameObject;
		gameObject.name = assembler.name;
		
		yield return new PrefabAssembleProgress(0.3333f, "Assembling");
		
		allBehaviours = GetBehavioursHierarchial(gameObject);
		foreach(var mb in allBehaviours)
		{
			if(mb == null) continue;
			var mbType = mb.GetType();
			if(mbType == null) continue;
			
			var method = GetMethod(mbType, "OnAssemble");
			if(method != null && method.GetParameters().Length == 0)
			{
				if(method.ReturnType == typeof(IEnumerator<PrefabAssembleProgress>))
				{
					var asyncMethod = (IEnumerator<PrefabAssembleProgress>)method.Invoke(mb, parameters);
					if(asyncMethod != null)
					{
						bool moveNext = true;
						while(moveNext)
						{
							try
							{
								moveNext = asyncMethod.MoveNext();
							}
							catch(Exception e)
							{
								GameObject.DestroyImmediate(gameObject);
								throw e;
								//								throw e.InnerException;
							}
							
							var progress = asyncMethod.Current;
							progress.progress = Mathf.Lerp(0.3333f, 0.6f, progress.progress);
							yield return progress;
						}
					}
				}
				else
				{
					try
					{
						method.Invoke(mb, parameters);
					}
					catch
					{
						GameObject.DestroyImmediate(gameObject);
						throw;
					}
				}
			}
		}
		
		yield return new PrefabAssembleProgress(0.6f, "Applying");
		
		PrefabUtility.ReplacePrefab(gameObject, assembler.prefab, ReplacePrefabOptions.ReplaceNameBased);
		
		yield return new PrefabAssembleProgress(0.8f, "Post-assembly");
		
		allBehaviours = GetBehavioursHierarchial((GameObject)assembler.prefab);
		foreach(var mb in allBehaviours)
		{
			if(mb == null) continue;
			var mbType = mb.GetType();
			if(mbType == null) continue;
			
			var method = GetMethod(mbType, "OnPostAssemble");
			
			if(method != null && method.GetParameters().Length == 0)
			{
				if(method.ReturnType == typeof(IEnumerator<PrefabAssembleProgress>))
				{
					var asyncMethod = (IEnumerator<PrefabAssembleProgress>)method.Invoke(mb, parameters);
					if(asyncMethod != null)
					{
						while(asyncMethod.MoveNext())
						{
							var progress = asyncMethod.Current;
							progress.progress = Mathf.Lerp(0.8f, 0.95f, progress.progress);
							yield return progress;
						}
					}
				}
				else
				{
					method.Invoke(mb, parameters);
				}
			}
		}
		
		var labels = AssetDatabase.GetLabels(assembler.prefab);
		bool found = false;
		for(int i = 0; i < labels.Length; i++)
		{
			var label = labels[i];
			if(label.StartsWith("Stage: "))
			{
				labels[i] = GenerateLabel();
				AssetDatabase.SetLabels(assembler.prefab, labels);
				found = true;
				break;
			}
		}
		if(!found)
		{
			var list = new List<string>(labels);
			list.Add(GenerateLabel());
			AssetDatabase.SetLabels(assembler.prefab, list.ToArray());
		}
		
		yield return new PrefabAssembleProgress(0.95f, "Cleanup");
		
		GameObject.DestroyImmediate(gameObject);
		
		PrefabAssembler.Current = null;
		
		yield break;
	}
	
	static MethodInfo GetMethod (Type type, string name)
	{
		while(type != typeof(object) && type != typeof(MonoBehaviour))
		{
			var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
			if(method != null)
			{
				return method;
			}
			type = type.BaseType;
		}
		return null;
	}
	
	static string GenerateLabel ()
	{
		return "Stage: " + EditorApplication.currentScene.Replace("Assets/","");
	}
	
	static IEnumerable<MonoBehaviour> GetBehavioursHierarchial (GameObject gameObject)
	{
		List<Transform> next = new List<Transform>(){gameObject.transform};
		while(next.Count != 0)
		{
			var now = next.ToArray();
			next.Clear();
			for(int i = 0; i < now.Length; i++)
			{
				var tr = now[i];
				foreach(var mb in tr.GetComponents<MonoBehaviour>())
				{
					yield return mb;
				}
				for(int t = 0; t < tr.childCount; t++)
				{
					next.Add(tr.GetChild(t));
				}
			}
		}
	}
	
	/// <summary>
	/// Performs a search of the scene for any other prefab assemblers which are dependant on the given prefabs
	/// Uses the recursive GetAssemblerHierarchy with the same name
	/// </summary>
	static IEnumerable<PrefabAssembler> GetAssemblerHierarchy (params PrefabAssembler[] assemblers)
	{
		var assemblersByPrefab = GetAllAssemblersByPrefab();
		var assemblerChildren = new Dictionary<PrefabAssembler, List<PrefabAssembler>>();
		
		foreach(var kvp in assemblersByPrefab)
		{			
			foreach(var obj in GetPrefabs(kvp.Value.transform))
			{
				PrefabAssembler b = null;
				if(assemblersByPrefab.TryGetValue(obj, out b))
				{
					List<PrefabAssembler> list = null;
					if(!assemblerChildren.TryGetValue(b, out list))
					{
						list = new List<PrefabAssembler>();
						assemblerChildren.Add(b, list);
					}
					list.Add(kvp.Value);
				}
			}
		}
		
		var used = new HashSet<PrefabAssembler>();
		foreach(var a in assemblers)
		{
			foreach(var b in GetAssemblerHierarchy(a, assemblerChildren, used))
			{
				yield return b;
			}
		}
	}
	
	static IEnumerable<PrefabAssembler> GetAssemblerHierarchy (
		PrefabAssembler assembler, 
		Dictionary<PrefabAssembler, List<PrefabAssembler>> prefabs,
		HashSet<PrefabAssembler> used)
	{
		if(!used.Add(assembler))
		{
			yield break;
		}
		
		yield return assembler;
		
		List<PrefabAssembler> p = null;
		
		if(prefabs.TryGetValue(assembler, out p))
		{
			foreach(var a in p)
			{
				foreach(var b in GetAssemblerHierarchy(a, prefabs, used))
				{
					yield return b;
				}
			}
		}
	}
	
	/// <summary>
	/// Performs a search of the scene for any other prefab assemblers which are dependant on the given prefabs
	/// Uses the recursive GetAssemblerHierarchy with the same name
	/// </summary>
	static IEnumerable<PrefabAssembler> GetAssemblerDependencies (params PrefabAssembler[] assemblers)
	{
		return GetAssemblerDependencies(new HashSet<PrefabAssembler>(), assemblers);
	}
	
	static IEnumerable<PrefabAssembler> GetAssemblerDependencies (HashSet<PrefabAssembler> found, params PrefabAssembler[] assemblers)
	{
		var assemblersByPrefab = GetAllAssemblersByPrefab();
		
		foreach(PrefabAssembler a in assemblers)
		{			
			if(found.Add(a))
			{
				yield return a;
			}
			
			foreach(var obj in GetPrefabs(a.transform))
			{
				PrefabAssembler b = null;
				if(assemblersByPrefab.TryGetValue(obj, out b))
				{
					if(found.Add(b))
					{
						yield return b;
						
						foreach(var p in GetAssemblerDependencies(found, b))
						{
							yield return p;
						}
					}
				}
			}
		}
	}
	
	static Dictionary<UnityEngine.Object, PrefabAssembler> GetAllAssemblersByPrefab ()
	{
		var allAssemblers = GameObject.FindObjectsOfType(typeof(PrefabAssembler));
		var assemblersByPrefab = new Dictionary<UnityEngine.Object, PrefabAssembler>();
		
		foreach(PrefabAssembler a in allAssemblers)
		{
			if(assemblersByPrefab.ContainsKey(a.prefab))
			{
				Debug.LogWarning("Warning: Multiple assemblers referencing the same prefab!", a);
				continue;
			}
			assemblersByPrefab.Add(a.prefab, a);
		}
		
		return assemblersByPrefab;
	}
	
	static IEnumerable<UnityEngine.Object> GetPrefabs (Transform transform)
	{
		foreach(Transform child in transform)
		{
			if(PrefabUtility.GetPrefabType(child) == PrefabType.PrefabInstance)
			{
				var obj = PrefabUtility.GetPrefabParent(child.gameObject);
				if(!obj) continue;
				yield return obj;
			}
			else
			{
				foreach(var obj in GetPrefabs(child))
				{
					yield return obj;
				}
			}
		}
	}
}
