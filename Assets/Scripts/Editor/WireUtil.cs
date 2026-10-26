using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Editor helpers that write persistent UnityEvent calls (ported from the desktop WireUtil). Used by the per-task wiring
/// generators so a task flow ends up as ordinary Inspector events that can be edited by hand afterwards.</summary>
public static class WireUtil
{
    public static MethodInfo M(Type t, string name, params Type[] args)
    {
        var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, args, null);
        if (m == null) throw new Exception("no method " + t.Name + "." + name);
        return m;
    }

    public static UnityAction Act(UnityEngine.Object target, MethodInfo mi) { return (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, mi); }

    public static void Clear(UnityEventBase ev)
    {
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(ev, i);
    }

    public static void Void(UnityEventBase ev, UnityEngine.Object target, string name)
    {
        UnityEventTools.AddVoidPersistentListener(ev, Act(target, M(target.GetType(), name, Type.EmptyTypes)));
    }

    public static void Obj(UnityEventBase ev, UnityEngine.Object target, string name, GameObject arg)
    {
        var mi = M(target.GetType(), name, typeof(GameObject));
        UnityEventTools.AddObjectPersistentListener<GameObject>(ev, (UnityAction<GameObject>)Delegate.CreateDelegate(typeof(UnityAction<GameObject>), target, mi), arg);
    }

    public static void Tr(UnityEventBase ev, UnityEngine.Object target, string name, Transform arg)
    {
        var mi = M(target.GetType(), name, typeof(Transform));
        UnityEventTools.AddObjectPersistentListener<Transform>(ev, (UnityAction<Transform>)Delegate.CreateDelegate(typeof(UnityAction<Transform>), target, mi), arg);
    }

    public static void Str(UnityEventBase ev, UnityEngine.Object target, string name, string arg)
    {
        var mi = M(target.GetType(), name, typeof(string));
        UnityEventTools.AddStringPersistentListener(ev, (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), target, mi), arg);
    }

    public static void Flt(UnityEventBase ev, UnityEngine.Object target, string name, float arg)
    {
        var mi = M(target.GetType(), name, typeof(float));
        UnityEventTools.AddFloatPersistentListener(ev, (UnityAction<float>)Delegate.CreateDelegate(typeof(UnityAction<float>), target, mi), arg);
    }

    /// <summary>TMP_Text.text = value.</summary>
    public static void Text(UnityEventBase ev, TMPro.TMP_Text t, string value)
    {
        var mi = typeof(TMPro.TMP_Text).GetProperty("text").GetSetMethod();
        UnityEventTools.AddStringPersistentListener(ev, (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), t, mi), value);
    }

    public static void SetActive(UnityEventBase ev, GameObject go, bool v)
    {
        UnityEventTools.AddBoolPersistentListener(ev, (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), go, M(typeof(GameObject), "SetActive", typeof(bool))), v);
    }

    public static void Enabled(UnityEventBase ev, Behaviour b, bool v)
    {
        var mi = typeof(Behaviour).GetProperty("enabled").GetSetMethod();
        UnityEventTools.AddBoolPersistentListener(ev, (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), b, mi), v);
    }

    public static void Play(UnityEventBase ev, Animator a, string state)
    {
        var mi = typeof(Animator).GetMethod("Play", new[] { typeof(string) });
        UnityEventTools.AddStringPersistentListener(ev, (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), a, mi), state);
    }

    /// <summary>ParticleSystem.Play / Stop / Clear (parameterless).</summary>
    public static void PS(UnityEventBase ev, ParticleSystem p, string name)
    {
        var mi = typeof(ParticleSystem).GetMethod(name, Type.EmptyTypes);
        UnityEventTools.AddVoidPersistentListener(ev, (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), p, mi));
    }

    /// <summary>Makes every persistent call run in the editor-and-runtime (state 2), as the desktop wiring does.</summary>
    public static void FixCallStates(UnityEngine.Object owner)
    {
        var so = new SerializedObject(owner);
        var p = so.GetIterator();
        while (p.Next(true)) if (p.name == "m_CallState") p.intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
