using System.Linq;
using UnityEngine;

public sealed class RuntimePlayerVisualAnimator : MonoBehaviour
{
    Animation animationComponent;
    Transform observedRoot;
    string idleClipName;
    string runClipName;
    string jumpClipName;
    string currentClipName;
    Vector3 lastRootPosition;

    public void Initialize(Transform root, AnimationClip[] clips)
    {
        observedRoot = root;
        lastRootPosition = root != null ? root.position : Vector3.zero;

        if (clips == null || clips.Length == 0)
            return;

        animationComponent = gameObject.GetComponent<Animation>();
        if (animationComponent == null)
            animationComponent = gameObject.AddComponent<Animation>();

        foreach (var clip in clips.Where(c => c != null && !string.IsNullOrWhiteSpace(c.name)))
        {
            if (animationComponent.GetClip(clip.name) != null)
                continue;

            clip.legacy = true;
            animationComponent.AddClip(clip, clip.name);
        }

        idleClipName = FindClipName("idle");
        runClipName = FindClipName("run", "walk", "move");
        jumpClipName = FindClipName("jump", "air");

        if (string.IsNullOrEmpty(runClipName))
            runClipName = idleClipName;
        if (string.IsNullOrEmpty(idleClipName))
            idleClipName = runClipName;

        SetWrapMode(idleClipName, WrapMode.Loop);
        SetWrapMode(runClipName, WrapMode.Loop);
        SetWrapMode(jumpClipName, WrapMode.ClampForever);

        PlayClip(!string.IsNullOrEmpty(runClipName) ? runClipName : idleClipName);
    }

    void Update()
    {
        if (animationComponent == null || observedRoot == null)
            return;

        Vector3 rootDelta = observedRoot.position - lastRootPosition;
        lastRootPosition = observedRoot.position;

        bool isJumping = Mathf.Abs(rootDelta.y) > 0.02f && !string.IsNullOrEmpty(jumpClipName);
        bool isRunning = observedRoot.GetComponent<PlayerMovement>() != null;

        if (isJumping)
        {
            PlayClip(jumpClipName);
            return;
        }

        if (isRunning && !string.IsNullOrEmpty(runClipName))
        {
            PlayClip(runClipName);
            return;
        }

        PlayClip(idleClipName);
    }

    string FindClipName(params string[] keywords)
    {
        foreach (AnimationState state in animationComponent)
        {
            string lowered = state.name.ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                if (lowered.Contains(keywords[i]))
                    return state.name;
            }
        }

        foreach (AnimationState state in animationComponent)
            return state.name;

        return null;
    }

    void SetWrapMode(string clipName, WrapMode wrapMode)
    {
        if (string.IsNullOrEmpty(clipName) || animationComponent[clipName] == null)
            return;

        animationComponent[clipName].wrapMode = wrapMode;
    }

    void PlayClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || currentClipName == clipName)
            return;

        if (animationComponent[clipName] == null)
            return;

        currentClipName = clipName;
        animationComponent.CrossFade(clipName, 0.12f);
    }
}
