# Error handling

A failed identity, Followers, or Following request must abort before modifying follow state. A failed PUT stops the current run and preserves its current target plus unfinished targets in pending.json. On the next run, recompute current followers minus following so accounts already followed or no longer following the owner are not retried. Never retry by first unfollowing. Keep upstream errors and credentials out of persisted status and public responses.
