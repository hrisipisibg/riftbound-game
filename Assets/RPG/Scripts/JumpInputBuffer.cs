namespace Riftbound
{
    public sealed class JumpInputBuffer
    {
        const float CoyoteTime = .10f, BufferTime = .12f;
        float groundedAt = float.NegativeInfinity, pressedUntil = float.NegativeInfinity;
        bool consumed;

        public void Reset()
        {
            groundedAt = pressedUntil = float.NegativeInfinity;
            consumed = false;
        }

        public bool TryJump(float now, bool grounded, bool pressed, bool allowed)
        {
            if (!allowed) { Reset(); return false; }
            if (grounded) { groundedAt = now; consumed = false; }
            if (pressed) pressedUntil = now + BufferTime;
            if (consumed || now > pressedUntil || now - groundedAt > CoyoteTime) return false;
            consumed = true;
            groundedAt = pressedUntil = float.NegativeInfinity;
            return true;
        }
    }
}
