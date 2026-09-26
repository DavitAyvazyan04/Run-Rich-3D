using UnityEngine;

namespace RunRichClone
{
    public sealed class RunRichCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4.8f, -7.2f);
        [SerializeField] private float followSpeed = 7f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);
            var lookTarget = target.position + Vector3.up * 1.25f + Vector3.forward * 4.5f;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookTarget - transform.position),
                followSpeed * Time.deltaTime);
        }
    }
}
