using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Walks the player through a list of (sourceTool → destinationCopy) picks.
/// For each pair:
///   - Highlight ONLY this source tool (clears highlight on siblings).
///   - Enable and highlight the destination copy (sibling of ToolBox).
///   - Add XRGrabInteractable to source + XRSocketInteractor on destination.
///   - On socketed/drop: disable source tool, keep destination enabled WITHOUT highlight.
/// Plays voiceKey per pair (if set) when the pair begins. OnAllComplete fires at the end.</summary>
public class ToolSelectionSequencer : MonoBehaviour
{
    [Serializable] public class Pair
    {
        public GameObject source;        // e.g. ToolBox/SST PIPE
        public GameObject destination;   // e.g. ToolBox/SST PIPE (1) (placed where the user should drop it)
        public string voiceKey;          // optional TTS key
    }

    public Transform toolBoxRoot;        // used to pulse-highlight only children of this root
    public Pair[] pairs;
    public float socketRadius = 0.25f;

    public Action OnAllComplete;

    int idx = -1;
    readonly List<VRHighlight> currentHighlights = new List<VRHighlight>();
    XRSocketInteractor currentSocket;
    XRGrabInteractable currentGrab;

    public void Begin()
    {
        idx = -1;
        Advance();
    }

    void Advance()
    {
        ClearHighlights();
        if (currentSocket) { Destroy(currentSocket); currentSocket = null; }
        idx++;
        if (idx >= pairs.Length) { OnAllComplete?.Invoke(); return; }

        var p = pairs[idx];
        if (!string.IsNullOrEmpty(p.voiceKey) && VoiceOverPlayer.instance) VoiceOverPlayer.instance.Play(p.voiceKey);

        if (p.source != null)
        {
            p.source.SetActive(true);
            Highlight(p.source);
            currentGrab = SequenceHelperFunctions.EnsureInteractable(p.source, true);
        }

        if (p.destination != null)
        {
            p.destination.SetActive(true);
            Highlight(p.destination);

            // Add trigger collider + socket interactor so dropping the source onto this spot completes the pair.
            var col = p.destination.GetComponent<Collider>();
            if (col == null) { var sc = p.destination.AddComponent<SphereCollider>(); sc.radius = socketRadius; sc.isTrigger = true; col = sc; } else col.isTrigger = true;
            currentSocket = p.destination.AddComponent<XRSocketInteractor>();
            currentSocket.selectEntered.AddListener(OnSocketed);
        }
    }

    void OnSocketed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        var p = pairs[idx];
        if (p.source) p.source.SetActive(false);
        if (p.destination)
        {
            // Clear highlight — keep the destination enabled and in place of source.
            var h = p.destination.GetComponent<VRHighlight>(); if (h) h.SetHighlight(false);
        }
        currentSocket.selectEntered.RemoveListener(OnSocketed);
        Advance();
    }

    void Highlight(GameObject go)
    {
        var h = go.GetComponent<VRHighlight>();
        if (!h) h = go.AddComponent<VRHighlight>();
        h.SetHighlight(true);
        currentHighlights.Add(h);
    }

    void ClearHighlights()
    {
        foreach (var h in currentHighlights) if (h) h.SetHighlight(false);
        currentHighlights.Clear();
    }
}
