using System.Collections;
using UnityEngine;

public class SeeSawButtonManager : MonoBehaviour
{
    public enum SeeSawState
    {
        Free,
        Stretch
    }

    [Header("Jump See Saw")]
    [SerializeField] private GameObject mySeeSaw;
    [SerializeField] private SeeSawState mySeeSawState;
    [SerializeField] private float angleAfterPressed = 27.0f;
    [SerializeField] private float rotateSpeed = 30.0f;

    private Rigidbody _mySeeSawRb;
    private bool _isStretching;

    [Header("Bridge")]
    [SerializeField] private GameObject myBridge;
    [SerializeField] private float localXAfterPressed = 0.6f;
    [SerializeField] private float bridgeMoveSpeed = 1.0f;

    private void Start()
    {
        mySeeSawState = SeeSawState.Free;

        if (mySeeSaw != null)
        {
            _mySeeSawRb = mySeeSaw.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (mySeeSawState != SeeSawState.Stretch) return;
        if (!_isStretching)
        {
            StartCoroutine(StaticAndStretch());
        }
    }

    public void SetState(SeeSawState state)
    {
        mySeeSawState = state;
    }

    private IEnumerator StaticAndStretch()
    {
        _isStretching = true;

        if (_mySeeSawRb)
        {
            if (!_mySeeSawRb.isKinematic)
            {
                _mySeeSawRb.linearVelocity = Vector3.zero;
                _mySeeSawRb.angularVelocity = Vector3.zero;
            }

            _mySeeSawRb.isKinematic = true;
        }

        //静止状態の角度に戻る
        while (Mathf.Abs(Mathf.DeltaAngle(mySeeSaw.transform.localEulerAngles.z, angleAfterPressed)) > 0.1f)
        {
            var currentEuler = mySeeSaw.transform.localEulerAngles;
            var nextZ = Mathf.MoveTowardsAngle(
                currentEuler.z,
                angleAfterPressed,
                rotateSpeed * Time.deltaTime
            );

            mySeeSaw.transform.localRotation = Quaternion.Euler(
                currentEuler.x,
                currentEuler.y,
                nextZ
            );

            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

        if(SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayLoopSE3D(SoundID.Env_IronPlateLock_Loop, myBridge);
        }

        //橋を伸ばす
        while (myBridge.transform.localPosition.x < localXAfterPressed)
        {
            var currentPosition = myBridge.transform.localPosition;

            var nextX = Mathf.MoveTowards(
                currentPosition.x,
                localXAfterPressed,
                bridgeMoveSpeed * Time.deltaTime
            );

            myBridge.transform.localPosition = new Vector3(
                nextX,
                currentPosition.y,
                currentPosition.z
            );

            yield return null;
        }

        if(SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE3D
                (SoundID.Env_IronPlateLock_Loop, myBridge);
        }

        _isStretching = false;
    }
}
