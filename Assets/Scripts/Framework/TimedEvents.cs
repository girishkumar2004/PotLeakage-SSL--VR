using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>A small Inspector timeline: Run() fires every step's UnityEvent at its time (seconds after Run). Use it from Events To Follow
/// for automatic, non-interactive stretches of a task (crane rolls, then the cylinder slides, then ...). Run() again restarts it, Stop()
/// cancels it; it also stops when the object is disabled.</summary>
public class TimedEvents : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        public string label;
        public float atSeconds;
        public UnityEvent onFire = new UnityEvent();
    }

    public List<Step> steps = new List<Step>();

    Coroutine routine;

    public void Run()
    {
        Stop();
        routine = StartCoroutine(Go());
    }

    public void Stop()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
    }

    void OnDisable() { routine = null; }

    IEnumerator Go()
    {
        var order = new List<Step>(steps);
        order.Sort((a, b) => a.atSeconds.CompareTo(b.atSeconds));
        float t0 = Time.time;
        foreach (var s in order)
        {
            float wait = s.atSeconds - (Time.time - t0);
            if (wait > 0f) yield return new WaitForSeconds(wait);
            s.onFire.Invoke();
        }
        routine = null;
    }
}
