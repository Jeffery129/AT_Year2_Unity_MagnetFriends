using System.Collections;
using UnityEngine;

public class ButtonCollider : MonoBehaviour
{
    private enum ButtonState
    {
        UnPressed,
        Pressed,
    }
    private ButtonState _myButtonState = ButtonState.UnPressed;

    [SerializeField] private float localYAfterPressed = 0.1f;
    [SerializeField] private float minFallSpeed = 5.0f;
    [SerializeField] private float pressMoveSpeed = 1.0f;

    private SeeSawButtonManager _myButtonManager;

    private void Start()
    {
        _myButtonManager = GetComponentInParent<SeeSawButtonManager>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_myButtonState != ButtonState.UnPressed) return;
        var player = collision.gameObject.GetComponentInParent<MagnetPlayer>();
        if (player == null) return;

        var rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        var fallSpeed = Mathf.Max(0f, -rb.linearVelocity.y);

        if (fallSpeed < minFallSpeed) return;

        _myButtonManager.SetState(SeeSawButtonManager.SeeSawState.Stretch);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(
                SoundID.Env_Seesaw_Launch
            );
        }

        SetButtonState(ButtonState.Pressed);
        StartCoroutine(ButtonPressed());
    }

    private void SetButtonState(ButtonState state)
    {
        _myButtonState = state;
    }

    private IEnumerator ButtonPressed()
    {
        while (transform.localPosition.y > localYAfterPressed)
        {
            var currentPosition = transform.localPosition;

            var nextY = Mathf.MoveTowards(
                currentPosition.y,
                localYAfterPressed,
                pressMoveSpeed * Time.deltaTime
            );

            transform.localPosition = new Vector3(
                currentPosition.x,
                nextY,
                currentPosition.z
            );

            yield return null;
        }
    }
}
