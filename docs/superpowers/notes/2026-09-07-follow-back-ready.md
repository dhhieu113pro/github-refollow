# Follow-back readiness

The existing implementation, tests, configuration, and CI have been reviewed. The root cause is the absence of a Followers lookup and an unconditional unfollow/follow loop. The approved remedy is a current follower difference and PUT-only execution. The next action is to implement and verify this correction in an isolated branch; no live account changes are authorized for testing.
