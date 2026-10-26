using UnityEngine;

/// <summary>Put on a canvas root: translates the labels below it every time it is shown (UiLocalizer does the scene-wide pass).</summary>
public class LocalizeOnEnable : MonoBehaviour
{
    void OnEnable()
    {
        if (UiLocalizer.Instance != null && TrainingDataProvider.instance != null && TrainingDataProvider.instance.Ready) UiLocalizer.Instance.Apply(transform);
    }
}
