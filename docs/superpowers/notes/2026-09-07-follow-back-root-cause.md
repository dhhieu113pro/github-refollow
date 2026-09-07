# Follow-back root cause

The current RefollowService reads only GetFollowingAsync, merges its result with pending recovery, and calls UnfollowAsync followed by FollowAsync for every target. GitHubFollowingClient has no GetFollowersAsync method. Consequently followers who are not already followed cannot be discovered, and a live run unnecessarily deletes existing follow relationships. The fix is to use current Followers minus Following as the only automatic target set and to remove DELETE from execution.
