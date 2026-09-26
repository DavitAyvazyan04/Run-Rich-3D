using UnityEngine;

namespace RunRichClone
{
    public sealed class RunRichPickup : MonoBehaviour
    {
        public PickupKind kind;
        public int value;
        public bool collected;

        private void Update()
        {
            if (!collected && (kind == PickupKind.Money || kind == PickupKind.Alcohol))
                transform.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
                return;

            var player = other.GetComponentInParent<RunRichPlayer>();
            if (player == null)
                return;

            collected = true;
            player.Game.HandlePickup(this);
        }
    }
}
