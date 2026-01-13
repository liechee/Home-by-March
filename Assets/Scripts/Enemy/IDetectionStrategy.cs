using UnityEngine;
using Utilities;

namespace HomeByMarch {
    public interface IDetectionStrategy {
        bool Execute(Transform player, Transform detector, CountdownTimer timer);
    }
}