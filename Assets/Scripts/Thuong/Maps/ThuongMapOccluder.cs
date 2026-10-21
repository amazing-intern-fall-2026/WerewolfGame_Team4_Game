using Thuong.Online;
using UnityEngine;

public sealed class ThuongMapOccluder : MonoBehaviour
{
    private SpriteRenderer view;
    private void Awake() => view = GetComponent<SpriteRenderer>();
    private void LateUpdate()
    {
        if (view == null || ThuongOnlineSession.Active == null) return;
        var camera = Camera.main;
        var follow = camera != null ? camera.GetComponent<ThuongTopDownCamera>() : null;
        bool behind = follow?.target != null && view.bounds.Contains(follow.target.position + Vector3.up * .5f);
        Color color = view.color; color.a = Mathf.MoveTowards(color.a, behind ? .3f : 1, Time.unscaledDeltaTime * 4); view.color = color;
    }
}
