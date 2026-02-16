namespace Mask
{
    public class RabbitMask : AnimalMask
    {
        public override void BeforeMove()
        {
            if (!GameManager.Instance.CanMoveTo(transform.position + movement))
            {
                movement = -movement; // Reverse direction
                (lightRangeForward, lightRangeBack) = (lightRangeBack, lightRangeForward); // Swap light ranges
            }
        }
    }
}