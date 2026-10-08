using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MagneticIronSystem : MonoBehaviour
{
    [Header("プレイヤー設定")]

    [Tooltip("空欄の場合はシーン内のMagnetPlayerを自動検索する")]
    [SerializeField]
    private MagnetPlayer[] players =
        new MagnetPlayer[0];

    [Tooltip("シーン内のMagnetPlayerを自動検索する")]
    [SerializeField]
    private bool autoFindPlayers = true;

    [Tooltip("プレイヤーを再検索する間隔")]
    [Min(0.1f)]
    [SerializeField]
    private float playerRefreshInterval = 1.0f;

    private static MagneticIronSystem _instance;

    private readonly List<MagneticIron> _smallIrons =
        new List<MagneticIron>();

    private readonly List<MagneticIron> _largeIrons =
        new List<MagneticIron>();

    private readonly HashSet<MagnetPlayer>
        _playersInRangeLastFrame =
            new HashSet<MagnetPlayer>();

    private readonly HashSet<MagnetPlayer>
        _playersInRangeThisFrame =
            new HashSet<MagnetPlayer>();

    private readonly Dictionary<MagnetPlayer, Rigidbody>
        _playerBodies =
            new Dictionary<MagnetPlayer, Rigidbody>();

    private float _nextPlayerRefreshTime;

    private void Awake()
    {
        if (_instance != null &&
            _instance != this)
        {
            Debug.LogError(
                "MagneticIronSystemが複数存在しています。" +
                "シーン内に1つだけ配置してください。",
                this
            );

            enabled = false;
            return;
        }

        _instance = this;

        if (autoFindPlayers)
        {
            RefreshPlayers();
        }
        else
        {
            CachePlayerRigidbodies();
        }
    }

    private void OnDisable()
    {
        ReleaseAllPlayerRangeStates();

        foreach (MagneticIron large in _largeIrons)
        {
            if (large != null)
            {
                large.ApplyLargeControlledMotion(
                    Vector3.zero,
                    false
                );
            }
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void FixedUpdate()
    {
        if (autoFindPlayers &&
            Time.unscaledTime >=
            _nextPlayerRefreshTime)
        {
            RefreshPlayers();
        }

        CollectIrons();

        UpdateLargePolarities();
        UpdateSmallPolarities();
        SyncPlayerRangeStates();

        ApplyPlayerForcesToSmall();
        ApplySmallLargeForces();

        ApplySmallIdleBrakes();
        ClampSmallSpeeds();
    }

    [ContextMenu("プレイヤーを再検索")]
    public void RefreshPlayers()
    {
        players =
            FindObjectsByType<MagnetPlayer>(
                FindObjectsSortMode.None
            );

        CachePlayerRigidbodies();

        _nextPlayerRefreshTime =
            Time.unscaledTime +
            Mathf.Max(
                0.1f,
                playerRefreshInterval
            );
    }

    private void CachePlayerRigidbodies()
    {
        _playerBodies.Clear();

        if (players == null)
        {
            return;
        }

        foreach (MagnetPlayer player in players)
        {
            if (player == null)
            {
                continue;
            }

            Rigidbody playerBody =
                player.GetComponent<Rigidbody>();

            _playerBodies[player] =
                playerBody;
        }
    }

    private void CollectIrons()
    {
        _smallIrons.Clear();
        _largeIrons.Clear();

        IReadOnlyList<MagneticIron> allIrons =
            MagneticIron.AllIrons;

        for (int i = 0;
             i < allIrons.Count;
             i++)
        {
            MagneticIron iron =
                allIrons[i];

            if (iron == null ||
                !iron.isActiveAndEnabled)
            {
                continue;
            }

            if (iron.Size ==
                MagneticIron.IronSize.Small)
            {
                _smallIrons.Add(iron);
            }
            else
            {
                _largeIrons.Add(iron);
            }
        }
    }

    private void UpdateLargePolarities()
    {
        foreach (MagneticIron large
                 in _largeIrons)
        {
            large.SetRuntimePolarity(
                large.FixedLargePolarity
            );
        }
    }

    private void UpdateSmallPolarities()
    {
        _playersInRangeThisFrame.Clear();

        foreach (MagneticIron small
                 in _smallIrons)
        {
            MagnetPlayer nearestPlayer =
                null;

            float nearestDistance =
                float.MaxValue;

            if (players != null)
            {
                foreach (MagnetPlayer player
                         in players)
                {
                    if (player == null ||
                        !player.isActiveAndEnabled)
                    {
                        continue;
                    }

                    float distance =
                        Vector3.Distance(
                            small.transform.position,
                            player.transform.position
                        );

                    if (distance >
                        small.PlayerInfluenceRange)
                    {
                        continue;
                    }

                    _playersInRangeThisFrame.Add(
                        player
                    );

                    if (distance <
                        nearestDistance)
                    {
                        nearestDistance =
                            distance;

                        nearestPlayer =
                            player;
                    }
                }
            }

            if (nearestPlayer == null)
            {
                small.SetRuntimePolarity(
                    MagneticIron.Polarity.None
                );

                continue;
            }

            MagneticIron.Polarity sourcePolarity =
                ConvertPlayerPolarity(
                    nearestPlayer.currentPolarity
                );

            MagneticIron.Polarity newPolarity =
                ResolveInheritedPolarity(
                    sourcePolarity,
                    small.InheritanceMode
                );

            small.SetRuntimePolarity(
                newPolarity
            );
        }
    }

    private void SyncPlayerRangeStates()
    {
        foreach (MagnetPlayer player
                 in _playersInRangeThisFrame)
        {
            if (!_playersInRangeLastFrame
                .Contains(player))
            {
                player.EnterExternalMagnetRange();
            }
        }

        foreach (MagnetPlayer player
                 in _playersInRangeLastFrame)
        {
            if (player == null)
            {
                continue;
            }

            if (!_playersInRangeThisFrame
                .Contains(player))
            {
                player.ExitExternalMagnetRange();
            }
        }

        _playersInRangeLastFrame.Clear();

        foreach (MagnetPlayer player
                 in _playersInRangeThisFrame)
        {
            _playersInRangeLastFrame.Add(
                player
            );
        }
    }

    private void ReleaseAllPlayerRangeStates()
    {
        foreach (MagnetPlayer player
                 in _playersInRangeLastFrame)
        {
            if (player != null)
            {
                player.ExitExternalMagnetRange();
            }
        }

        _playersInRangeLastFrame.Clear();
        _playersInRangeThisFrame.Clear();
    }

    private void ApplyPlayerForcesToSmall()
    {
        if (players == null)
        {
            return;
        }

        foreach (MagneticIron small
                 in _smallIrons)
        {
            if (small.CurrentPolarity ==
                MagneticIron.Polarity.None)
            {
                continue;
            }

            foreach (MagnetPlayer player
                     in players)
            {
                if (player == null ||
                    !player.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 acceleration =
                    CalculatePlayerAcceleration(
                        small,
                        player
                    );

                if (acceleration.sqrMagnitude <=
                    0.0001f)
                {
                    continue;
                }

                if (!small.Body.isKinematic)
                {
                    small.Body.AddForce(
                        acceleration,
                        ForceMode.Acceleration
                    );
                }
            }
        }
    }

    private Vector3 CalculatePlayerAcceleration(
        MagneticIron small,
        MagnetPlayer player
    )
    {
        Vector3 difference =
            player.transform.position -
            small.transform.position;

        float distance =
            difference.magnitude;

        if (distance <= 0.0001f ||
            distance >
            small.PlayerInfluenceRange)
        {
            return Vector3.zero;
        }

        Vector3 direction =
            difference / distance;

        MagneticIron.Polarity playerPolarity =
            ConvertPlayerPolarity(
                player.currentPolarity
            );

        bool attract =
            playerPolarity !=
            small.CurrentPolarity;

        float accelerationMagnitude;
        Vector3 accelerationDirection;

        if (attract)
        {
            // バネと減衰を使って、
            // プレイヤー周辺で振動しにくくする。
            float stretch =
                distance -
                small.PlayerAttractionRestDistance;

            Vector3 playerVelocity =
                GetPlayerVelocity(player);

            Vector3 relativeVelocity =
                playerVelocity -
                small.InteractionVelocity;

            float separationSpeed =
                Vector3.Dot(
                    relativeVelocity,
                    direction
                );

            accelerationMagnitude =
                small.PlayerForce *
                stretch;

            accelerationMagnitude +=
                small.PlayerAttractionDamping *
                separationSpeed;

            accelerationMagnitude =
                Mathf.Clamp(
                    accelerationMagnitude,
                    -small.MaxPlayerAcceleration,
                    small.MaxPlayerAcceleration
                );

            accelerationDirection =
                direction;
        }
        else
        {
            float distanceRate =
                Mathf.Clamp01(
                    distance /
                    small.PlayerInfluenceRange
                );

            float proximity =
                1.0f -
                distanceRate;

            accelerationMagnitude =
                small.PlayerForce *
                proximity *
                proximity;

            accelerationMagnitude =
                Mathf.Min(
                    accelerationMagnitude,
                    small.MaxPlayerAcceleration
                );

            accelerationDirection =
                -direction;
        }

        return ScaleDirection(
            accelerationDirection,
            small.PlayerHorizontalMultiplier,
            small.PlayerVerticalMultiplier
        ) * accelerationMagnitude;
    }

    private void ApplySmallLargeForces()
    {
        foreach (MagneticIron large
                 in _largeIrons)
        {
            if (large.CurrentPolarity ==
                MagneticIron.Polarity.None)
            {
                large.ApplyLargeControlledMotion(
                    Vector3.zero,
                    false
                );

                continue;
            }

            int affectingSmallCount = 0;

            Vector3 totalAccelerationOnLarge =
                Vector3.zero;

            foreach (MagneticIron small
                     in _smallIrons)
            {
                if (small.CurrentPolarity ==
                    MagneticIron.Polarity.None)
                {
                    continue;
                }

                float distance =
                    Vector3.Distance(
                        small.transform.position,
                        large.transform.position
                    );

                if (distance >
                    large.SmallInteractionRange)
                {
                    continue;
                }

                affectingSmallCount++;

                Vector3 accelerationOnSmall =
                    CalculateSmallLargeAcceleration(
                        small,
                        large
                    );

                if (accelerationOnSmall.sqrMagnitude <=
                    0.0001f)
                {
                    continue;
                }

                if (!small.Body.isKinematic)
                {
                    small.Body.AddForce(
                        accelerationOnSmall,
                        ForceMode.Acceleration
                    );
                }

                // Smallに加えた加速度と反対方向を
                // Large側の磁力加速度として加算する。
                totalAccelerationOnLarge -=
                    accelerationOnSmall;
            }

            bool magnetActive =
                affectingSmallCount >=
                large.RequiredSmallCount;

            large.ApplyLargeControlledMotion(
                totalAccelerationOnLarge,
                magnetActive
            );
        }
    }

    private Vector3
        CalculateSmallLargeAcceleration(
            MagneticIron small,
            MagneticIron large
        )
    {
        Vector3 difference =
            large.transform.position -
            small.transform.position;

        float distance =
            difference.magnitude;

        if (distance <= 0.0001f ||
            distance >
            large.SmallInteractionRange)
        {
            return Vector3.zero;
        }

        Vector3 direction =
            difference / distance;

        bool attract =
            small.CurrentPolarity !=
            large.CurrentPolarity;

        float accelerationMagnitude;
        Vector3 accelerationDirection;

        if (attract)
        {
            float stretch =
                distance -
                large.AttractionRestDistance;

            Vector3 relativeVelocity =
                large.InteractionVelocity -
                small.InteractionVelocity;

            float separationSpeed =
                Vector3.Dot(
                    relativeVelocity,
                    direction
                );

            accelerationMagnitude =
                large.AttractionSpringStrength *
                stretch;

            accelerationMagnitude +=
                large.AttractionDamping *
                separationSpeed;

            accelerationMagnitude =
                Mathf.Clamp(
                    accelerationMagnitude,
                    -large.MaxInteractionAcceleration,
                    large.MaxInteractionAcceleration
                );

            accelerationDirection =
                direction;
        }
        else
        {
            float distanceRate =
                Mathf.Clamp01(
                    distance /
                    large.SmallInteractionRange
                );

            float proximity =
                1.0f -
                distanceRate;

            // 二乗すると少し離れただけで反発力が極端に弱くなるため、
            // 線形にしてLargeを押し出しやすくする。
            accelerationMagnitude =
                large.RepulsionForce *
                proximity;

            accelerationMagnitude =
                Mathf.Clamp(
                    accelerationMagnitude,
                    0.0f,
                    large.MaxInteractionAcceleration
                );

            // directionはSmallからLargeへ向かう方向。
            // Smallは反対方向へ押し戻す。
            accelerationDirection =
                -direction;
        }

        return ScaleDirection(
            accelerationDirection,
            large.InteractionHorizontalMultiplier,
            large.InteractionVerticalMultiplier
        ) * accelerationMagnitude;
    }

    private Vector3 GetPlayerVelocity(
        MagnetPlayer player
    )
    {
        if (!_playerBodies.TryGetValue(
                player,
                out Rigidbody playerBody))
        {
            playerBody =
                player.GetComponent<Rigidbody>();

            _playerBodies[player] =
                playerBody;
        }

        if (playerBody == null)
        {
            return Vector3.zero;
        }

        return playerBody.linearVelocity;
    }

    private void ApplySmallIdleBrakes()
    {
        foreach (MagneticIron small
                 in _smallIrons)
        {
            if (small != null)
            {
                small.ApplyIdleBrake();
            }
        }
    }

    private void ClampSmallSpeeds()
    {
        foreach (MagneticIron small
                 in _smallIrons)
        {
            if (small != null)
            {
                small.ClampSmallHorizontalSpeed();
            }
        }
    }

    private static Vector3 ScaleDirection(
        Vector3 direction,
        float horizontalMultiplier,
        float verticalMultiplier
    )
    {
        return new Vector3(
            direction.x *
            horizontalMultiplier,

            direction.y *
            verticalMultiplier,

            direction.z *
            horizontalMultiplier
        );
    }

    private static MagneticIron.Polarity
        ConvertPlayerPolarity(
            MagnetPlayer.Polarity polarity
        )
    {
        return polarity ==
               MagnetPlayer.Polarity.North
            ? MagneticIron.Polarity.North
            : MagneticIron.Polarity.South;
    }

    private static MagneticIron.Polarity
        ResolveInheritedPolarity(
            MagneticIron.Polarity sourcePolarity,
            MagneticIron.PolarityInheritance mode
        )
    {
        if (sourcePolarity ==
            MagneticIron.Polarity.None)
        {
            return MagneticIron.Polarity.None;
        }

        if (mode ==
            MagneticIron.PolarityInheritance.Same)
        {
            return sourcePolarity;
        }

        return sourcePolarity ==
               MagneticIron.Polarity.North
            ? MagneticIron.Polarity.South
            : MagneticIron.Polarity.North;
    }
}