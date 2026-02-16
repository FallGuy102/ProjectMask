namespace Mask
{
    public class CrabMask : AnimalMask
    {
        public override void BeforeMove()
        {
            if (!GameManager.Instance.CanMoveTo(transform.position + movement))
            {
                movement = -movement; // Reverse direction
            }
        }
    }
}