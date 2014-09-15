using UnityEngine;
using System.Collections;
using System;
using System.Diagnostics;

[AddComponentMenu("Editor/Prefab Assembler")]
public class PrefabAssembler : MonoBehaviour
{
	public static bool IsSaving = false;
	public static PrefabAssembler Current;

	public GameObject prefab;
	public int priority = 0;

	/// <summary>
	/// Destroy the prefab assembler so that it is not included in the prefab
	/// </summary>
	void OnAssemble ()
	{
		GameObject.DestroyImmediate(this);
	}

#if UNITY_EDITOR
	static Action<PrefabAssembler> EDITOR_AssembleCallback;
	static void EDITOR_AssembleCallbackWarningSupression ()
		{ EDITOR_AssembleCallback += (p)=>{}; }
#endif	

	[Conditional("UNITY_EDITOR")]
	public void Assemble ()
	{
#if UNITY_EDITOR
		if(EDITOR_AssembleCallback != null)
		{
			EDITOR_AssembleCallback(this);
		}
#endif
	}
	
	public static PrefabAssembler FindInScene (string name)
	{
		foreach(var obj in FindObjectsOfType(typeof(PrefabAssembler)))
		{
			if(obj.name == name)
			{
				return (PrefabAssembler)obj;
			}
		}
		return null;
	}
}

public class AssembleOnSave : Attribute
{
}

public struct PrefabAssembleProgress
{
	public float progress;
	public string message;
	
	public PrefabAssembleProgress (float progress, string message)
	{
		this.progress = progress;
		this.message = message;
	}
	public PrefabAssembleProgress (float progress)
	{
		this.progress = progress;
		this.message = "";
	}

	public PrefabAssembleProgress Normalize (float start, float end)
	{
		progress = start + (end-start)*progress;
		return this;
	}
}